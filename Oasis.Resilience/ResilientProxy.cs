namespace Oasis.Resilience;

using Akka.Actor;
using System.Reflection;

/// <summary>
/// Provides a dynamic proxy that adds resilience features, such as retry logic, to method calls on decorated instances
/// of type T.
/// </summary>
/// <remarks>The ResilientProxy<T> class uses DispatchProxy to intercept method calls and applies resilience
/// policies, such as retries, to methods marked with the ResilientAttribute. Methods without this attribute are invoked
/// directly on the decorated instance. This proxy is intended for use with actor-based systems and requires an
/// ActorSystem to manage resilience operations. Thread safety and actor system lifecycle management are the
/// responsibility of the caller.</remarks>
/// <typeparam name="T">The type of the interface or class to proxy. Typically, this is the service or component interface whose methods
/// require resilience.</typeparam>
public class ResilientProxy<T> : DispatchProxy
{
    /// <summary>
    /// Gets or sets the instance of the decorated object.
    /// </summary>
    public T? DecoratedInstance { get; set; }

    /// <summary>
    /// Gets or sets the actor system used to communicate with remote actors through a proxy.
    /// </summary>
    /// <remarks>Set this property to specify the underlying actor system that will be used for proxy-based
    /// remote communication. If not set, remote actor operations that depend on a proxy may not function as
    /// expected.</remarks>
    public ActorSystem? ProxyActorSystem { get; set; }

    /// <summary>
    /// Invokes the specified method on the decorated instance, applying resilience policies if the method is marked
    /// with a ResilientAttribute.
    /// </summary>
    /// <remarks>If the target method is decorated with ResilientAttribute, the invocation is executed with
    /// retry logic using an actor-based resilience mechanism. Otherwise, the method is invoked directly without
    /// additional resilience handling.</remarks>
    /// <param name="targetMethod">The method to invoke on the decorated instance. Cannot be null.</param>
    /// <param name="args">An array of arguments to pass to the method. May be null if the method does not require parameters.</param>
    /// <returns>The result of the invoked method. If the method is marked with ResilientAttribute, returns a Task representing
    /// the asynchronous operation with resilience applied; otherwise, returns the direct result of the method
    /// invocation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if targetMethod is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the decorated instance or the proxy actor system is null.</exception>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            throw new ArgumentNullException(nameof(targetMethod));
        if (DecoratedInstance == null)
            throw new InvalidOperationException("DecoratedInstance must not be null.");
        if (ProxyActorSystem == null)
            throw new InvalidOperationException("ProxyActorSystem must not be null.");

        var attribute = targetMethod.GetCustomAttribute<ResilientAttribute>();
        if (attribute == null)
            return targetMethod.Invoke(DecoratedInstance, args);

        var retryActor = ProxyActorSystem.ActorOf(Props.Create(typeof(ResilienceActor)));

        // Wrap call in Ask and handle success/failure
        var task = retryActor.Ask<object>(
            new ResilienceActor.Execute
            {
                Operation = () => (Task<string>)targetMethod.Invoke(DecoratedInstance, args)!,
                MaxAttempts = attribute.MaxAttempts,
                InitialDelay = TimeSpan.FromSeconds(attribute.InitialDelaySeconds)
            }
        ).ContinueWith(t =>
        {
            if (t.Result is Status.Failure failure)
                throw failure.Cause;

            return (string)t.Result;
        });

        return task;
    }
}