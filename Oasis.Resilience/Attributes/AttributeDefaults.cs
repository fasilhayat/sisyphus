namespace Oasis.Resilience.Attributes;

/// <summary>
/// Sentinel constants used by resilience attributes to indicate that a value was not explicitly supplied
/// and should be resolved from the configured global options at runtime.
/// </summary>
public static class AttributeDefaults
{
    /// <summary>
    /// Sentinel value indicating an integer attribute parameter was not explicitly set.
    /// </summary>
    public const int UnsetInt = -1;

    /// <summary>
    /// Sentinel value indicating a double attribute parameter was not explicitly set.
    /// </summary>
    public const double UnsetDouble = -1.0;
}
