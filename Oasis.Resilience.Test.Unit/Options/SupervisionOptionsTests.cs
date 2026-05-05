namespace Oasis.Resilience.Test.Unit.Options;

using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SupervisionOptions"/>.
/// </summary>
public class SupervisionOptionsTests
{
    [Fact]
    public void DefaultStrategy_should_be_RestartWithBackoff()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, options.DefaultStrategy);
    }

    [Fact]
    public void DefaultMaxRetries_should_be_5()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(5, options.DefaultMaxRetries);
    }

    [Fact]
    public void DefaultBackoffMinMs_should_be_2000()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(2000, options.DefaultBackoffMinMs);
    }

    [Fact]
    public void DefaultBackoffMaxMs_should_be_30000()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(30000, options.DefaultBackoffMaxMs);
    }

    [Fact]
    public void DefaultRandomFactor_should_be_0_2()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(0.2, options.DefaultRandomFactor);
    }

    [Fact]
    public void Should_allow_setting_DefaultStrategy()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Act
        options.DefaultStrategy = SupervisionStrategy.Restart;

        // Assert
        Assert.Equal(SupervisionStrategy.Restart, options.DefaultStrategy);
    }

    [Fact]
    public void Should_allow_setting_all_properties()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Act
        options.DefaultStrategy = SupervisionStrategy.Stop;
        options.DefaultMaxRetries = 10;
        options.DefaultBackoffMinMs = 1000;
        options.DefaultBackoffMaxMs = 60000;
        options.DefaultRandomFactor = 0.5;

        // Assert
        Assert.Equal(SupervisionStrategy.Stop, options.DefaultStrategy);
        Assert.Equal(10, options.DefaultMaxRetries);
        Assert.Equal(1000, options.DefaultBackoffMinMs);
        Assert.Equal(60000, options.DefaultBackoffMaxMs);
        Assert.Equal(0.5, options.DefaultRandomFactor);
    }
}
