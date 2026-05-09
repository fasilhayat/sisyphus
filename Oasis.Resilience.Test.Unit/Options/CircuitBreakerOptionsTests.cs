namespace Oasis.Resilience.Test.Unit.Options;

using Oasis.Resilience;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerOptions"/>.
/// </summary>
public class CircuitBreakerOptionsTests
{
    /// <summary>
    /// Verifies the default <see cref="CircuitBreakerOptions.DefaultFailureThreshold"/> is 5.
    /// </summary>
    [Fact]
    public void DefaultFailureThreshold_should_be_5()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Assert
        Assert.Equal(5, options.DefaultFailureThreshold);
    }

    /// <summary>
    /// Verifies the default <see cref="CircuitBreakerOptions.DefaultResetTimeout"/> is 30000.
    /// </summary>
    [Fact]
    public void DefaultResetTimeout_should_be_30000()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Assert
        Assert.Equal(30000, options.DefaultResetTimeout);
    }

    /// <summary>
    /// Verifies the default <see cref="CircuitBreakerOptions.DefaultMaxConcurrentCalls"/> is 1.
    /// </summary>
    [Fact]
    public void DefaultMaxConcurrentCalls_should_be_1()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Assert
        Assert.Equal(1, options.DefaultMaxConcurrentCalls);
    }

    /// <summary>
    /// Verifies <see cref="CircuitBreakerOptions.DefaultFailureThreshold"/> can be set to a custom value.
    /// </summary>
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

    /// <summary>
    /// Verifies <see cref="CircuitBreakerOptions.DefaultResetTimeout"/> can be set to a custom value.
    /// </summary>
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

    /// <summary>
    /// Verifies <see cref="CircuitBreakerOptions.DefaultMaxConcurrentCalls"/> can be set to a custom value.
    /// </summary>
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
