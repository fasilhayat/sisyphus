namespace Oasis.Resilience;

/// <summary>
/// Configuration options for supervision behavior when using the <see cref="Attributes.SupervisionAttribute"/>.
/// </summary>
/// <remarks>These options provide global defaults that can be overridden per-method using the attribute.</remarks>
public class SupervisionOptions
{
    /// <summary>
    /// Gets or sets the default supervision strategy.
    /// </summary>
    public Attributes.SupervisionStrategy DefaultStrategy { get; set; } = Attributes.SupervisionStrategy.RestartWithBackoff;

    /// <summary>
    /// Gets or sets the default maximum number of retries.
    /// </summary>
    public int DefaultMaxRetries { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default minimum backoff duration in milliseconds.
    /// </summary>
    public int DefaultBackoffMinMs { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the default maximum backoff duration in milliseconds.
    /// </summary>
    public int DefaultBackoffMaxMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the default random factor for jitter.
    /// </summary>
    public double DefaultRandomFactor { get; set; } = 0.2;
}
