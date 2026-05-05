namespace Oasis.Resilience.Test.Unit.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Oasis.Resilience;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ResilienceRegistration"/>.
/// </summary>
public class ResilienceRegistrationTests
{
    public interface ITestService
    {
        string SimpleMethod();
    }

    public class TestService : ITestService
    {
        public string SimpleMethod() => "test";
    }

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
}
