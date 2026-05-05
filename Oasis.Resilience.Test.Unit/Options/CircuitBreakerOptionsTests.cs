namespace Oasis.Resilience.Test.Unit.Options;

using Oasis.Resilience;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerOptions"/>.
/// </summary>
public class CircuitBreakerOptionsTests
{
    [Fact]
    public void DefaultFailureThreshold_should_be_5()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Assert
        Assert.Equal(5, options.DefaultFailureThreshold);
    }

    [Fact]
    public void DefaultResetTimeout_should_be_30000()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Assert
        Assert.Equal(30000, options.DefaultResetTimeout);
    }

    [Fact]
    public void DefaultMaxConcurrentCalls_should_be_1()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Assert
        Assert.Equal(1, options.DefaultMaxConcurrentCalls);
    }

    [Fact]
    public void Should_allow_setting_FailureThreshold()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Act
        options.DefaultFailureThreshold = 10;

        // Assert
        Assert.Equal(10, options.DefaultFailureThreshold);
    }

    [Fact]
    public void Should_allow_setting_ResetTimeout()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Act
        options.DefaultResetTimeout = 60000;

        // Assert
        Assert.Equal(60000, options.DefaultResetTimeout);
    }

    [Fact]
    public void Should_allow_setting_MaxConcurrentCalls()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Act
        options.DefaultMaxConcurrentCalls = 5;

        // Assert
        Assert.Equal(5, options.DefaultMaxConcurrentCalls);
    }
}
