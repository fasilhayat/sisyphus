namespace ResilienceWithAop;

using Akka.Actor;
using System.Reflection;

public class ResilientProxy<T> : DispatchProxy
{
    public T? DecoratedInstance { get; set; }
    public ActorSystem? ProxyActorSystem { get; set; }

    protected override object? Invoke(MethodInfo targetMethod, object[] args)
    {
        var attribute = targetMethod.GetCustomAttribute<ResilientAttribute>();
        if (attribute == null)
            return targetMethod.Invoke(DecoratedInstance, args);

        var retryActor = ProxyActorSystem!.ActorOf(Props.Create(typeof(RetryActor)));

        // Wrap call in Ask and handle success/failure
        var task = retryActor.Ask<object>(
            new RetryActor.Execute
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