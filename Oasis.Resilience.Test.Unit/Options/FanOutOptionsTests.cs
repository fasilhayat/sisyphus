namespace Oasis.Resilience.Test.Unit.Options;

using Oasis.Resilience;
using Xunit;

/// <summary>
/// Unit tests for <see cref="FanOutOptions"/>.
/// </summary>
public class FanOutOptionsTests
{
    /// <summary>
    /// Tests that default values are set correctly.
    /// </summary>
    [Fact]
    public void Default_values_should_be_set_correctly()
    {
        // Arrange & Act
        var options = new FanOutOptions();

        // Assert
        Assert.Equal(5, options.DefaultMaxWorkers);
    }

    /// <summary>
    /// Tests that values can be overridden via property setters.
    /// </summary>
    [Fact]
    public void Values_can_be_overridden_via_properties()
    {
        // Arrange
        var options = new FanOutOptions();

        // Act
        options.DefaultMaxWorkers = 10;

        // Assert
        Assert.Equal(10, options.DefaultMaxWorkers);
    }
}
