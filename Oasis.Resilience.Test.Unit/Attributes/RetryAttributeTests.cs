namespace Oasis.Resilience.Test.Unit.Attributes;

using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RetryAttribute"/>.
/// </summary>
public class RetryAttributeTests
{
    /// <summary>
    /// Verifies the default property values of <see cref="RetryAttribute"/> are sentinel (unset).
    /// </summary>
    [Fact]
    public void Should_have_default_values()
    {
        // Arrange
        var attr = new RetryAttribute();

        // Assert
        Assert.Equal(AttributeDefaults.UnsetInt, attr.MaxAttempts);
        Assert.Equal(AttributeDefaults.UnsetInt, attr.InitialDelay);
        Assert.Null(attr.RetryOn);
    }

    /// <summary>
    /// Verifies custom values are properly accepted by <see cref="RetryAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_accept_custom_values()
    {
        // Arrange
        var attr = new RetryAttribute(maxAttempts: 3, initialDelay: 500);

        // Assert
        Assert.Equal(3, attr.MaxAttempts);
        Assert.Equal(500, attr.InitialDelay);
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for invalid max attempts values
    /// (the sentinel <c>-1</c> is permitted, but <c>0</c> is not).
    /// </summary>
    [Fact]
    public void Should_throw_for_invalid_maxAttempts()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryAttribute(maxAttempts: 0));
    }

    /// <summary>
    /// Verifies an <see cref="ArgumentOutOfRangeException"/> is thrown for a negative initial delay
    /// (the sentinel <c>-1</c> is permitted, but <c>-2</c> is not).
    /// </summary>
    [Fact]
    public void Should_throw_for_negative_initialDelay()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryAttribute(initialDelay: -2));
    }

    /// <summary>
    /// Verifies a max attempts value of 1 is accepted by <see cref="RetryAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_allow_maxAttempts_of_1()
    {
        // Arrange & Act
        var attr = new RetryAttribute(maxAttempts: 1);

        // Assert
        Assert.Equal(1, attr.MaxAttempts);
    }

    /// <summary>
    /// Verifies an initial delay of zero is accepted by <see cref="RetryAttribute"/>.
    /// </summary>
    [Fact]
    public void Should_allow_zero_initialDelay()
    {
        // Arrange & Act
        var attr = new RetryAttribute(initialDelay: 0);

        // Assert
        Assert.Equal(0, attr.InitialDelay);
    }
}
