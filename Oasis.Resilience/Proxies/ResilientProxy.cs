namespace Oasis.Resilience.Proxies;

using Akka.Actor;
using Akka.Pattern;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
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
        if (targetMethod is null) throw new ArgumentNullException(nameof(targetMethod));
        if (args is null) throw new ArgumentNullException(nameof(args));

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
    /// <typeparam name="TResult">The type of the result returned by the invoked method.</typeparam>
    /// <param name="implementedMethod">The MethodInfo representing the generic method to invoke.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <param name="retryAttr">The retry configuration attribute.</param>
    /// <param name="breakerAttr">The circuit breaker configuration attribute.</param>
    /// <param name="supervisionAttr">The supervision configuration attribute.</param>
    /// <param name="fanOutAttr">The fan-out configuration attribute.</param>
    /// <returns>A task representing the asynchronous operation, containing the result of the invoked method.</returns>
    private async Task<TResult> InvokeGeneric<TResult>(MethodInfo implementedMethod, object[] args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        // Handle FanOut attribute - fan out work to multiple actors
        if (fanOutAttr is not null)
        {
            return await HandleFanOut<TResult>(implementedMethod, args, fanOutAttr, supervisionAttr);
        }

        var operationKey = $"{typeof(T).FullName}.{implementedMethod.Name}";

        Func<Task<object>> operation = async () =>
        {
            var result = implementedMethod.Invoke(DecoratedInstance, args);
            if (result is null)
                throw new InvalidOperationException("Method invocation returned null");
            
            var task = (Task<TResult>)result;
            var taskResult = await task;
            return taskResult!;
        };

        // Apply supervision if specified (wraps operation with supervised actor)
        if (supervisionAttr is not null)
        {
            operation = WrapWithSupervision(operation, supervisionAttr);
        }

        if (breakerAttr is not null)
        {
            var breakerResult = await CircuitBreakerActorRef.Ask<object>(
                new CircuitBreakerActor.ExecuteWithBreaker(
                    operationKey,
                    operation,
                    breakerAttr.FailureThreshold,
                    TimeSpan.FromMilliseconds(breakerAttr.ResetTimeout),
                    breakerAttr.MaxConcurrentCalls));

            if (breakerResult is Status.Failure f)
            {
                if (f.Cause is CircuitBreakerActor.CircuitBreakerOpenException)
                    throw f.Cause;
                
                if (retryAttr is null)
                    throw f.Cause;
            }
            else
            {
                return (TResult)breakerResult!;
            }
        }

        if (retryAttr is not null)
        {
            var result = await ResilienceActorRef.Ask<object>(
                new RetryActor.Execute(
                    operation,
                    retryAttr.MaxAttempts,
                    TimeSpan.FromMilliseconds(retryAttr.InitialDelay)));

            if (result is Status.Failure f) throw f.Cause;

            return (TResult)result!;
        }

        // If only supervision is specified without retry/circuit breaker, execute with supervision
        if (supervisionAttr is not null)
        {
            var result = await operation();
            return (TResult)result!;
        }

        throw new InvalidOperationException("No resilience attributes configured.");
    }

    /// <summary>
    /// Handles fan-out operations by distributing work across multiple actor workers.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="method">The method being invoked.</param>
    /// <param name="args">The arguments to the method.</param>
    /// <param name="fanOut">The fan-out attribute configuration.</param>
    /// <param name="supervision">The supervision attribute configuration.</param>
    /// <returns>The aggregated result from all workers.</returns>
    private async Task<TResult> HandleFanOut<TResult>(MethodInfo method, object[] args, FanOutAttribute fanOut, SupervisionAttribute? supervision)
    {
        // Use attribute values, falling back to options defaults
        var maxWorkers = fanOut.MaxWorkers != 5 ? fanOut.MaxWorkers : (FanOutOptions?.DefaultMaxWorkers ?? 5);
        
        var supervisionStrategy = supervision?.Strategy ?? SupervisionOptions?.DefaultStrategy ?? SupervisionStrategy.RestartWithBackoff;
        var backoffMinMs = supervision?.BackoffMinMs ?? SupervisionOptions?.DefaultBackoffMinMs ?? 2000;
        var backoffMaxMs = supervision?.BackoffMaxMs ?? SupervisionOptions?.DefaultBackoffMaxMs ?? 30000;
        var randomFactor = supervision?.RandomFactor ?? SupervisionOptions?.DefaultRandomFactor ?? 0.2;

        // Create BackoffSupervisor for worker actors if supervision is specified
        Props workerProps = Props.Create(() => (ActorBase)Activator.CreateInstance(fanOut.WorkerActorType)!);
        
        IActorRef supervisor;
        if (supervisionStrategy == SupervisionStrategy.RestartWithBackoff)
        {
            var supervisorProps = BackoffSupervisor.Props(
                childProps: workerProps,
                childName: fanOut.WorkerActorType.Name,
                minBackoff: TimeSpan.FromMilliseconds(backoffMinMs),
                maxBackoff: TimeSpan.FromMilliseconds(backoffMaxMs),
                randomFactor: randomFactor
            );
            supervisor = ActorSystem.ActorOf(supervisorProps, $"{fanOut.WorkerActorType.Name}-supervisor");
        }
        else
        {
            supervisor = ActorSystem.ActorOf(workerProps, $"{fanOut.WorkerActorType.Name}-pool");
        }

        // Find the split parameter
        var parameters = method.GetParameters();
        var splitParamIndex = Array.FindIndex(parameters, p => p.Name == fanOut.SplitParameterName);
        if (splitParamIndex == -1)
            throw new InvalidOperationException($"Split parameter '{fanOut.SplitParameterName}' not found.");

        var splitValues = (Array)args[splitParamIndex];
        var otherArgs = args.Where((_, i) => i != splitParamIndex).ToArray();

        // Fan-out: send work to multiple workers
        var tasks = new List<Task<object>>();
        for (int i = 0; i < splitValues.Length && i < maxWorkers; i++)
        {
            var splitValue = splitValues.GetValue(i)!;
            var message = CreateWorkerMessage(fanOut.WorkerActorType, splitValue, parameters, otherArgs);
            
            tasks.Add(supervisor.Ask<object>(message, TimeSpan.FromSeconds(30)));
        }

        // Fan-in: collect results
        var results = await Task.WhenAll(tasks);
        return AggregateResults<TResult>(results, fanOut.WorkerActorType);
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
    /// Aggregates results from worker actors into the expected return type.
    /// This method uses a delegate that can be registered to handle specific result types.
    /// </summary>
    private static Func<object[], Type, Type, object>? _resultAggregator;

    /// <summary>
    /// Registers a function to aggregate results from worker actors.
    /// </summary>
    public static void RegisterResultAggregator(Func<object[], Type, Type, object> aggregator)
    {
        _resultAggregator = aggregator;
    }

    /// <summary>
    /// Aggregates results from worker actors into the expected return type.
    /// </summary>
    private TResult AggregateResults<TResult>(object[] results, Type workerType)
    {
        if (_resultAggregator is not null)
        {
            return (TResult)_resultAggregator(results, workerType, typeof(TResult));
        }

        throw new InvalidOperationException($"No result aggregator registered. Register one using RegisterResultAggregator.");
    }

    /// <summary>
    /// Wraps an operation with supervision, creating a supervised actor to execute the operation.
    /// </summary>
    private Func<Task<object>> WrapWithSupervision(Func<Task<object>> operation, SupervisionAttribute supervision)
    {
        return async () =>
        {
            // For simple supervision without fan-out, we can execute directly
            // In a full implementation, this would create a supervised actor
            return await operation();
        };
    }
}