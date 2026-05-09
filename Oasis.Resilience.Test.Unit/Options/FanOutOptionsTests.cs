namespace Oasis.Resilience.Test.Unit.Options;

using Oasis.Resilience;
using Xunit;

/// <summary>
/// Unit tests for <see cref="FanOutOptions"/>.
/// </summary>
public class FanOutOptionsTests
{
    /// <summary>
    /// Verifies the default <see cref="FanOutOptions.DefaultMaxWorkers"/> is 5.
    /// </summary>
    [Fact]
    public void DefaultMaxWorkers_should_be_5()
    {
        // Arrange
        var options = new FanOutOptions();

        // Assert
        Assert.Equal(5, options.DefaultMaxWorkers);
    }

    /// <summary>
    /// Verifies <see cref="FanOutOptions.DefaultMaxWorkers"/> can be set to a custom value.
    /// </summary>
    [Fact]
    public void Should_allow_setting_DefaultMaxWorkers()
    {
        // Arrange
        var options = new FanOutOptions();

        // Act
        options.DefaultMaxWorkers = 10;

        // Assert
        Assert.Equal(10, options.DefaultMaxWorkers);
    }

    /// <summary>
    /// Verifies <see cref="FanOutOptions.DefaultMaxWorkers"/> can be set to 1.
    /// </summary>
    [Fact]
    public void Should_allow_setting_MaxWorkers_to_1()
    {
        // Arrange
        var options = new FanOutOptions();

        // Act
        options.DefaultMaxWorkers = 1;

        // Assert
        Assert.Equal(1, options.DefaultMaxWorkers);
    }

    /// <summary>
    /// Verifies <see cref="FanOutOptions.DefaultMaxWorkers"/> can be set to 100.
    /// </summary>
    [Fact]
    public void Should_allow_setting_MaxWorkers_to_100()
    {
        // Arrange
        var options = new FanOutOptions();

        // Act
        options.DefaultMaxWorkers = 100;

        // Assert
        Assert.Equal(100, options.DefaultMaxWorkers);
    }
}
