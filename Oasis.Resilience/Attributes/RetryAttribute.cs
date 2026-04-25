namespace Oasis.Resilience.Attributes;

/// <summary>
/// Specifies that a method should be executed with retry logic, allowing configuration of maximum attempts and initial
/// delay between retries.
/// </summary>
/// <remarks>Apply to methods that require resilience against transient failures, such as network or I/O
/// operations.</remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RetryAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of allowed attempts.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the initial delay, in milliseconds, before starting the operation.
    /// </summary>
    public int InitialDelay { get; }

    /// <summary>
    /// Initializes a new instance of the RetryAttribute class.
    /// Default values are 5 retry attempts and an initial delay of 2 seconds.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of retry attempts. Default is 5.</param>
    /// <param name="initialDelay">The initial delay between attempts, in milliseconds. Default is 2000.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when maxAttempts is less than 1 or initialDelay is negative.
    /// </exception>
    public RetryAttribute(int maxAttempts = 5, int initialDelay = 2000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(initialDelay);

        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay;
    }
}