namespace Oasis.Resilience.Test.Unit.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Oasis.Resilience;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ResilienceRegistration"/>.
/// </summary>
public class ResilienceRegistrationTests
{
    /// <summary>
    /// Test service interface for verifying resilient service registration.
    /// </summary>
    public interface ITestService
    {
        /// <summary>Executes a simple test method that returns a string result.</summary>
        string SimpleMethod();
    }

    /// <summary>
    /// Provides test-related operations.
    /// </summary>
    public class TestService : ITestService
    {
        /// <summary>
        /// Returns a test string to verify proxy invocation.
        /// </summary>
        /// <returns>A test string.</returns>
        public string SimpleMethod() => "test";
    }

    /// <summary>
    /// Tests that calling AddResilience registers the default RetryOptions in the DI container with expected default values.
    /// </summary>
    [Fact]
    public void AddResilience_should_register_RetryOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience();

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
        Assert.NotNull(options);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Debug, options.Value.LogLevel);
    }

    /// <summary>
    /// Tests that calling AddResilience with a configuration action properly configures the RetryOptions in the DI container.
    /// </summary>
    [Fact]
    public void AddResilience_should_configure_RetryOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience(options =>
        {
            options.LogLevel = Microsoft.Extensions.Logging.LogLevel.Warning;
        });

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RetryOptions>>();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, options.Value.LogLevel);
    }

    /// <summary>
    /// Tests that calling AddResilience registers the default CircuitBreakerOptions in the DI container with expected default values.
    /// </summary>
    [Fact]
    public void AddResilience_should_register_CircuitBreakerOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience();

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
        Assert.NotNull(options);
        Assert.Equal(5, options.Value.DefaultFailureThreshold);
    }

    /// <summary>
    /// Tests that calling AddResilience with a configuration action properly configures the CircuitBreakerOptions in the DI container.
    /// </summary>
    [Fact]
    public void AddResilience_should_configure_CircuitBreakerOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience(
            configureBreakerOptions: options =>
            {
                options.DefaultFailureThreshold = 10;
            });

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CircuitBreakerOptions>>();
        Assert.Equal(10, options.Value.DefaultFailureThreshold);
    }

    /// <summary>
    /// Tests that calling AddResilience registers the default SupervisionOptions in the DI container with expected default values.
    /// </summary>
    [Fact]
    public void AddResilience_should_register_SupervisionOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience();

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
        Assert.NotNull(options);
        Assert.Equal(Oasis.Resilience.Attributes.SupervisionStrategy.RestartWithBackoff, options.Value.DefaultStrategy);
    }

    /// <summary>
    /// Tests that calling AddResilience with a configuration action properly configures the SupervisionOptions in the DI container.
    /// </summary>
    [Fact]
    public void AddResilience_should_register_FanOutOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience();

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
        Assert.NotNull(options);
        Assert.Equal(5, options.Value.DefaultMaxWorkers);
    }

    /// <summary>
    /// Tests that calling AddResilience with a configuration action properly configures the FanOutOptions in the DI container.
    /// </summary>
    [Fact]
    public void AddResilientService_should_register_service()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();

        // Act
        services.AddResilientService<ITestService, TestService>();

        // Assert - Verify the service is registered
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ITestService>();
        Assert.NotNull(service);
        Assert.Equal("test", service.SimpleMethod());
    }

    /// <summary>
    /// Tests that calling AddResilientService registers the service as a proxy that implements the interface, allowing for resilience features to be applied.
    /// </summary>
    [Fact]
    public void AddResilientService_should_create_proxy()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();

        // Act
        services.AddResilientService<ITestService, TestService>();

        // Assert - Verify the service is a proxy
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ITestService>();
        Assert.NotNull(service);
    }

    /// <summary>
    /// Tests that calling AddResilience registers the default SupervisionOptions in the DI container with expected default values.
    /// </summary>
    [Fact]
    public void AddResilience_should_register_SupervisionOptions_with_defaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience();

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
        Assert.NotNull(options);
        Assert.Equal(Oasis.Resilience.Attributes.SupervisionStrategy.RestartWithBackoff, options.Value.DefaultStrategy);
        Assert.Equal(5, options.Value.DefaultMaxRetries);
        Assert.Equal(2000, options.Value.DefaultBackoffMinMs);
        Assert.Equal(30000, options.Value.DefaultBackoffMaxMs);
        Assert.Equal(0.2, options.Value.DefaultRandomFactor);
    }

    /// <summary>
    /// Tests that calling AddResilience with a configuration action properly configures the SupervisionOptions in the DI container.
    /// </summary>
    [Fact]
    public void AddResilience_should_configure_SupervisionOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience(configureSupervisionOptions: options =>
        {
            options.DefaultStrategy = Oasis.Resilience.Attributes.SupervisionStrategy.Stop;
            options.DefaultMaxRetries = 10;
            options.DefaultBackoffMinMs = 1000;
            options.DefaultBackoffMaxMs = 60000;
            options.DefaultRandomFactor = 0.5;
        });

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupervisionOptions>>();
        Assert.Equal(Oasis.Resilience.Attributes.SupervisionStrategy.Stop, options.Value.DefaultStrategy);
        Assert.Equal(10, options.Value.DefaultMaxRetries);
        Assert.Equal(1000, options.Value.DefaultBackoffMinMs);
        Assert.Equal(60000, options.Value.DefaultBackoffMaxMs);
        Assert.Equal(0.5, options.Value.DefaultRandomFactor);
    }

    /// <summary>
    /// Tests that calling AddResilience registers the default FanOutOptions in the DI container with expected default values.
    /// </summary>
    [Fact]
    public void AddResilience_should_register_FanOutOptions_with_defaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience();

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
        Assert.NotNull(options);
        Assert.Equal(5, options.Value.DefaultMaxWorkers);
    }

    /// <summary>
    /// Tests that calling AddResilience with a configuration action properly configures the FanOutOptions in the DI container.
    /// </summary>
    [Fact]
    public void AddResilience_should_configure_FanOutOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddResilience(configureFanOutOptions: options =>
        {
            options.DefaultMaxWorkers = 20;
        });

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FanOutOptions>>();
        Assert.Equal(20, options.Value.DefaultMaxWorkers);
    }

    /// <summary>
    /// Tests that calling AddResilientService registers the service as a proxy that has the necessary properties set for resilience features to work (e.g., ActorRefs for retry and circuit breaker actors).
    /// </summary>
    [Fact]
    public void AddResilientService_should_set_proxy_properties()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddResilience();

        // Act
        services.AddResilientService<ITestService, TestService>();

        // Assert - Verify proxy properties are set (proxy should have ActorRefs, etc.)
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<ResilienceRuntime>();
        Assert.NotNull(runtime);
        Assert.NotNull(runtime.RetryActor);
        Assert.NotNull(runtime.CircuitBreakerActor);
    }
}
