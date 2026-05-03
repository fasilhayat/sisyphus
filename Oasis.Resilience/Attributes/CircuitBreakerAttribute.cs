namespace Oasis.Resilience.Attributes;

/// <summary>
/// Specifies that a method should be protected by a circuit breaker, preventing calls when a failure threshold is exceeded.
/// </summary>
/// <remarks>Apply to methods that call external services or resources prone to cascading failures.</remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CircuitBreakerAttribute : Attribute
{
    /// <summary>
    /// Gets the number of consecutive failures before the circuit opens.
    /// </summary>
    public int FailureThreshold { get; }

    /// <summary>
    /// Gets the duration, in milliseconds, the circuit remains open before transitioning to half-open.
    /// </summary>
    public int ResetTimeout { get; }

    /// <summary>
    /// Gets the maximum number of calls allowed in half-open state to test recovery.
    /// </summary>
    public int MaxConcurrentCalls { get; }

    /// <summary>
    /// Initializes a new instance of the CircuitBreakerAttribute class.
    /// </summary>
    /// <param name="failureThreshold">The number of failures before opening the circuit. Default is 5.</param>
    /// <param name="resetTimeout">The reset timeout in milliseconds. Default is 30000.</param>
    /// <param name="maxConcurrentCalls">The max concurrent calls in half-open state. Default is 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when failureThreshold is less than 1, resetTimeout is negative, or maxConcurrentCalls is less than 1.
    /// </exception>
    public CircuitBreakerAttribute(int failureThreshold = 5, int resetTimeout = 30000, int maxConcurrentCalls = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureThreshold, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(resetTimeout);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentCalls, 1);

        FailureThreshold = failureThreshold;
        ResetTimeout = resetTimeout;
        MaxConcurrentCalls = maxConcurrentCalls;
    }
}
