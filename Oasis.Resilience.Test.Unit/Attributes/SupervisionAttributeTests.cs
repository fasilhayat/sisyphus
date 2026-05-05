namespace Oasis.Resilience.Test.Unit.Attributes;

using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SupervisionAttribute"/>.
/// </summary>
public class SupervisionAttributeTests
{
    [Fact]
    public void Should_have_default_values()
    {
        // Arrange
        var attr = new SupervisionAttribute();

        // Assert
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, attr.Strategy);
        Assert.Equal(5, attr.MaxRetries);
        Assert.Equal(2000, attr.BackoffMinMs);
        Assert.Equal(30000, attr.BackoffMaxMs);
        Assert.Equal(0.2, attr.RandomFactor);
    }

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

    [Fact]
    public void Should_throw_for_invalid_maxRetries()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(maxRetries: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(maxRetries: -1));
    }

    [Fact]
    public void Should_throw_for_negative_backoffMinMs()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(backoffMinMs: -1));
    }

    [Fact]
    public void Should_throw_for_negative_backoffMaxMs()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(backoffMaxMs: -1));
    }

    [Fact]
    public void Should_throw_for_negative_randomFactor()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(randomFactor: -0.1));
    }

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
