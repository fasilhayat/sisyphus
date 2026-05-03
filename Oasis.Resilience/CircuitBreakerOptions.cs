namespace Oasis.Resilience;

/// <summary>
/// Configuration options for circuit breaker behavior.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the default failure threshold before the circuit opens.
    /// </summary>
    public int DefaultFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default reset timeout in milliseconds.
    /// </summary>
    public int DefaultResetTimeout { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the default maximum concurrent calls in half-open state.
    /// </summary>
    public int DefaultMaxConcurrentCalls { get; set; } = 1;
}
