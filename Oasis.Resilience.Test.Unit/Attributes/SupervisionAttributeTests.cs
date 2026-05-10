namespace Oasis.Resilience.Test.Unit.Attributes;

using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SupervisionAttribute"/>.
/// </summary>
public class SupervisionAttributeTests
{
    /// <summary>
    /// Verifies the default property values of <see cref="SupervisionAttribute"/>.
    /// Strategy retains a real default; numerics are sentinel (unset).
    /// </summary>
    [Fact]
    public void Should_have_default_values()
    {
        // Arrange
        var attr = new SupervisionAttribute();

        // Assert
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, attr.Strategy);
        Assert.Equal(AttributeDefaults.UnsetInt, attr.MaxRetries);
        Assert.Equal(AttributeDefaults.UnsetInt, attr.BackoffMinMs);
        Assert.Equal(AttributeDefaults.UnsetInt, attr.BackoffMaxMs);
        Assert.Equal(AttributeDefaults.UnsetDouble, attr.RandomFactor);
    }

    /// <summary>
    /// Verifies custom values are properly accepted by <see cref="SupervisionAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_accept_custom_values()
    {
        // Arrange
        var attr = new SupervisionAttribute(
            strategy: SupervisionStrategy.Stop,
            maxRetries: 10,
            backoffMinMs: 1000,
            backoffMaxMs: 60000,
            randomFactor: 0.5);

        // Assert
        Assert.Equal(SupervisionStrategy.Stop, attr.Strategy);
        Assert.Equal(10, attr.MaxRetries);
        Assert.Equal(1000, attr.BackoffMinMs);
        Assert.Equal(60000, attr.BackoffMaxMs);
        Assert.Equal(0.5, attr.RandomFactor);
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for invalid max retries values.
    /// </summary>
    [Fact]
    public void Should_throw_for_invalid_maxRetries()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(maxRetries: 0));
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for a negative backoff minimum
    /// (the sentinel <c>-1</c> is permitted, but <c>-2</c> is not).
    /// </summary>
    [Fact]
    public void Should_throw_for_negative_backoffMinMs()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(backoffMinMs: -2));
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for a negative backoff maximum
    /// (the sentinel <c>-1</c> is permitted, but <c>-2</c> is not).
    /// </summary>
    [Fact]
    public void Should_throw_for_negative_backoffMaxMs()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(backoffMaxMs: -2));
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for a negative random factor
    /// (the sentinel <c>-1.0</c> is permitted, but anything else negative is not).
    /// </summary>
    [Fact]
    public void Should_throw_for_negative_randomFactor()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(randomFactor: -0.5));
    }

    /// <summary>
    /// Verifies all supervision strategies are accepted by <see cref="SupervisionAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_allow_all_strategies()
    {
        // Arrange & Act & Assert
        var restart = new SupervisionAttribute(strategy: SupervisionStrategy.Restart);
        Assert.Equal(SupervisionStrategy.Restart, restart.Strategy);

        var stop = new SupervisionAttribute(strategy: SupervisionStrategy.Stop);
        Assert.Equal(SupervisionStrategy.Stop, stop.Strategy);

        var escalate = new SupervisionAttribute(strategy: SupervisionStrategy.Escalate);
        Assert.Equal(SupervisionStrategy.Escalate, escalate.Strategy);

        var resume = new SupervisionAttribute(strategy: SupervisionStrategy.Resume);
        Assert.Equal(SupervisionStrategy.Resume, resume.Strategy);

        var backoff = new SupervisionAttribute(strategy: SupervisionStrategy.RestartWithBackoff);
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, backoff.Strategy);
    }
}
