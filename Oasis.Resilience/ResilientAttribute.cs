namespace Oasis.Resilience;

/// <summary>
/// Indicates that a method should be executed with automatic retry logic in the event of failure.
/// </summary>
/// <remarks>Apply this attribute to methods that require resilience against transient failures, such as network
/// or I/O operations. The attribute specifies the maximum number of retry attempts and the initial delay between
/// attempts. The actual retry behavior depends on the framework or infrastructure that processes this attribute;
/// applying the attribute alone does not implement retries.</remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ResilientAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of attempts allowed for the operation.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the initial delay, in seconds, before the operation starts.
    /// </summary>
    public int InitialDelaySeconds { get; }

    /// <summary>
    /// Initializes a new instance of the ResilientAttribute class with the specified maximum number of attempts and
    /// initial delay between attempts.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of retry attempts to perform. Must be greater than zero.</param>
    /// <param name="initialDelaySeconds">The initial delay, in seconds, to wait before the first retry attempt. Must be zero or greater.</param>
    public ResilientAttribute(int maxAttempts = 5, int initialDelaySeconds = 2)
    {
        MaxAttempts = maxAttempts;
        InitialDelaySeconds = initialDelaySeconds;
    }
}