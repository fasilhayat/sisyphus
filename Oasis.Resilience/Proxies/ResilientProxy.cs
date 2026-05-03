namespace Oasis.Resilience.Proxies;

using Akka.Actor;
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

        if (retryAttr is null && breakerAttr is null)
            return targetMethod.Invoke(DecoratedInstance, args);

        return InvokeResilient(implementedMethod, args, retryAttr, breakerAttr);
    }

    /// <summary>
    /// Invokes the specified method with resilience logic, supporting only methods returning Task<T>.
    /// </summary>
    /// <param name="implMethod">The method to invoke.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <param name="attr">The resilience configuration attribute.</param>
    /// <returns>The result of the invoked method.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the method does not return a generic Task<T>.</exception>
    private object InvokeResilient(MethodInfo implementedMethod, object[] args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr)
    {
        var returnType = implementedMethod.ReturnType;

        if (!returnType.IsGenericType) throw new InvalidOperationException("Only Task<T> supported.");

        var resultType = returnType.GetGenericArguments()[0];
        var method = typeof(ResilientProxy<T>).GetMethod(nameof(InvokeGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

        return method.Invoke(this, [implementedMethod, args, retryAttr, breakerAttr])!;
    }

    /// <summary>
    /// Invokes a generic asynchronous method on the decorated instance with resilience and retry logic.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the invoked method.</typeparam>
    /// <param name="implementedMethod">The MethodInfo representing the generic method to invoke.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <param name="attr">The resilience configuration attributes.</param>
    /// <returns>A task representing the asynchronous operation, containing the result of the invoked method.</returns>
    private async Task<TResult> InvokeGeneric<TResult>(MethodInfo implementedMethod, object[] args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr)
    {
        var operationKey = $"{typeof(T).FullName}.{implementedMethod.Name}";

        Func<Task<object>> operation = async () =>
        {
            var task = (Task<TResult>)implementedMethod.Invoke(DecoratedInstance, args)!;
            return await task;
        };

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

        throw new InvalidOperationException("No resilience attributes configured.");
    }
}