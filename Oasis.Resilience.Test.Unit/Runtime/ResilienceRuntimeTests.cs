namespace Oasis.Resilience.Test.Unit.Runtime;

using Microsoft.Extensions.DependencyInjection;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ResilienceRuntime"/>.
/// </summary>
/// <remarks>Tests the runtime initialization, actor creation, and shutdown functionality.</remarks>
public class ResilienceRuntimeTests
{
    /// <summary>
    /// Tests that ResilienceRuntime can be created via DI container.
    /// </summary>
    [Fact]
    public void Runtime_should_be_creatable_via_DI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();

        // Act
        services.AddSingleton<ResilienceRuntime>(sp =>
        {
            var retryOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
            var breakerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
            var supervisionOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
            var fanOutOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
            return new ResilienceRuntime(
                retryOptions,
                breakerOptions,
                supervisionOptions,
                fanOutOptions);
        });

        // Assert
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();
        Assert.NotNull(runtime);
        Assert.NotNull(runtime.System);
    }

    /// <summary>
    /// Tests that ResilienceRuntime exposes the actor system.
    /// </summary>
    [Fact]
    public void Runtime_should_expose_ActorSystem()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();
        services.AddSingleton<ResilienceRuntime>(sp =>
        {
            var retryOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
            var breakerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
            var supervisionOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
            var fanOutOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
            return new ResilienceRuntime(
                retryOptions,
                breakerOptions,
                supervisionOptions,
                fanOutOptions);
        });

        // Act
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();

        // Assert
        Assert.NotNull(runtime.System);
        Assert.Contains("resilience-system", runtime.System.Name);
    }

    /// <summary>
    /// Tests that ResilienceRuntime creates RetryActor.
    /// </summary>
    [Fact]
    public void Runtime_should_create_RetryActor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();
        services.AddSingleton<ResilienceRuntime>(sp =>
        {
            var retryOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
            var breakerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
            var supervisionOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
            var fanOutOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
            return new ResilienceRuntime(
                retryOptions,
                breakerOptions,
                supervisionOptions,
                fanOutOptions);
        });

        // Act
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();

        // Assert
        Assert.NotNull(runtime.RetryActor);
    }

    /// <summary>
    /// Tests that ResilienceRuntime creates CircuitBreakerActor.
    /// </summary>
    [Fact]
    public void Runtime_should_create_CircuitBreakerActor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();
        services.AddSingleton<ResilienceRuntime>(sp =>
        {
            var retryOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
            var breakerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
            var supervisionOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
            var fanOutOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
            return new ResilienceRuntime(
                retryOptions,
                breakerOptions,
                supervisionOptions,
                fanOutOptions);
        });

        // Act
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();

        // Assert
        Assert.NotNull(runtime.CircuitBreakerActor);
    }

    /// <summary>
    /// Tests that ResilienceRuntime exposes SupervisionOptions.
    /// </summary>
    [Fact]
    public void Runtime_should_expose_SupervisionOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();
        services.AddSingleton<ResilienceRuntime>(sp =>
        {
            var retryOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
            var breakerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
            var supervisionOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
            var fanOutOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
            return new ResilienceRuntime(
                retryOptions,
                breakerOptions,
                supervisionOptions,
                fanOutOptions);
        });

        // Act
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();

        // Assert
        Assert.NotNull(runtime.SupervisionOptions);
        Assert.Equal(Oasis.Resilience.Attributes.SupervisionStrategy.RestartWithBackoff, runtime.SupervisionOptions.DefaultStrategy);
    }

    /// <summary>
    /// Tests that ResilienceRuntime exposes FanOutOptions.
    /// </summary>
    [Fact]
    public void Runtime_should_expose_FanOutOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();
        services.AddSingleton<ResilienceRuntime>(sp =>
        {
            var retryOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
            var breakerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
            var supervisionOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
            var fanOutOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
            return new ResilienceRuntime(
                retryOptions,
                breakerOptions,
                supervisionOptions,
                fanOutOptions);
        });

        // Act
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();

        // Assert
        Assert.NotNull(runtime.FanOutOptions);
        Assert.Equal(5, runtime.FanOutOptions.DefaultMaxWorkers);
    }

    /// <summary>
    /// Tests that ResilienceRuntime properties can be accessed without error.
    /// </summary>
    [Fact]
    public void Runtime_should_expose_all_properties()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();
        services.AddSingleton<ResilienceRuntime>(sp =>
        {
            var retryOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
            var breakerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
            var supervisionOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
            var fanOutOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
            return new ResilienceRuntime(
                retryOptions,
                breakerOptions,
                supervisionOptions,
                fanOutOptions);
        });

        // Act
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();

        // Assert - Verify all properties are accessible
        Assert.NotNull(runtime.System);
        Assert.NotNull(runtime.RetryActor);
        Assert.NotNull(runtime.CircuitBreakerActor);
        Assert.NotNull(runtime.SupervisionOptions);
        Assert.NotNull(runtime.FanOutOptions);
    }
}
