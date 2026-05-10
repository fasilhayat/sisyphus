namespace Oasis.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Oasis.Resilience.Proxies;
using System.Reflection;

/// <summary>
/// Provides extension methods for registering resilience services with the DI container.
/// </summary>
public static class ResilienceRegistration
{
    /// <summary>
    /// Adds resilience infrastructure (retry, circuit breaker, supervision, fan-out options and runtime) to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureRetryOptions">Optional delegate to configure retry options.</param>
    /// <param name="configureBreakerOptions">Optional delegate to configure circuit breaker options.</param>
    /// <param name="configureSupervisionOptions">Optional delegate to configure supervision options.</param>
    /// <param name="configureFanOutOptions">Optional delegate to configure fan-out options.</param>
    /// <returns>The service collection with resilience services registered.</returns>
    public static IServiceCollection AddResilience(
        this IServiceCollection services,
        Action<RetryOptions>? configureRetryOptions = null,
        Action<CircuitBreakerOptions>? configureBreakerOptions = null,
        Action<SupervisionOptions>? configureSupervisionOptions = null,
        Action<FanOutOptions>? configureFanOutOptions = null)
    {
        services.Configure<RetryOptions>(options =>
        {
            configureRetryOptions?.Invoke(options);
        });

        services.Configure<CircuitBreakerOptions>(options =>
        {
            configureBreakerOptions?.Invoke(options);
        });

        services.Configure<SupervisionOptions>(options =>
        {
            configureSupervisionOptions?.Invoke(options);
        });

        services.Configure<FanOutOptions>(options =>
        {
            configureFanOutOptions?.Invoke(options);
        });

        services.TryAddSingleton<ResilienceRuntime>();
        return services;
    }

    /// <summary>
    /// Registers a service interface with a concrete implementation, wrapped in a resilience proxy.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type.</typeparam>
    /// <typeparam name="TImplementation">The concrete implementation type.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The service collection with the resilient service registered.</returns>
    public static IServiceCollection AddResilientService<TInterface, TImplementation>(this IServiceCollection services)
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.AddSingleton<TImplementation>();

        services.TryAddSingleton(sp =>
        {
            var runtime = sp.GetRequiredService<ResilienceRuntime>();
            var implementation = sp.GetRequiredService<TImplementation>();
            var proxy = DispatchProxy.Create<TInterface, ResilientProxy<TInterface>>();
            var p = proxy as ResilientProxy<TInterface> ??
                throw new InvalidOperationException($"Failed to create proxy for {typeof(TInterface).Name}");

            p.DecoratedInstance = implementation;
            p.ResilienceActorRef = runtime.RetryActor;
            p.CircuitBreakerActorRef = runtime.CircuitBreakerActor;
            p.ActorSystem = runtime.System;
            p.RetryOptions = runtime.RetryOptions;
            p.CircuitBreakerOptions = runtime.CircuitBreakerOptions;
            p.SupervisionOptions = runtime.SupervisionOptions;
            p.FanOutOptions = runtime.FanOutOptions;
            return proxy;
        });

        return services;
    }
}
