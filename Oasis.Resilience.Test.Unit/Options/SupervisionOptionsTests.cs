namespace Oasis.Resilience.Test.Unit.Options;

using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SupervisionOptions"/>.
/// </summary>
public class SupervisionOptionsTests
{
    /// <summary>
    /// Verifies the default <see cref="SupervisionOptions.DefaultStrategy"/> is <see cref="SupervisionStrategy.RestartWithBackoff"/>.
    /// </summary>
    [Fact]
    public void DefaultStrategy_should_be_RestartWithBackoff()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, options.DefaultStrategy);
    }

    /// <summary>
    /// Verifies the default <see cref="SupervisionOptions.DefaultMaxRetries"/> is 5.
    /// </summary>
    [Fact]
    public void DefaultMaxRetries_should_be_5()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(5, options.DefaultMaxRetries);
    }

    /// <summary>
    /// Verifies the default <see cref="SupervisionOptions.DefaultBackoffMinMs"/> is 2000.
    /// </summary>
    [Fact]
    public void DefaultBackoffMinMs_should_be_2000()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(2000, options.DefaultBackoffMinMs);
    }

    /// <summary>
    /// Verifies the default <see cref="SupervisionOptions.DefaultBackoffMaxMs"/> is 30000.
    /// </summary>
    [Fact]
    public void DefaultBackoffMaxMs_should_be_30000()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(30000, options.DefaultBackoffMaxMs);
    }

    /// <summary>
    /// Verifies the default <see cref="SupervisionOptions.DefaultRandomFactor"/> is 0.2.
    /// </summary>
    [Fact]
    public void DefaultRandomFactor_should_be_0_2()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(0.2, options.DefaultRandomFactor);
    }

    /// <summary>
    /// Verifies <see cref="SupervisionOptions.DefaultStrategy"/> can be set to a custom value.
    /// </summary>
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

    /// <summary>
    /// Verifies all properties of <see cref="SupervisionOptions"/> can be set simultaneously.
    /// </summary>
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
