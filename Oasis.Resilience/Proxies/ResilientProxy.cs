namespace Oasis.Resilience.Proxies;

using Akka.Actor;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using System.Collections.Concurrent;
using System.Reflection;

public class ResilientProxy<T> : DispatchProxy
{
    private static readonly ConcurrentDictionary<MethodInfo, ResilientAttribute?>
        AttributeCache = new();

    public T DecoratedInstance { get; set; } = default!;

    public IActorRef ResilienceActorRef { get; set; } = default!;

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        var implementedMethod = DecoratedInstance!.GetType().GetMethod(targetMethod.Name, targetMethod.GetParameters()
                        .Select(p => p.ParameterType)
                        .ToArray());

        if (implementedMethod is null)
            throw new InvalidOperationException(
                $"Implementation method not found: {targetMethod.Name}");

        var attr =
            AttributeCache.GetOrAdd(implementedMethod, m => m.GetCustomAttribute<ResilientAttribute>());

        if (attr is null) 
            return targetMethod.Invoke(DecoratedInstance, args);

        return InvokeResilient(implementedMethod, args, attr);
    }

    private object InvokeResilient(MethodInfo implMethod, object[] args,  ResilientAttribute attr)
    {
        var returnType = implMethod.ReturnType;

        if (!returnType.IsGenericType)
            throw new InvalidOperationException(
                "Only Task<T> supported.");

        var resultType =
            returnType.GetGenericArguments()[0];

        var method = typeof(ResilientProxy<T>).GetMethod(nameof(InvokeGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

        return method.Invoke(this, [implMethod, args, attr])!;
    }

    private async Task<TResult> InvokeGeneric<TResult>(MethodInfo implementedMethod, object[] args, ResilientAttribute attr)
    {
        var result = await ResilienceActorRef.Ask<object>(
                new ResilienceActor.Execute(
                    async () =>
                    {
                        var task = (Task<TResult>) implementedMethod.Invoke(DecoratedInstance, args)!;
                        return await task;
                    },
                    attr.MaxAttempts,
                    TimeSpan.FromSeconds(
                        attr.InitialDelaySeconds)));

        if (result is Status.Failure f)  throw f.Cause;

        return (TResult)result!;
    }
}