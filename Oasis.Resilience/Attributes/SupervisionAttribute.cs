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
    /// Initializes a new instance of the SupervisionAttribute class.
    /// </summary>
    /// <param name="strategy">The supervision strategy. Default is RestartWithBackoff.</param>
    /// <param name="maxRetries">The maximum number of retries. Default is 5.</param>
    /// <param name="backoffMinMs">The minimum backoff in milliseconds. Default is 2000.</param>
    /// <param name="backoffMaxMs">The maximum backoff in milliseconds. Default is 30000.</param>
    /// <param name="randomFactor">The random factor for jitter. Default is 0.2.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when maxRetries is less than 1, backoffMinMs is negative, backoffMaxMs is negative, or randomFactor is negative.
    /// </exception>
    public SupervisionAttribute(
        SupervisionStrategy strategy = SupervisionStrategy.RestartWithBackoff,
        int maxRetries = 5,
        int backoffMinMs = 2000,
        int backoffMaxMs = 30000,
        double randomFactor = 0.2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRetries, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(backoffMinMs);
        ArgumentOutOfRangeException.ThrowIfNegative(backoffMaxMs);
        ArgumentOutOfRangeException.ThrowIfNegative(randomFactor);

        Strategy = strategy;
        MaxRetries = maxRetries;
        BackoffMinMs = backoffMinMs;
        BackoffMaxMs = backoffMaxMs;
        RandomFactor = randomFactor;
    }
}
