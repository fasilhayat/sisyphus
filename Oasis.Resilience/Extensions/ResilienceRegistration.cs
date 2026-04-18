namespace Oasis.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Oasis.Resilience.Proxies;
using System.Reflection;

public static class ResilienceRegistration
{
    public static IServiceCollection AddResilience(this IServiceCollection services)
    {
        services.AddSingleton<ResilienceRuntime>();
        return services;
    }

    public static IServiceCollection AddResilientService<TInterface, TImpl>(this IServiceCollection services) 
        where TImpl : class, TInterface, new() 
        where TInterface : class
    {
        services.AddSingleton(sp =>
        {
            var runtime = sp.GetRequiredService<ResilienceRuntime>();
            var proxy =  DispatchProxy.Create<TInterface, ResilientProxy<TInterface>>();
            var p = proxy as ResilientProxy<TInterface>;

            p.DecoratedInstance = new TImpl();
            p.ResilienceActorRef = runtime.Actor;
            return proxy;
        });

        return services;
    }
}