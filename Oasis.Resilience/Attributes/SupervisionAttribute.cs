namespace Oasis.Resilience.Attributes;

/// <summary>
/// Specifies that a method should be executed with Akka.NET supervision, allowing configuration of strategy and backoff parameters.
/// </summary>
/// <remarks>Apply to methods that require actor-based supervision with restart, stop, escalate, resume, or backoff strategies.</remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SupervisionAttribute : Attribute
{
    /// <summary>
    /// Gets the supervision strategy to use when an actor fails.
    /// </summary>
    public SupervisionStrategy Strategy { get; }

    /// <summary>
    /// Gets the maximum number of retries before giving up.
    /// </summary>
    public int MaxRetries { get; }

    /// <summary>
    /// Gets the minimum backoff duration in milliseconds.
    /// </summary>
    public int BackoffMinMs { get; }

    /// <summary>
    /// Gets the maximum backoff duration in milliseconds.
    /// </summary>
    public int BackoffMaxMs { get; }

    /// <summary>
    /// Gets the random factor to add jitter to backoff.
    /// </summary>
    public double RandomFactor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisionAttribute"/> class.
    /// Use <c>-1</c> for any numeric parameter to inherit the value from <see cref="SupervisionOptions"/>.
    /// </summary>
    /// <param name="strategy">The supervision strategy. Default is <see cref="SupervisionStrategy.RestartWithBackoff"/>.</param>
    /// <param name="maxRetries">The maximum number of retries. Use <c>-1</c> to use the configured default.</param>
    /// <param name="backoffMinMs">The minimum backoff in milliseconds. Use <c>-1</c> to use the configured default.</param>
    /// <param name="backoffMaxMs">The maximum backoff in milliseconds. Use <c>-1</c> to use the configured default.</param>
    /// <param name="randomFactor">The random factor for jitter. Use <c>-1</c> to use the configured default.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an explicitly supplied value is invalid.
    /// </exception>
    public SupervisionAttribute(
        SupervisionStrategy strategy = SupervisionStrategy.RestartWithBackoff,
        int maxRetries = AttributeDefaults.UnsetInt,
        int backoffMinMs = AttributeDefaults.UnsetInt,
        int backoffMaxMs = AttributeDefaults.UnsetInt,
        double randomFactor = AttributeDefaults.UnsetDouble)
    {
        if (maxRetries != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxRetries, 1);
        if (backoffMinMs != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfNegative(backoffMinMs);
        if (backoffMaxMs != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfNegative(backoffMaxMs);
        if (randomFactor != AttributeDefaults.UnsetDouble)
            ArgumentOutOfRangeException.ThrowIfNegative(randomFactor);

        Strategy = strategy;
        MaxRetries = maxRetries;
        BackoffMinMs = backoffMinMs;
        BackoffMaxMs = backoffMaxMs;
        RandomFactor = randomFactor;
    }
}
