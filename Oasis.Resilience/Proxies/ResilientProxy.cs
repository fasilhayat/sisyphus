namespace Oasis.Resilience.Proxies;

using Actors;
using Akka.Actor;
using Akka.Pattern;
using Attributes;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// A <see cref="DispatchProxy"/> that intercepts method calls and applies resilience policies (retry,
/// circuit breaker, supervision, fan-out) based on attributes applied to the target method. Sentinel
/// values (<c>-1</c>) on attribute parameters fall back to the configured global options. Supervisor
/// actors are cached per method (for supervision) and per worker type (for fan-out) to avoid leaking
/// actors across invocations.
/// </summary>
/// <typeparam name="T">The interface type to proxy.</typeparam>
public class ResilientProxy<T> : DispatchProxy, IAsyncDisposable, IDisposable
{
    private static readonly ConcurrentDictionary<MethodInfo, RetryAttribute?> RetryAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, CircuitBreakerAttribute?> CircuitBreakerAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, SupervisionAttribute?> SupervisionAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, FanOutAttribute?> FanOutAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, MethodInfo> ImplementedMethodCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, MethodInfo> InvokeGenericMethodCache = new();

    private static volatile Func<Type, object, ParameterInfo[], object[], object>? _globalMessageFactory;
    private static volatile Func<object[], Type, Type, object>? _globalResultAggregator;

    private readonly ConcurrentDictionary<MethodInfo, Lazy<IActorRef>> _supervisorCache = new();
    private readonly ConcurrentDictionary<Type, Lazy<IActorRef>> _workerSupervisorCache = new();

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
    /// Gets or sets global retry options used as defaults when attributes omit values.
    /// </summary>
    public RetryOptions? RetryOptions { get; set; }

    /// <summary>
    /// Gets or sets global circuit breaker options used as defaults when attributes omit values.
    /// </summary>
    public CircuitBreakerOptions? CircuitBreakerOptions { get; set; }

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

    /// <summary>Resolves the implemented method from the decorated instance matching the target method signature.
    /// Cached per <see cref="MethodInfo"/> so the reflection lookup runs once per interface method.</summary>
    private MethodInfo ResolveImplementedMethod(MethodInfo targetMethod)
    {
        return ImplementedMethodCache.GetOrAdd(targetMethod, tm =>
        {
            var method = DecoratedInstance!.GetType().GetMethod(tm.Name,
                tm.GetParameters().Select(p => p.ParameterType).ToArray());
            return method ?? throw new InvalidOperationException($"Implementation method not found: {tm.Name}");
        });
    }

    /// <summary>
    /// Routes the invocation to the generic resilience pipeline, resolving the return type and invoking
    /// <see cref="InvokeGeneric{TResult}"/> via reflection. The constructed generic <see cref="MethodInfo"/>
    /// is cached per implemented method to avoid the cost of <c>MakeGenericMethod</c> on every call.
    /// </summary>
    private object InvokeResilient(MethodInfo implementedMethod, object?[]? args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        var method = InvokeGenericMethodCache.GetOrAdd(implementedMethod, BuildInvokeGeneric);
        return method.Invoke(this, [implementedMethod, args, retryAttr, breakerAttr, supervisionAttr, fanOutAttr])!;
    }

