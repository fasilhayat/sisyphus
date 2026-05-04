namespace Oasis.Resilience;

/// <summary>
/// Configuration options for fan-out behavior when using the <see cref="Attributes.FanOutAttribute"/>.
/// </summary>
/// <remarks>These options provide global defaults that can be overridden per-method using the attribute.</remarks>
public class FanOutOptions
{
    /// <summary>
    /// Gets or sets the default maximum number of worker actors.
    /// </summary>
    public int DefaultMaxWorkers { get; set; } = 5;
}
