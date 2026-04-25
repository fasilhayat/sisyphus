namespace Oasis.Resilience;

/// <summary>
/// Configuration options for resilience behavior.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether verbose logging is enabled for resilience operations.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}