    /// <summary>Builds the generic <c>InvokeGeneric&lt;TResult&gt;</c> <see cref="MethodInfo"/> for the implemented method.</summary>
    private static MethodInfo BuildInvokeGeneric(MethodInfo implementedMethod)
    {
        var returnType = implementedMethod.ReturnType;
        if (!returnType.IsGenericType)
            throw new InvalidOperationException($"Method '{implementedMethod.Name}' on '{implementedMethod.DeclaringType?.Name}' has unsupported return type '{returnType.Name}'. Only Task<T> is supported.");

        var resultType = returnType.GetGenericArguments()[0];
        return typeof(ResilientProxy<T>).GetMethod(nameof(InvokeGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(resultType);
    }

    /// <summary>Invokes the resilience pipeline for a given return type, dispatching to fan-out or wrapping operation with supervision as configured.</summary>
    private async Task<TResult> InvokeGeneric<TResult>(MethodInfo implementedMethod, object[] args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        var ct = ExtractCancellationToken(args, implementedMethod);

        if (fanOutAttr is not null)
            return await HandleFanOut<TResult>(implementedMethod, args, fanOutAttr, supervisionAttr);

        var operation = CreateOperation<TResult>(implementedMethod, args);

        if (supervisionAttr is not null)
            operation = WrapWithSupervision(implementedMethod, operation, supervisionAttr);

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

    /// <summary>Returns the configured ask timeout, falling back to a 30s default when no options are configured.</summary>
    private TimeSpan AskTimeout => RetryOptions?.AskTimeout ?? TimeSpan.FromSeconds(30);

    /// <summary>Dispatches the operation to the appropriate resilience strategy based on configured attributes.</summary>
    private async Task<TResult> ExecuteResilienceStrategy<TResult>(Func<Task<object>> operation, MethodInfo implementedMethod, CircuitBreakerAttribute? breakerAttr, RetryAttribute? retryAttr, SupervisionAttribute? supervisionAttr, CancellationToken ct)
    {
        if (breakerAttr is not null)
            return await ExecuteWithCircuitBreaker<TResult>($"{typeof(T).FullName}.{implementedMethod.Name}", operation, breakerAttr, retryAttr, ct);

        if (retryAttr is not null)
            return await ExecuteWithRetry<TResult>(operation, retryAttr, ct);

        if (supervisionAttr is not null)
            return (TResult)await operation();

        throw new InvalidOperationException($"No resilience attributes configured on method '{implementedMethod.Name}' of '{implementedMethod.DeclaringType?.Name}'.");
    }

    /// <summary>Creates a delegate that invokes the method and returns its typed result as an object.
    /// Unwraps <see cref="TargetInvocationException"/> so the original exception type reaches the
    /// resilience pipeline (and the caller).</summary>
    private Func<Task<object>> CreateOperation<TResult>(MethodInfo implementedMethod, object[] args)
    {
        return async () =>
        {
            object? result;
            try
            {
                result = implementedMethod.Invoke(DecoratedInstance, args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw tie.InnerException;
            }

            if (result is null) throw new InvalidOperationException($"Method '{implementedMethod.Name}' returned null, but a Task<T> was expected.");

            var task = (Task<TResult>)result;
            return (await task)!;
        };
    }

    /// <summary>Executes the operation through the circuit breaker actor, falling back to retry on failure if configured.</summary>
    private async Task<TResult> ExecuteWithCircuitBreaker<TResult>(string operationKey, Func<Task<object>> operation, CircuitBreakerAttribute breakerAttr, RetryAttribute? retryAttr, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var resolved = OptionsResolver.ResolveCircuitBreaker(breakerAttr, CircuitBreakerOptions);

        try
        {
            var breakerResult = await CircuitBreakerActorRef.Ask<object>(
                new CircuitBreakerActor.ExecuteWithBreaker(
                    operationKey,
                    operation,
                    resolved.FailureThreshold,
                    TimeSpan.FromMilliseconds(resolved.ResetTimeoutMs),
                    resolved.MaxConcurrentCalls),
                AskTimeout,
                ct);

            return (TResult)breakerResult!;
        }
        catch (CircuitBreakerActor.CircuitBreakerOpenException)
        {
            throw;
        }
        catch (Exception ex) when (retryAttr is not null)
        {
            return await HandleCircuitBreakerFailure<TResult>(ex, operation, retryAttr, ct);
        }
    }

    /// <summary>Handles a circuit breaker failure by delegating to the retry strategy.</summary>
    private async Task<TResult> HandleCircuitBreakerFailure<TResult>(Exception cause, Func<Task<object>> operation, RetryAttribute retryAttr, CancellationToken ct)
    {
        _ = cause;
        return await ExecuteWithRetry<TResult>(operation, retryAttr, ct);
    }

    /// <summary>Executes the operation with retry logic via the retry actor, honoring the resolved retry options and exception filter.</summary>
    private async Task<TResult> ExecuteWithRetry<TResult>(Func<Task<object>> operation, RetryAttribute retryAttr, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var resolved = OptionsResolver.ResolveRetry(retryAttr, RetryOptions);

        var result = await ResilienceActorRef.Ask<object>(
            new RetryActor.Execute(
                operation,
                resolved.MaxAttempts,
                TimeSpan.FromMilliseconds(resolved.InitialDelayMs),
                resolved.RetryOn),
            AskTimeout,
            ct);

        if (result is Status.Failure f) throw f.Cause;

        return (TResult)result!;
    }

    /// <summary>
    /// Wraps the operation in a supervised actor that restarts on failure according to the supervision strategy.
    /// The supervisor is cached per method, so repeated invocations reuse the same long-lived actor.
    /// </summary>
    private Func<Task<object>> WrapWithSupervision(MethodInfo method, Func<Task<object>> operation, SupervisionAttribute supervision)
    {
        var supervisor = _supervisorCache.GetOrAdd(method, m =>
            new Lazy<IActorRef>(() => CreateSupervisedRunner(m, supervision), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        return async () =>
        {
            var result = await supervisor.Ask<object>(new RunOperation(operation), AskTimeout);

            if (result is Status.Failure failure)
                throw failure.Cause;

            return result;
        };
    }

    /// <summary>Creates a long-lived supervisor actor for the supplied method using the resolved supervision options.</summary>
    private IActorRef CreateSupervisedRunner(MethodInfo method, SupervisionAttribute supervision)
    {
        var resolved = OptionsResolver.ResolveSupervision(supervision, SupervisionOptions);
        var props = SupervisionActor.CreateSupervisorProps(
            resolved.Strategy, resolved.MaxRetries, resolved.BackoffMinMs, resolved.BackoffMaxMs, resolved.RandomFactor);
        var actorName = $"supervised-{typeof(T).Name}-{method.Name}-{Guid.NewGuid():N}";
        return ActorSystem.ActorOf(props, actorName);
    }

    /// <summary>Orchestrates a fan-out operation by distributing work to multiple worker actors and aggregating results.</summary>
    private async Task<TResult> HandleFanOut<TResult>(MethodInfo method, object[] args, FanOutAttribute fanOut, SupervisionAttribute? supervision)
    {
        var resolvedSupervision = OptionsResolver.ResolveSupervision(supervision, SupervisionOptions);
        var maxWorkers = OptionsResolver.ResolveMaxWorkers(fanOut, FanOutOptions);

        var supervisor = _workerSupervisorCache.GetOrAdd(
            fanOut.WorkerActorType,
            t => new Lazy<IActorRef>(() => CreateWorkerSupervisor(t, resolvedSupervision), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        var splitParams = ExtractSplitParameters(method, args, fanOut.SplitParameterName);
        var tasks = SendWorkToWorkers(supervisor, fanOut.WorkerActorType, splitParams.SplitValues, splitParams.OtherArgs, method, maxWorkers);

        var results = await Task.WhenAll(tasks);
        return AggregateResults<TResult>(results, fanOut.WorkerActorType);
    }

    /// <summary>Creates a worker supervisor (cached per worker type) using the resolved supervision settings.</summary>
    private IActorRef CreateWorkerSupervisor(Type workerActorType, ResolvedSupervision supervision)
    {
        var workerProps = Props.Create(() => (ActorBase)Activator.CreateInstance(workerActorType)!);

        if (supervision.Strategy == SupervisionStrategy.RestartWithBackoff)
            return CreateBackoffSupervisor(workerActorType, workerProps, supervision.BackoffMinMs, supervision.BackoffMaxMs, supervision.RandomFactor);

        return CreatePoolSupervisor(workerActorType, workerProps, MapToDirective(supervision.Strategy), supervision.MaxRetries);
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
        return ActorSystem.ActorOf(supervisorProps, $"{workerActorType.Name}-supervisor-{Guid.NewGuid():N}");
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
    private IActorRef CreatePoolSupervisor(Type workerActorType, Props workerProps, Directive directive, int maxRetries)
    {
        var supervisionStrategy = new OneForOneStrategy(
            maxNrOfRetries: maxRetries,
            withinTimeRange: TimeSpan.FromMinutes(1),
            localOnlyDecider: _ => directive);

        var poolProps = new WorkerPoolProps(workerProps, supervisionStrategy);
        return ActorSystem.ActorOf(Props.Create(() => new WorkerPoolActor(poolProps)), $"{workerActorType.Name}-pool-{Guid.NewGuid():N}");
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

    /// <summary>Sends work messages to worker actors via the supervisor and collects the result tasks.
    /// All <paramref name="splitValues"/> are processed; <paramref name="maxWorkers"/> caps the number
    /// of in-flight requests via a semaphore so the actor system isn't flooded for very large inputs.</summary>
    private List<Task<object>> SendWorkToWorkers(IActorRef supervisor, Type workerActorType, Array splitValues, object[] otherArgs, MethodInfo method, int maxWorkers)
    {
        var tasks = new List<Task<object>>(splitValues.Length);
        var parameters = method.GetParameters();
        var concurrency = Math.Max(1, maxWorkers);
        var gate = new SemaphoreSlim(concurrency, concurrency);
        var askTimeout = AskTimeout;

        for (int i = 0; i < splitValues.Length; i++)
        {
            var splitValue = splitValues.GetValue(i)!;
            var message = CreateWorkerMessage(workerActorType, splitValue, parameters, otherArgs);
            tasks.Add(SendThrottled(supervisor, message, askTimeout, gate));
        }

        _ = ReleaseGateWhenAllComplete(tasks, gate);
        return tasks;
    }

    /// <summary>Awaits a slot in <paramref name="gate"/> before issuing the Ask and releases it when the reply arrives.</summary>
    private static async Task<object> SendThrottled(IActorRef supervisor, object message, TimeSpan askTimeout, SemaphoreSlim gate)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await supervisor.Ask<object>(message, askTimeout).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Disposes the throttling semaphore once every queued work item has finished.</summary>
    private static async Task ReleaseGateWhenAllComplete(List<Task<object>> tasks, SemaphoreSlim gate)
    {
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch { /* individual task failures surface to the aggregator */ }
        finally { gate.Dispose(); }
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

    /// <summary>Synchronous disposal that delegates to <see cref="DisposeAsync"/>; provided so the
    /// proxy is compatible with synchronous DI scope disposal.</summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        GC.SuppressFinalize(this);
    }

    /// <summary>Stops every cached supervisor actor and clears the per-instance caches.
    /// The shared <see cref="ResilienceActorRef"/> and <see cref="CircuitBreakerActorRef"/> are owned
    /// by the <c>ResilienceRuntime</c> and intentionally not stopped here.</summary>
    public async ValueTask DisposeAsync()
    {
        await StopCachedActorsAsync(_supervisorCache.Values).ConfigureAwait(false);
        await StopCachedActorsAsync(_workerSupervisorCache.Values).ConfigureAwait(false);
        _supervisorCache.Clear();
        _workerSupervisorCache.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>Gracefully stops every materialised actor in <paramref name="lazyRefs"/>.</summary>
    private async Task StopCachedActorsAsync(ICollection<Lazy<IActorRef>> lazyRefs)
    {
        if (ActorSystem is null) return;

        foreach (var lazy in lazyRefs)
        {
            if (!lazy.IsValueCreated) continue;
            try { await lazy.Value.GracefulStop(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch { /* swallow — disposal is best-effort */ }
        }
    }
}
