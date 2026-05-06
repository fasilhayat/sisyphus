namespace Oasis.Resilience.Proxies;

using Actors;
using Akka.Actor;
using Akka.Pattern;
using Attributes;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Provides a dynamic proxy that adds resilience features to method invocations on decorated instances, using an
/// actor-based retry and circuit breaker mechanism.
/// </summary>
/// <remarks>Methods decorated with RetryAttribute and/or CircuitBreakerAttribute are executed with resilience logic managed by resilience actors. Only asynchronous methods returning Task<T> are supported.</remarks>
/// <typeparam name="T">The interface or class type to proxy.</typeparam>
public class ResilientProxy<T> : DispatchProxy
{
    /// <summary>
    /// Caches retry attributes discovered on methods to avoid repeated reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodInfo, RetryAttribute?> RetryAttributeCache = new();

    /// <summary>
    /// Caches circuit breaker attributes discovered on methods to avoid repeated reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodInfo, CircuitBreakerAttribute?> CircuitBreakerAttributeCache = new();

    /// <summary>
    /// Caches supervision attributes discovered on methods to avoid repeated reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodInfo, SupervisionAttribute?> SupervisionAttributeCache = new();

    /// <summary>
    /// Caches fan-out attributes discovered on methods to avoid repeated reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodInfo, FanOutAttribute?> FanOutAttributeCache = new();

