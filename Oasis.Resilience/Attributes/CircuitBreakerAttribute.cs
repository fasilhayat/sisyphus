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
    /// Initializes a new instance of the <see cref="CircuitBreakerAttribute"/> class.
    /// Use <c>-1</c> for any parameter to inherit the value from <see cref="CircuitBreakerOptions"/>.
    /// </summary>
    /// <param name="failureThreshold">The number of failures before opening the circuit. Use <c>-1</c> to use the configured default.</param>
    /// <param name="resetTimeout">The reset timeout in milliseconds. Use <c>-1</c> to use the configured default.</param>
    /// <param name="maxConcurrentCalls">The max concurrent calls in half-open state. Use <c>-1</c> to use the configured default.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an explicitly supplied value is invalid.
    /// </exception>
    public CircuitBreakerAttribute(
        int failureThreshold = AttributeDefaults.UnsetInt,
        int resetTimeout = AttributeDefaults.UnsetInt,
        int maxConcurrentCalls = AttributeDefaults.UnsetInt)
    {
        if (failureThreshold != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfLessThan(failureThreshold, 1);
        if (resetTimeout != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfNegative(resetTimeout);
        if (maxConcurrentCalls != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentCalls, 1);

        FailureThreshold = failureThreshold;
        ResetTimeout = resetTimeout;
        MaxConcurrentCalls = maxConcurrentCalls;
    }
}
