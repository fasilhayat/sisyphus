namespace Oasis.Resilience.Test.Unit.Attributes;

using System.Reflection;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SupervisionAttribute"/>.
/// </summary>
public class SupervisionAttributeTests
{
    /// <summary>
    /// Tests that default constructor values are applied correctly.
    /// </summary>
    [Fact]
    public void Default_constructor_should_set_default_values()
    {
        // Arrange & Act
        var attribute = new SupervisionAttribute();

        // Assert
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, attribute.Strategy);
        Assert.Equal(5, attribute.MaxRetries);
        Assert.Equal(2000, attribute.BackoffMinMs);
        Assert.Equal(30000, attribute.BackoffMaxMs);
        Assert.Equal(0.2, attribute.RandomFactor);
    }

    /// <summary>
    /// Tests that custom constructor values override defaults.
    /// </summary>
    [Fact]
    public void Custom_constructor_values_should_override_defaults()
    {
        // Arrange & Act
        var attribute = new SupervisionAttribute(
            strategy: SupervisionStrategy.Stop,
            maxRetries: 3,
            backoffMinMs: 1000,
            backoffMaxMs: 15000,
            randomFactor: 0.5);

        // Assert
        Assert.Equal(SupervisionStrategy.Stop, attribute.Strategy);
        Assert.Equal(3, attribute.MaxRetries);
        Assert.Equal(1000, attribute.BackoffMinMs);
        Assert.Equal(15000, attribute.BackoffMaxMs);
        Assert.Equal(0.5, attribute.RandomFactor);
    }

    /// <summary>
    /// Tests that constructor throws when maxRetries is less than 1.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_should_throw_when_maxRetries_less_than_one(int invalidMaxRetries)
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(maxRetries: invalidMaxRetries));
        Assert.Equal("maxRetries", ex.ParamName);
    }

    /// <summary>
    /// Tests that constructor throws when backoffMinMs is negative.
    /// </summary>
    [Fact]
    public void Constructor_should_throw_when_backoffMinMs_is_negative()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(backoffMinMs: -1));
        Assert.Equal("backoffMinMs", ex.ParamName);
    }

    /// <summary>
    /// Tests that constructor throws when backoffMaxMs is negative.
    /// </summary>
    [Fact]
    public void Constructor_should_throw_when_backoffMaxMs_is_negative()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(backoffMaxMs: -1));
        Assert.Equal("backoffMaxMs", ex.ParamName);
    }

    /// <summary>
    /// Tests that constructor throws when randomFactor is negative.
    /// </summary>
    [Fact]
    public void Constructor_should_throw_when_randomFactor_is_negative()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SupervisionAttribute(randomFactor: -0.1));
        Assert.Equal("randomFactor", ex.ParamName);
    }

    /// <summary>
    /// Tests that attribute can be retrieved from method using reflection.
    /// </summary>
    [Fact]
    public void Attribute_should_be_retrievable_via_reflection()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.SupervisedMethod));

        // Act
        var attribute = method?.GetCustomAttribute<SupervisionAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(SupervisionStrategy.RestartWithBackoff, attribute!.Strategy);
    }

    /// <summary>
    /// Test service class for reflection tests.
    /// </summary>
    private class TestService
    {
        [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
        public void SupervisedMethod() { }
    }
}
