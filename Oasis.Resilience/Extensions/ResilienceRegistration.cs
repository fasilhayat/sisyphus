namespace Oasis.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Oasis.Resilience.Proxies;
using System.Reflection;

/// <summary>
/// Provides extension methods for registering resilience-related services and proxies in an IServiceCollection.
/// </summary>
/// <remarks>Use these methods to enable resilience features and to register services with automatic resilience
/// proxying in dependency injection.</remarks>
public static class ResilienceRegistration
{
    /// <summary>
    /// Adds resilience-related services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddResilience(this IServiceCollection services)
    {
        services.AddSingleton<ResilienceRuntime>();
        return services;
    }

    /// <summary>
    /// Registers a resilient proxy for the specified interface and implementation type as a singleton in the service
    /// collection.
    /// </summary>
    /// <remarks>The registered proxy enables resilience features by integrating with
    /// ResilienceRuntime.</remarks>
    /// <typeparam name="TInterface">The interface type to register.</typeparam>
    /// <typeparam name="TImplementation">The concrete implementation type of the interface.</typeparam>
    /// <param name="services">The service collection to add the service to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddResilientService<TInterface, TImplementation>(this IServiceCollection services) 
        where TImplementation : class, TInterface, new() 
        where TInterface : class
    {
        services.AddSingleton(sp =>
        {
            var runtime = sp.GetRequiredService<ResilienceRuntime>();
            var proxy =  DispatchProxy.Create<TInterface, ResilientProxy<TInterface>>();
            var p = proxy as ResilientProxy<TInterface>;

            p.DecoratedInstance = new TImplementation();
            p.ResilienceActorRef = runtime.Actor;
            return proxy;
        });

        return services;
    }
}