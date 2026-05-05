namespace Oasis.Resilience.Test.Unit.Attributes;

using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RetryAttribute"/>.
/// </summary>
public class RetryAttributeTests
{
    [Fact]
    public void Should_have_default_values()
    {
        // Arrange
        var attr = new RetryAttribute();
        
        // Assert
        Assert.Equal(5, attr.MaxAttempts);
        Assert.Equal(2000, attr.InitialDelay);
    }

    [Fact]
    public void Should_accept_custom_values()
    {
        // Arrange
        var attr = new RetryAttribute(maxAttempts: 3, initialDelay: 500);
        
        // Assert
        Assert.Equal(3, attr.MaxAttempts);
        Assert.Equal(500, attr.InitialDelay);
    }

    [Fact]
    public void Should_throw_for_invalid_maxAttempts()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryAttribute(maxAttempts: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryAttribute(maxAttempts: -1));
    }

    [Fact]
    public void Should_throw_for_negative_initialDelay()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryAttribute(initialDelay: -1));
    }

    [Fact]
    public void Should_allow_maxAttempts_of_1()
    {
        // Arrange & Act
        var attr = new RetryAttribute(maxAttempts: 1);
        
        // Assert
        Assert.Equal(1, attr.MaxAttempts);
    }

    [Fact]
    public void Should_allow_zero_initialDelay()
    {
        // Arrange & Act
        var attr = new RetryAttribute(initialDelay: 0);
        
        // Assert
        Assert.Equal(0, attr.InitialDelay);
    }
}
