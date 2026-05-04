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
    /// Tests that default values are set correctly.
    /// </summary>
    [Fact]
    public void Default_values_should_be_set_correctly()
    {
        // Arrange & Act
        var options = new SupervisionOptions();

        // Assert
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, options.DefaultStrategy);
        Assert.Equal(5, options.DefaultMaxRetries);
        Assert.Equal(2000, options.DefaultBackoffMinMs);
        Assert.Equal(30000, options.DefaultBackoffMaxMs);
        Assert.Equal(0.2, options.DefaultRandomFactor);
    }

    /// <summary>
    /// Tests that values can be overridden via property setters.
    /// </summary>
    [Fact]
    public void Values_can_be_overridden_via_properties()
    {
        // Arrange
        var options = new SupervisionOptions();

        // Act
        options.DefaultStrategy = SupervisionStrategy.Stop;
        options.DefaultMaxRetries = 3;
        options.DefaultBackoffMinMs = 1000;
        options.DefaultBackoffMaxMs = 15000;
        options.DefaultRandomFactor = 0.5;

        // Assert
        Assert.Equal(SupervisionStrategy.Stop, options.DefaultStrategy);
        Assert.Equal(3, options.DefaultMaxRetries);
        Assert.Equal(1000, options.DefaultBackoffMinMs);
        Assert.Equal(15000, options.DefaultBackoffMaxMs);
        Assert.Equal(0.5, options.DefaultRandomFactor);
    }
}
