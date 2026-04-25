namespace Oasis.Resilience;

using Microsoft.Extensions.Logging;

/// <summary>
/// Configuration options for resilience behavior.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Gets or sets the log level for resilience operations.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Debug;
}