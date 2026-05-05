namespace Oasis.Resilience.Test.Unit.Options;

using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RetryOptions"/>.
/// </summary>
public class RetryOptionsTests
{
    [Fact]
    public void DefaultLogLevel_should_be_Debug()
    {
        // Arrange
        var options = new RetryOptions();

        // Assert
        Assert.Equal(LogLevel.Debug, options.LogLevel);
    }

    [Fact]
    public void Should_allow_setting_LogLevel()
    {
        // Arrange
        var options = new RetryOptions();

        // Act
        options.LogLevel = LogLevel.Information;

        // Assert
        Assert.Equal(LogLevel.Information, options.LogLevel);
    }

    [Fact]
    public void Should_allow_setting_LogLevel_to_Warning()
    {
        // Arrange
        var options = new RetryOptions();

        // Act
        options.LogLevel = LogLevel.Warning;

        // Assert
        Assert.Equal(LogLevel.Warning, options.LogLevel);
    }

    [Fact]
    public void Should_allow_setting_LogLevel_to_None()
    {
        // Arrange
        var options = new RetryOptions();

        // Act
        options.LogLevel = LogLevel.None;

        // Assert
        Assert.Equal(LogLevel.None, options.LogLevel);
    }
}
