namespace Oasis.Resilience.Proxies;

using Actors;
using Akka.Actor;
using Akka.Pattern;
using Attributes;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// A DispatchProxy that intercepts method calls and applies resilience policies (retry, circuit breaker, supervision, fan-out)
/// based on attributes applied to the target method.
/// </summary>
/// <typeparam name="T">The interface type to proxy.</typeparam>
public class ResilientProxy<T> : DispatchProxy
{
    private static readonly ConcurrentDictionary<MethodInfo, RetryAttribute?> RetryAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, CircuitBreakerAttribute?> CircuitBreakerAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, SupervisionAttribute?> SupervisionAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, FanOutAttribute?> FanOutAttributeCache = new();

    private static Func<Type, object, ParameterInfo[], object[], object>? _globalMessageFactory;
    private static Func<object[], Type, Type, object>? _globalResultAggregator;

    private Func<Type, object, ParameterInfo[], object[], object>? _instanceMessageFactory;
    private Func<object[], Type, Type, object>? _instanceResultAggregator;

    /// <summary>
    /// Gets or sets the decorated service instance that method calls are forwarded to.
    /// </summary>
    public T DecoratedInstance { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor ref for retry operations.
    /// </summary>
    public IActorRef ResilienceActorRef { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor ref for circuit breaker operations.
    /// </summary>
    public IActorRef CircuitBreakerActorRef { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor system used to create supervised and worker actors.
    /// </summary>
    public ActorSystem ActorSystem { get; set; } = default!;

    /// <summary>
    /// Gets or sets global supervision options used as defaults when attributes omit values.
    /// </summary>
    public SupervisionOptions? SupervisionOptions { get; set; }

    /// <summary>
    /// Gets or sets global fan-out options used as defaults when attributes omit values.
    /// </summary>
    public FanOutOptions? FanOutOptions { get; set; }

    /// <summary>
    /// Registers a global message factory used to create worker messages for fan-out operations.
    /// </summary>
    /// <param name="factory">A function that creates a message from worker type, split value, parameters, and other args.</param>
    public static void RegisterMessageFactory(Func<Type, object, ParameterInfo[], object[], object> factory)
    {
        _globalMessageFactory = factory;
    }

    /// <summary>
    /// Registers a global result aggregator used to combine worker results from fan-out operations.
    /// </summary>
    /// <param name="aggregator">A function that aggregates results into the expected return type.</param>
    public static void RegisterResultAggregator(Func<object[], Type, Type, object> aggregator)
    {
        _globalResultAggregator = aggregator;
    }

    /// <summary>
    /// Sets an instance-level message factory, overriding the global factory for this proxy instance.
    /// </summary>
    /// <param name="factory">A function that creates a message from worker type, split value, parameters, and other args.</param>
    public void SetMessageFactory(Func<Type, object, ParameterInfo[], object[], object> factory)
    {
        _instanceMessageFactory = factory;
    }

    /// <summary>
    /// Sets an instance-level result aggregator, overriding the global aggregator for this proxy instance.
    /// </summary>
    /// <param name="aggregator">A function that aggregates results into the expected return type.</param>
    public void SetResultAggregator(Func<object[], Type, Type, object> aggregator)
    {
        _instanceResultAggregator = aggregator;
    }

    /// <summary>
    /// Intercepts the method invocation, resolves resilience attributes, and delegates to the appropriate handler.
    /// </summary>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);

        var method = ResolveImplementedMethod(targetMethod);

        var retryAttr = RetryAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<RetryAttribute>());
        var breakerAttr = CircuitBreakerAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<CircuitBreakerAttribute>());
        var supervisionAttr = SupervisionAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<SupervisionAttribute>());
        var fanOutAttr = FanOutAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<FanOutAttribute>());

        var safeArgs = args ?? [];

        if (!HasResilienceAttributes(retryAttr, breakerAttr, supervisionAttr, fanOutAttr))
            return targetMethod.Invoke(DecoratedInstance, safeArgs);

        return InvokeResilient(method, safeArgs, retryAttr, breakerAttr, supervisionAttr, fanOutAttr);
    }

    /// <summary>Determines whether any resilience attributes are present on the method.</summary>
    private static bool HasResilienceAttributes(RetryAttribute? retry, CircuitBreakerAttribute? breaker, SupervisionAttribute? supervision, FanOutAttribute? fanOut)
        => retry is not null || breaker is not null || supervision is not null || fanOut is not null;

    /// <summary>Resolves the implemented method from the decorated instance matching the target method signature.</summary>
    private MethodInfo ResolveImplementedMethod(MethodInfo targetMethod)
    {
        var method = DecoratedInstance!.GetType().GetMethod(targetMethod.Name,
            targetMethod.GetParameters().Select(p => p.ParameterType).ToArray());
        return method ?? throw new InvalidOperationException($"Implementation method not found: {targetMethod.Name}");
    }

    /// <summary>
    /// Routes the invocation to the generic resilience pipeline, resolving the return type and invoking
    /// <see cref="InvokeGeneric{TResult}"/> via reflection.
    /// </summary>
    private object InvokeResilient(MethodInfo implementedMethod, object?[]? args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        var returnType = implementedMethod.ReturnType;

        if (!returnType.IsGenericType)
            throw new InvalidOperationException("Only Task<T> supported.");

        var resultType = returnType.GetGenericArguments()[0];
        var method = typeof(ResilientProxy<T>).GetMethod(nameof(InvokeGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

        return method.Invoke(this, [implementedMethod, args, retryAttr, breakerAttr, supervisionAttr, fanOutAttr])!;
    }

    /// <summary>Invokes the resilience pipeline for a given return type, dispatching to fan-out or wrapping operation with supervision as configured.</summary>
    private async Task<TResult> InvokeGeneric<TResult>(MethodInfo implementedMethod, object[] args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        var ct = ExtractCancellationToken(args, implementedMethod);

        if (fanOutAttr is not null)
            return await HandleFanOut<TResult>(implementedMethod, args, fanOutAttr, supervisionAttr);

        var operation = CreateOperation<TResult>(implementedMethod, args);

        if (supervisionAttr is not null)
            operation = WrapWithSupervision(operation, supervisionAttr);

        return await ExecuteResilienceStrategy<TResult>(operation, implementedMethod, breakerAttr, retryAttr, supervisionAttr, ct);
    }

    /// <summary>Extracts the <see cref="CancellationToken"/> from the method arguments, if present.</summary>
    private static CancellationToken ExtractCancellationToken(object[] args, MethodInfo method)
    {
        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken) && args[i] is CancellationToken token)
                return token;
        }
        return CancellationToken.None;
    }

    /// <summary>Dispatches the operation to the appropriate resilience strategy based on configured attributes.</summary>
    private async Task<TResult> ExecuteResilienceStrategy<TResult>(Func<Task<object>> operation, MethodInfo implementedMethod, CircuitBreakerAttribute? breakerAttr, RetryAttribute? retryAttr, SupervisionAttribute? supervisionAttr, CancellationToken ct)
    {
        if (breakerAttr is not null)
            return await ExecuteWithCircuitBreaker<TResult>($"{typeof(T).FullName}.{implementedMethod.Name}", operation, breakerAttr, retryAttr);

        if (retryAttr is not null)
            return await ExecuteWithRetry<TResult>(operation, retryAttr, ct);

        if (supervisionAttr is not null)
            return (TResult)await operation();

        throw new InvalidOperationException("No resilience attributes configured.");
    }

    /// <summary>Creates a delegate that invokes the method and returns its typed result as an object.</summary>
    private Func<Task<object>> CreateOperation<TResult>(MethodInfo implementedMethod, object[] args)
    {
        return async () =>
        {
            var result = implementedMethod.Invoke(DecoratedInstance, args);
            if (result is null) throw new InvalidOperationException("Method invocation returned null");

            var task = (Task<TResult>)result;
            return (await task)!;
        };
    }

    /// <summary>Executes the operation through the circuit breaker actor, falling back to retry on failure if configured.</summary>
    private async Task<TResult> ExecuteWithCircuitBreaker<TResult>(string operationKey, Func<Task<object>> operation, CircuitBreakerAttribute breakerAttr, RetryAttribute? retryAttr)
    {
        var breakerResult = await CircuitBreakerActorRef.Ask<object>(
            new CircuitBreakerActor.ExecuteWithBreaker(
                operationKey,
                operation,
                breakerAttr.FailureThreshold,
                TimeSpan.FromMilliseconds(breakerAttr.ResetTimeout),
                breakerAttr.MaxConcurrentCalls));

        if (breakerResult is Status.Failure failure)
            return await HandleCircuitBreakerFailure<TResult>(failure, operation, retryAttr);

        return (TResult)breakerResult!;
    }

    /// <summary>Handles a circuit breaker failure by optionally delegating to the retry strategy.</summary>
    private async Task<TResult> HandleCircuitBreakerFailure<TResult>(Status.Failure failure, Func<Task<object>> operation, RetryAttribute? retryAttr)
    {
        if (failure.Cause is CircuitBreakerActor.CircuitBreakerOpenException || retryAttr is null)
            throw failure.Cause;

        var result = await ResilienceActorRef.Ask<object>(
            new RetryActor.Execute(
                operation,
                retryAttr.MaxAttempts,
                TimeSpan.FromMilliseconds(retryAttr.InitialDelay)));

        if (result is Status.Failure retryFailure) throw retryFailure.Cause;

        return (TResult)result!;
    }

    /// <summary>Executes the operation with retry logic via the retry actor.</summary>
    private async Task<TResult> ExecuteWithRetry<TResult>(Func<Task<object>> operation, RetryAttribute retryAttr, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var result = await ResilienceActorRef.Ask<object>(
            new RetryActor.Execute(operation, retryAttr.MaxAttempts, TimeSpan.FromMilliseconds(retryAttr.InitialDelay)));

        if (result is Status.Failure f) throw f.Cause;

        return (TResult)result!;
    }

    /// <summary>Wraps the operation in a supervised actor that restarts on failure according to the supervision strategy.</summary>
    private Func<Task<object>> WrapWithSupervision(Func<Task<object>> operation, SupervisionAttribute supervision)
    {
        return async () =>
        {
            var props = SupervisionActor.CreateSupervisorProps(operation, supervision);
            var supervisor = ActorSystem.ActorOf(props, $"supervised-op-{Guid.NewGuid():N}");
            var result = await supervisor.Ask<object>(new RunOperation(), TimeSpan.FromSeconds(30));

            if (result is Status.Failure failure)
                throw failure.Cause;

            return result;
        };
    }

    /// <summary>Orchestrates a fan-out operation by distributing work to multiple worker actors and aggregating results.</summary>
    private async Task<TResult> HandleFanOut<TResult>(MethodInfo method, object[] args, FanOutAttribute fanOut, SupervisionAttribute? supervision)
    {
        var fanOutParams = ExtractFanOutParameters(fanOut, supervision);

        var supervisor = CreateWorkerSupervisor(fanOut.WorkerActorType, fanOutParams.Strategy, fanOutParams.BackoffMinMs, fanOutParams.BackoffMaxMs, fanOutParams.RandomFactor);

        var splitParams = ExtractSplitParameters(method, args, fanOut.SplitParameterName);

        var tasks = SendWorkToWorkers<TResult>(supervisor, fanOut.WorkerActorType, splitParams.SplitValues, splitParams.OtherArgs, method, fanOutParams.MaxWorkers);

        var results = await Task.WhenAll(tasks);
        return AggregateResults<TResult>(results, fanOut.WorkerActorType);
    }

    /// <summary>Extracts and resolves the fan-out parameters from attributes and global options.</summary>
    private FanOutParameters ExtractFanOutParameters(FanOutAttribute fanOut, SupervisionAttribute? supervision)
    {
        return new FanOutParameters
        {
            MaxWorkers = ResolveMaxWorkers(fanOut),
            Strategy = ResolveStrategy(supervision),
            BackoffMinMs = ResolveBackoffMinMs(supervision),
            BackoffMaxMs = ResolveBackoffMaxMs(supervision),
            RandomFactor = ResolveRandomFactor(supervision)
        };
    }

    /// <summary>Resolves the maximum number of workers from the attribute or global default.</summary>
    private int ResolveMaxWorkers(FanOutAttribute fanOut)
        => fanOut.MaxWorkers != 5 ? fanOut.MaxWorkers : (FanOutOptions?.DefaultMaxWorkers ?? 5);

    /// <summary>Resolves the supervision strategy from the attribute or global default.</summary>
    private SupervisionStrategy ResolveStrategy(SupervisionAttribute? supervision)
        => supervision?.Strategy ?? SupervisionOptions?.DefaultStrategy ?? SupervisionStrategy.RestartWithBackoff;

    /// <summary>Resolves the minimum backoff interval in milliseconds from the attribute or global default.</summary>
    private int ResolveBackoffMinMs(SupervisionAttribute? supervision)
        => supervision?.BackoffMinMs ?? SupervisionOptions?.DefaultBackoffMinMs ?? 2000;

    /// <summary>Resolves the maximum backoff interval in milliseconds from the attribute or global default.</summary>
    private int ResolveBackoffMaxMs(SupervisionAttribute? supervision)
        => supervision?.BackoffMaxMs ?? SupervisionOptions?.DefaultBackoffMaxMs ?? 30000;

    /// <summary>Resolves the random backoff factor from the attribute or global default.</summary>
    private double ResolveRandomFactor(SupervisionAttribute? supervision)
        => supervision?.RandomFactor ?? SupervisionOptions?.DefaultRandomFactor ?? 0.2;

    /// <summary>Creates a worker supervisor using either backoff or pool supervision based on the strategy.</summary>
    private IActorRef CreateWorkerSupervisor(Type workerActorType, SupervisionStrategy strategy, int minMs, int maxMs, double factor)
    {
        var workerProps = Props.Create(() => (ActorBase)Activator.CreateInstance(workerActorType)!);

        if (strategy == SupervisionStrategy.RestartWithBackoff)
            return CreateBackoffSupervisor(workerActorType, workerProps, minMs, maxMs, factor);

        return CreatePoolSupervisor(workerActorType, workerProps, MapToDirective(strategy));
    }

    /// <summary>Creates a backoff supervisor that restarts the worker with exponential backoff.</summary>
    private IActorRef CreateBackoffSupervisor(Type workerActorType, Props workerProps, int minMs, int maxMs, double factor)
    {
        var supervisorProps = BackoffSupervisor.Props(
            childProps: workerProps,
            childName: workerActorType.Name,
            minBackoff: TimeSpan.FromMilliseconds(minMs),
            maxBackoff: TimeSpan.FromMilliseconds(maxMs),
            randomFactor: factor);
        return ActorSystem.ActorOf(supervisorProps, $"{workerActorType.Name}-supervisor");
    }

    /// <summary>Maps a <see cref="SupervisionStrategy"/> to the corresponding Akka.NET <see cref="Directive"/>.</summary>
    private static Directive MapToDirective(SupervisionStrategy strategy) => strategy switch
    {
        SupervisionStrategy.Restart => Directive.Restart,
        SupervisionStrategy.Stop => Directive.Stop,
        SupervisionStrategy.Escalate => Directive.Escalate,
        SupervisionStrategy.Resume => Directive.Resume,
        _ => Directive.Restart
    };

    /// <summary>Creates a pool-based supervisor with a one-for-one strategy using the given directive.</summary>
    private IActorRef CreatePoolSupervisor(Type workerActorType, Props workerProps, Directive directive)
    {
        var supervisionStrategy = new OneForOneStrategy(
            maxNrOfRetries: -1,
            withinTimeRange: TimeSpan.FromMinutes(1),
            localOnlyDecider: _ => directive);

        var poolProps = new WorkerPoolProps(workerProps, supervisionStrategy);
        return ActorSystem.ActorOf(Props.Create(() => new WorkerPoolActor(poolProps)), $"{workerActorType.Name}-pool");
    }

    /// <summary>Extracts the split parameter value and remaining arguments from the method invocation.</summary>
    private SplitParametersResult ExtractSplitParameters(MethodInfo method, object[] args, string splitParameterName)
    {
        var parameters = method.GetParameters();
        var splitParamIndex = Array.FindIndex(parameters, p => p.Name == splitParameterName);
        if (splitParamIndex == -1)
            throw new InvalidOperationException($"Split parameter '{splitParameterName}' not found.");

        var splitValues = (Array)args[splitParamIndex];
        var otherArgs = args.Where((_, i) => i != splitParamIndex).ToArray();

        return new SplitParametersResult
        {
            SplitValues = splitValues,
            OtherArgs = otherArgs
        };
    }

    /// <summary>Sends work messages to worker actors via the supervisor and collects the result tasks.</summary>
    private List<Task<object>> SendWorkToWorkers<TResult>(IActorRef supervisor, Type workerActorType, Array splitValues, object[] otherArgs, MethodInfo method, int maxWorkers)
    {
        var tasks = new List<Task<object>>();
        var parameters = method.GetParameters();

        for (int i = 0; i < splitValues.Length && i < maxWorkers; i++)
        {
            var splitValue = splitValues.GetValue(i)!;
            var message = CreateWorkerMessage(workerActorType, splitValue, parameters, otherArgs);
            tasks.Add(supervisor.Ask<object>(message, TimeSpan.FromSeconds(30)));
        }

        return tasks;
    }

    /// <summary>Creates a worker message using the registered message factory.</summary>
    private object CreateWorkerMessage(Type workerType, object splitValue, ParameterInfo[] parameters, object[] otherArgs)
    {
        var factory = _instanceMessageFactory ?? _globalMessageFactory;
        if (factory is not null)
            return factory(workerType, splitValue, parameters, otherArgs);

        throw new InvalidOperationException($"No message factory registered for worker type '{workerType.Name}'. Register one using RegisterMessageFactory.");
    }

    /// <summary>Aggregates worker results using the registered result aggregator.</summary>
    private TResult AggregateResults<TResult>(object[] results, Type workerType)
    {
        var aggregator = _instanceResultAggregator ?? _globalResultAggregator;
        if (aggregator is not null)
            return (TResult)aggregator(results, workerType, typeof(TResult));

        throw new InvalidOperationException($"No result aggregator registered for worker type '{workerType.Name}'. Register one using RegisterResultAggregator.");
    }
}