    /// <summary>
    /// Gets or sets the instance being decorated.
    /// </summary>
    public T DecoratedInstance { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor reference used for resilience operations.
    /// </summary>
    public IActorRef ResilienceActorRef { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor reference used for circuit breaker operations.
    /// </summary>
    public IActorRef CircuitBreakerActorRef { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor system used for supervision and fan-out operations.
    /// </summary>
    public ActorSystem ActorSystem { get; set; } = default!;

    /// <summary>
    /// Gets or sets the supervision options for fallback values.
    /// </summary>
    public SupervisionOptions? SupervisionOptions { get; set; }

    /// <summary>
    /// Gets or sets the fan-out options for fallback values.
    /// </summary>
    public FanOutOptions? FanOutOptions { get; set; }

    /// <summary>
    /// Invokes the specified method on the decorated instance, applying resilience logic if the method is decorated
    /// with a RetryAttribute.
    /// </summary>
    /// <param name="targetMethod">The method to invoke.</param>
    /// <param name="args">An array of arguments to pass to the method.</param>
    /// <returns>The result of the invoked method.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the implementation method cannot be found on the decorated instance.</exception>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);

        var implementedMethod = DecoratedInstance!.GetType().GetMethod(targetMethod.Name, targetMethod.GetParameters()
                .Select(p => p.ParameterType)
                .ToArray());

        if (implementedMethod is null) throw new InvalidOperationException($"Implementation method not found: {targetMethod.Name}");

        var retryAttr = RetryAttributeCache.GetOrAdd(implementedMethod, m => m.GetCustomAttribute<RetryAttribute>());
        var breakerAttr = CircuitBreakerAttributeCache.GetOrAdd(implementedMethod, m => m.GetCustomAttribute<CircuitBreakerAttribute>());
        var supervisionAttr = SupervisionAttributeCache.GetOrAdd(implementedMethod, m => m.GetCustomAttribute<SupervisionAttribute>());
        var fanOutAttr = FanOutAttributeCache.GetOrAdd(implementedMethod, m => m.GetCustomAttribute<FanOutAttribute>());

        if (retryAttr is null && breakerAttr is null && supervisionAttr is null && fanOutAttr is null)
            return targetMethod.Invoke(DecoratedInstance, args ?? Array.Empty<object?>());

        return InvokeResilient(implementedMethod, args ?? Array.Empty<object?>(), retryAttr, breakerAttr, supervisionAttr, fanOutAttr);
    }

    /// <summary>
    /// Invokes the specified method with resilience logic, supporting only methods returning Task<T>.
    /// </summary>
    /// <param name="implementedMethod">The method to invoke.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <param name="retryAttr">The retry configuration attribute.</param>
    /// <param name="breakerAttr">The circuit breaker configuration attribute.</param>
    /// <param name="supervisionAttr">The supervision configuration attribute.</param>
    /// <param name="fanOutAttr">The fan-out configuration attribute.</param>
    /// <returns>The result of the invoked method.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the method does not return a generic Task<T>.</exception>
    private object InvokeResilient(MethodInfo implementedMethod, object?[]? args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        var returnType = implementedMethod.ReturnType;

        if (!returnType.IsGenericType) throw new InvalidOperationException("Only Task<T> supported.");

        var resultType = returnType.GetGenericArguments()[0];
        var method = typeof(ResilientProxy<T>).GetMethod(nameof(InvokeGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

        return method.Invoke(this, [implementedMethod, args, retryAttr, breakerAttr, supervisionAttr, fanOutAttr])!;
    }

    /// <summary>
    /// Invokes a generic asynchronous method on the decorated instance with resilience and retry logic.
    /// </summary>
    private async Task<TResult> InvokeGeneric<TResult>(MethodInfo implementedMethod, object[] args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        if (fanOutAttr is not null)
        {
            return await HandleFanOut<TResult>(implementedMethod, args, fanOutAttr, supervisionAttr);
        }

        var operation = CreateOperation<TResult>(implementedMethod, args);

        if (supervisionAttr is not null)
        {
            operation = WrapWithSupervision(operation, supervisionAttr);
        }

        return await ExecuteResilienceStrategy<TResult>(operation, implementedMethod, breakerAttr, retryAttr, supervisionAttr);
    }

    /// <summary>
    /// Executes the appropriate resilience strategy based on configured attributes.
    /// </summary>
    private async Task<TResult> ExecuteResilienceStrategy<TResult>(Func<Task<object>> operation, MethodInfo implementedMethod, CircuitBreakerAttribute? breakerAttr, RetryAttribute? retryAttr, SupervisionAttribute? supervisionAttr)
    {
        if (breakerAttr is not null)
            return await ExecuteWithCircuitBreaker<TResult>($"{typeof(T).FullName}.{implementedMethod.Name}", operation, breakerAttr, retryAttr);
        
        if (retryAttr is not null)
            return await ExecuteWithRetry<TResult>(operation, retryAttr);
        

        if (supervisionAttr is not null)
            return (TResult)await operation();
        
        throw new InvalidOperationException("No resilience attributes configured.");
    }

    /// <summary>
    /// Creates the base operation function for invoking the decorated method.
    /// </summary>
    private Func<Task<object>> CreateOperation<TResult>(MethodInfo implementedMethod, object[] args)
    {
        return async () =>
        {
            var result = implementedMethod.Invoke(DecoratedInstance, args);
            if (result is null) throw new InvalidOperationException("Method invocation returned null");

            var task = (Task<TResult>)result;
            var taskResult = await task;
            return taskResult!;
        };
    }

    /// <summary>
    /// Executes an operation with circuit breaker protection.
    /// </summary>
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

    /// <summary>
    /// Handles circuit breaker failure, either throwing or falling back to retry.
    /// </summary>
    private async Task<TResult> HandleCircuitBreakerFailure<TResult>(Status.Failure failure, Func<Task<object>> operation, RetryAttribute? retryAttr)
    {
        if (failure.Cause is CircuitBreakerActor.CircuitBreakerOpenException || retryAttr is null) throw failure.Cause;

        var result = await ResilienceActorRef.Ask<object>(
            new RetryActor.Execute(
                operation,
                retryAttr.MaxAttempts,
                TimeSpan.FromMilliseconds(retryAttr.InitialDelay)));

        if (result is Status.Failure retryFailure) throw retryFailure.Cause;

        return (TResult)result!;
    }

    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    private async Task<TResult> ExecuteWithRetry<TResult>(Func<Task<object>> operation, RetryAttribute retryAttr)
    {
        var result = await ResilienceActorRef.Ask<object>(new RetryActor.Execute(operation, retryAttr.MaxAttempts, TimeSpan.FromMilliseconds(retryAttr.InitialDelay)));
        if (result is Status.Failure f) throw f.Cause;

        return (TResult)result!;
    }

    /// <summary>
    /// Handles fan-out operations by distributing work across multiple actor workers.
    /// </summary>
    private async Task<TResult> HandleFanOut<TResult>(MethodInfo method, object[] args, FanOutAttribute fanOut, SupervisionAttribute? supervision)
    {
        // Extract fan-out parameters
        var fanOutParams = ExtractFanOutParameters(fanOut, supervision);

        // Create supervisor for worker actors
        var supervisor = CreateWorkerSupervisor(fanOut.WorkerActorType, fanOutParams.Strategy, fanOutParams.BackoffMinMs, fanOutParams.BackoffMaxMs, fanOutParams.RandomFactor);

        // Find the split parameter
        var splitParams = ExtractSplitParameters(method, args, fanOut.SplitParameterName);

        // Fan-out: send work to multiple workers
        var tasks = SendWorkToWorkers<TResult>(supervisor, fanOut.WorkerActorType, splitParams.SplitValues, splitParams.OtherArgs, method, fanOutParams.MaxWorkers);

        // Fan-in: collect results
        var results = await Task.WhenAll(tasks);
        return AggregateResults<TResult>(results, fanOut.WorkerActorType);
    }

    /// <summary>
    /// Extracts fan-out configuration parameters with fallback to options.
    /// </summary>
    private FanOutParameters ExtractFanOutParameters(
        FanOutAttribute fanOut, SupervisionAttribute? supervision)
    {
        var maxWorkers = fanOut.MaxWorkers != 5 ? fanOut.MaxWorkers : (FanOutOptions?.DefaultMaxWorkers ?? 5);
        var supervisionStrategy = supervision?.Strategy ?? SupervisionOptions?.DefaultStrategy ?? SupervisionStrategy.RestartWithBackoff;
        var backoffMinMs = supervision?.BackoffMinMs ?? SupervisionOptions?.DefaultBackoffMinMs ?? 2000;
        var backoffMaxMs = supervision?.BackoffMaxMs ?? SupervisionOptions?.DefaultBackoffMaxMs ?? 30000;
        var randomFactor = supervision?.RandomFactor ?? SupervisionOptions?.DefaultRandomFactor ?? 0.2;

        return new FanOutParameters
        {
            MaxWorkers = maxWorkers,
            Strategy = supervisionStrategy,
            BackoffMinMs = backoffMinMs,
            BackoffMaxMs = backoffMaxMs,
            RandomFactor = randomFactor
        };
    }

    /// <summary>
    /// Creates and starts a supervisor actor for the specified worker actor type, using the given supervision strategy
    /// and backoff parameters.
    /// </summary>
    /// <remarks>If the supervision strategy is RestartWithBackoff, the supervisor uses a backoff policy to
    /// restart the worker actor after failures. Otherwise, a standard supervisor is created without backoff. The
    /// returned actor reference can be used to interact with the supervisor or to send messages to the worker actor
    /// through the supervisor.</remarks>
    /// <param name="workerActorType">The type of the worker actor to supervise. Must derive from ActorBase.</param>
    /// <param name="strategy">The supervision strategy to apply to the worker actor. Determines how failures are handled.</param>
    /// <param name="minMs">The minimum backoff duration, in milliseconds, to wait before restarting the worker actor when using a backoff strategy. Must be non-negative.</param>
    /// <param name="maxMs">The maximum backoff duration, in milliseconds, to wait before restarting the worker actor when using a backoff strategy. Must be greater than or equal to minMs.</param>
    /// <param name="factor">The randomization factor used to calculate the backoff delay when using a backoff strategy. Must be non-negative.</param>
    /// <returns>An IActorRef representing the supervisor actor responsible for managing the specified worker actor.</returns>
    private IActorRef CreateWorkerSupervisor(Type workerActorType, SupervisionStrategy strategy, int minMs, int maxMs, double factor)
    {
        var workerProps = Props.Create(() => (ActorBase)Activator.CreateInstance(workerActorType)!);

        if (strategy == SupervisionStrategy.RestartWithBackoff)
        {
            var supervisorProps = BackoffSupervisor.Props(
                childProps: workerProps,
                childName: workerActorType.Name,
                minBackoff: TimeSpan.FromMilliseconds(minMs),
                maxBackoff: TimeSpan.FromMilliseconds(maxMs),
                randomFactor: factor
            );
            return ActorSystem.ActorOf(supervisorProps, $"{workerActorType.Name}-supervisor");
        }

        return ActorSystem.ActorOf(workerProps, $"{workerActorType.Name}-pool");
    }

    /// <summary>
    /// Extracts the split parameter and remaining arguments from the specified method invocation based on the provided
    /// split parameter name.
    /// </summary>
    /// <remarks>Use this method when you need to separate a specific array parameter (the split parameter)
    /// from the other arguments for further processing, such as batching or partitioning operations.</remarks>
    /// <param name="method">The MethodInfo representing the method whose parameters are being analyzed.</param>
    /// <param name="args">An array of argument values corresponding to the parameters of the method.</param>
    /// <param name="splitParameterName">The name of the parameter to be treated as the split parameter. This parameter must exist in the method's
    /// signature and its value must be an array.</param>
    /// <returns>A SplitParametersResult containing the values of the split parameter and the other arguments.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a parameter with the specified splitParameterName does not exist in the method's parameters.</exception>
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

    /// <summary>
    /// Sends work to multiple worker actors and returns tasks for collecting results.
    /// </summary>
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

    /// <summary>
    /// Creates a worker message based on the worker actor type and parameters.
    /// This method uses a delegate that can be registered to handle specific worker types.
    /// </summary>
    private static Func<Type, object, ParameterInfo[], object[], object>? _messageFactory;

    /// <summary>
    /// Registers a factory function to create worker messages for a specific worker type.
    /// </summary>
    public static void RegisterMessageFactory(Func<Type, object, ParameterInfo[], object[], object> factory)
    {
        _messageFactory = factory;
    }

    /// <summary>
    /// Creates a worker message based on the worker actor type and parameters.
    /// </summary>
    private object CreateWorkerMessage(Type workerType, object splitValue, ParameterInfo[] parameters, object[] otherArgs)
    {
        if (_messageFactory is not null)
        {
            return _messageFactory(workerType, splitValue, parameters, otherArgs);
        }

        throw new InvalidOperationException($"No message factory registered. Register one using RegisterMessageFactory.");
    }

    /// <summary>
    /// Represents a delegate that aggregates an array of objects into a single result of a specified type.
    /// </summary>
    /// <remarks>The delegate takes an array of input objects, a source type, and a target type, and returns
    /// an aggregated result. This field may be null if no aggregator is configured.</remarks>
    private static Func<object[], Type, Type, object>? _resultAggregator;

    /// <summary>
    /// Registers a custom result aggregator function to be used for combining results.
    /// </summary>
    /// <remarks>Registering a new aggregator overrides any previously registered aggregator. This method
    /// should be called before any aggregation operations that rely on the custom logic.</remarks>
    /// <param name="aggregator">A delegate that defines how to aggregate an array of result objects, given their source and target types. The
    /// function receives the results, the source type, and the target type, and returns the aggregated result.</param>
    public static void RegisterResultAggregator(Func<object[], Type, Type, object> aggregator)
    {
        _resultAggregator = aggregator;
    }

    /// <summary>
    /// Aggregates the specified result objects into a single value of the specified type using the registered result
    /// aggregator.
    /// </summary>
    /// <typeparam name="TResult">The type of the aggregated result to return.</typeparam>
    /// <param name="results">An array of result objects to aggregate. Each element represents an individual result to be combined.</param>
    /// <param name="workerType">The type of the worker that produced the results. Used by the aggregator to determine aggregation logic.</param>
    /// <returns>The aggregated result of type TResult produced by combining the input results.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no result aggregator has been registered prior to calling this method.</exception>
    private TResult AggregateResults<TResult>(object[] results, Type workerType)
    {
        if (_resultAggregator is not null)
            return (TResult)_resultAggregator(results, workerType, typeof(TResult));

        throw new InvalidOperationException($"No result aggregator registered. Register one using RegisterResultAggregator.");
    }

    /// <summary>
    /// Wraps the specified asynchronous operation with supervision logic as defined by the provided supervision
    /// attribute.
    /// </summary>
    /// <param name="operation">The asynchronous operation to be executed. The operation is represented as a function that returns a task
    /// producing an object result.</param>
    /// <param name="supervision">The supervision attribute that defines the supervision behavior to apply to the operation.</param>
    /// <returns>A function that, when invoked, executes the original operation under the specified supervision policy and
    /// returns a task representing the asynchronous result.</returns>
    private Func<Task<object>> WrapWithSupervision(Func<Task<object>> operation, SupervisionAttribute supervision)
    {
        return async () => await operation();
    }
}
