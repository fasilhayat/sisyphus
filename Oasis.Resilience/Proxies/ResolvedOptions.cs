namespace Oasis.Resilience.Proxies;

using Attributes;

/// <summary>
/// Holds resolved retry parameters merged from the <see cref="RetryAttribute"/> and the global
/// <see cref="RetryOptions"/>.
/// </summary>
internal sealed class ResolvedRetry
{
    /// <summary>Gets the resolved maximum number of retry attempts.</summary>
    public required int MaxAttempts { get; init; }

    /// <summary>Gets the resolved initial delay in milliseconds.</summary>
    public required int InitialDelayMs { get; init; }

    /// <summary>Gets the resolved set of exception types eligible for retry, or <c>null</c> for all.</summary>
    public Type[]? RetryOn { get; init; }
}

/// <summary>
/// Holds resolved circuit breaker parameters merged from the <see cref="CircuitBreakerAttribute"/> and the
/// global <see cref="CircuitBreakerOptions"/>.
/// </summary>
internal sealed class ResolvedCircuitBreaker
{
    /// <summary>Gets the resolved failure threshold.</summary>
    public required int FailureThreshold { get; init; }

    /// <summary>Gets the resolved reset timeout in milliseconds.</summary>
    public required int ResetTimeoutMs { get; init; }

    /// <summary>Gets the resolved maximum concurrent calls in the half-open state.</summary>
    public required int MaxConcurrentCalls { get; init; }
}

/// <summary>
/// Holds resolved supervision parameters merged from the <see cref="SupervisionAttribute"/> and the
/// global <see cref="SupervisionOptions"/>.
/// </summary>
internal sealed class ResolvedSupervision
{
    /// <summary>Gets the resolved supervision strategy.</summary>
    public required SupervisionStrategy Strategy { get; init; }

    /// <summary>Gets the resolved maximum retries.</summary>
    public required int MaxRetries { get; init; }

    /// <summary>Gets the resolved minimum backoff in milliseconds.</summary>
    public required int BackoffMinMs { get; init; }

    /// <summary>Gets the resolved maximum backoff in milliseconds.</summary>
    public required int BackoffMaxMs { get; init; }

    /// <summary>Gets the resolved random factor for jitter.</summary>
    public required double RandomFactor { get; init; }
}
