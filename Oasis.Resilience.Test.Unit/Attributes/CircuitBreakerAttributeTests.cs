namespace Oasis.Resilience.Test.Unit.Attributes;

using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerAttribute"/>.
/// </summary>
public class CircuitBreakerAttributeTests
{
    /// <summary>
    /// Verifies the default property values of <see cref="CircuitBreakerAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_have_default_values()
    {
        // Arrange
        var attr = new CircuitBreakerAttribute();

        // Assert
        Assert.Equal(5, attr.FailureThreshold);
        Assert.Equal(30000, attr.ResetTimeout);
        Assert.Equal(1, attr.MaxConcurrentCalls);
    }

    /// <summary>
    /// Verifies custom values are properly accepted by <see cref="CircuitBreakerAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_accept_custom_values()
    {
        // Arrange
        var attr = new CircuitBreakerAttribute(
            failureThreshold: 3,
            resetTimeout: 10000,
            maxConcurrentCalls: 2);

        // Assert
        Assert.Equal(3, attr.FailureThreshold);
        Assert.Equal(10000, attr.ResetTimeout);
        Assert.Equal(2, attr.MaxConcurrentCalls);
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for invalid failure threshold values.
    /// </summary>
    [Fact]
    public void Should_throw_for_invalid_failureThreshold()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreakerAttribute(failureThreshold: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreakerAttribute(failureThreshold: -1));
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for a negative reset timeout.
    /// </summary>
    [Fact]
    public void Should_throw_for_negative_resetTimeout()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreakerAttribute(resetTimeout: -1));
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for invalid max concurrent calls values.
    /// </summary>
    [Fact]
    public void Should_throw_for_invalid_maxConcurrentCalls()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreakerAttribute(maxConcurrentCalls: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreakerAttribute(maxConcurrentCalls: -1));
    }

    /// <summary>
    /// Verifies a failure threshold of 1 is accepted by <see cref="CircuitBreakerAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_allow_failureThreshold_of_1()
    {
        // Arrange & Act
        var attr = new CircuitBreakerAttribute(failureThreshold: 1);

        // Assert
        Assert.Equal(1, attr.FailureThreshold);
    }

    /// <summary>
    /// Verifies a reset timeout of zero is accepted by <see cref="CircuitBreakerAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_allow_zero_resetTimeout()
    {
        // Arrange & Act
        var attr = new CircuitBreakerAttribute(resetTimeout: 0);

        // Assert
        Assert.Equal(0, attr.ResetTimeout);
    }
}
