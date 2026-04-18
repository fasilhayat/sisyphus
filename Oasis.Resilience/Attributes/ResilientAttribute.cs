namespace Oasis.Resilience.Attributes;

/// <summary>
/// Specifies that a method should be executed with retry logic, allowing configuration of maximum attempts and initial
/// delay between retries.
/// </summary>
/// <remarks>Apply to methods that require resilience against transient failures, such as network or I/O
/// operations.</remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ResilientAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of allowed attempts.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the initial delay, in seconds, before starting the operation.
    /// </summary>
    public int InitialDelaySeconds { get; }

    /// <summary>
    /// Initializes a new instance of the ResilientAttribute class with the specified maximum number of attempts and
    /// initial delay in seconds.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of retry attempts. Must be greater than 0.</param>
    /// <param name="initialDelaySeconds">The initial delay between attempts, in seconds. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxAttempts is less than 1 or initialDelaySeconds is negative.</exception>
    public ResilientAttribute(int maxAttempts = 5, int initialDelaySeconds = 2)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        if (initialDelaySeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(initialDelaySeconds));

        MaxAttempts = maxAttempts;
        InitialDelaySeconds = initialDelaySeconds;
    }
}