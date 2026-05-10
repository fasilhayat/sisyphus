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
    /// Gets the maximum number of allowed attempts, or <see cref="AttributeDefaults.UnsetInt"/>
    /// to indicate the value should be resolved from <see cref="RetryOptions.DefaultMaxAttempts"/>.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the initial delay (in milliseconds) before retrying, or <see cref="AttributeDefaults.UnsetInt"/>
    /// to indicate the value should be resolved from <see cref="RetryOptions.DefaultInitialDelayMs"/>.
    /// </summary>
    public int InitialDelay { get; }

    /// <summary>
    /// Gets the optional set of exception types that should trigger a retry. When <c>null</c>,
    /// the value falls back to <see cref="RetryOptions.DefaultRetryOnExceptions"/>; when both are
    /// <c>null</c>, every exception is retried.
    /// </summary>
    public Type[]? RetryOn { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAttribute"/> class.
    /// </summary>
    /// <param name="maxAttempts">
    /// The maximum number of retry attempts. Use <c>-1</c> (the default) to resolve from <see cref="RetryOptions"/>.
    /// </param>
    /// <param name="initialDelay">
    /// The initial delay between attempts, in milliseconds. Use <c>-1</c> (the default) to resolve from <see cref="RetryOptions"/>.
    /// </param>
    /// <param name="retryOn">
    /// Optional set of exception types that should trigger a retry. When <c>null</c> (default), the value
    /// falls back to the configured global default.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an explicitly supplied <paramref name="maxAttempts"/> is less than 1, or when
    /// <paramref name="initialDelay"/> is explicitly negative (other than <c>-1</c>).
    /// </exception>
    public RetryAttribute(
        int maxAttempts = AttributeDefaults.UnsetInt,
        int initialDelay = AttributeDefaults.UnsetInt,
        Type[]? retryOn = null)
    {
        if (maxAttempts != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        if (initialDelay != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfNegative(initialDelay);

        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay;
        RetryOn = retryOn;
    }
}