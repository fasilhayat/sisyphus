namespace Oasis.Resilience.Proxies;

using Attributes;

/// <summary>
/// Merges per-method resilience attributes with the configured global options to produce a fully
/// resolved set of parameters for the proxy. The use of <c>-1</c> sentinels (see
/// <see cref="AttributeDefaults"/>) means callers can write <c>[Retry(maxAttempts: 5)]</c> and have
/// only the unspecified parameters fall back to the configured defaults.
/// </summary>
internal static class OptionsResolver
{
    /// <summary>
    /// Resolves the effective retry parameters for a method.
    /// </summary>
    /// <param name="attribute">The retry attribute (may be <c>null</c>).</param>
    /// <param name="options">The configured global retry options (may be <c>null</c>).</param>
    /// <returns>The resolved retry parameters.</returns>
    public static ResolvedRetry ResolveRetry(RetryAttribute? attribute, RetryOptions? options)
    {
        var defaultMaxAttempts = options?.DefaultMaxAttempts ?? 5;
        var defaultDelay = options?.DefaultInitialDelayMs ?? 2000;
        var defaultRetryOn = options?.DefaultRetryOnExceptions;

        return new ResolvedRetry
        {
            MaxAttempts = ResolveInt(attribute?.MaxAttempts, defaultMaxAttempts),
            InitialDelayMs = ResolveInt(attribute?.InitialDelay, defaultDelay),
            RetryOn = attribute?.RetryOn ?? defaultRetryOn
        };
    }

    /// <summary>
    /// Resolves the effective circuit breaker parameters for a method.
    /// </summary>
    /// <param name="attribute">The circuit breaker attribute.</param>
    /// <param name="options">The configured global circuit breaker options (may be <c>null</c>).</param>
    /// <returns>The resolved circuit breaker parameters.</returns>
    public static ResolvedCircuitBreaker ResolveCircuitBreaker(CircuitBreakerAttribute attribute, CircuitBreakerOptions? options)
    {
        return new ResolvedCircuitBreaker
        {
            FailureThreshold = ResolveInt(attribute.FailureThreshold, options?.DefaultFailureThreshold ?? 5),
            ResetTimeoutMs = ResolveInt(attribute.ResetTimeout, options?.DefaultResetTimeout ?? 30000),
            MaxConcurrentCalls = ResolveInt(attribute.MaxConcurrentCalls, options?.DefaultMaxConcurrentCalls ?? 1)
        };
    }

    /// <summary>
    /// Resolves the effective supervision parameters for a method or worker.
    /// </summary>
    /// <param name="attribute">The supervision attribute (may be <c>null</c>).</param>
    /// <param name="options">The configured global supervision options (may be <c>null</c>).</param>
    /// <returns>The resolved supervision parameters.</returns>
    public static ResolvedSupervision ResolveSupervision(SupervisionAttribute? attribute, SupervisionOptions? options)
    {
        var strategy = attribute?.Strategy ?? options?.DefaultStrategy ?? SupervisionStrategy.RestartWithBackoff;

        return new ResolvedSupervision
        {
            Strategy = strategy,
            MaxRetries = ResolveInt(attribute?.MaxRetries, options?.DefaultMaxRetries ?? 5),
            BackoffMinMs = ResolveInt(attribute?.BackoffMinMs, options?.DefaultBackoffMinMs ?? 2000),
            BackoffMaxMs = ResolveInt(attribute?.BackoffMaxMs, options?.DefaultBackoffMaxMs ?? 30000),
            RandomFactor = ResolveDouble(attribute?.RandomFactor, options?.DefaultRandomFactor ?? 0.2)
        };
    }

    /// <summary>
    /// Resolves the effective fan-out worker count.
    /// </summary>
    /// <param name="attribute">The fan-out attribute.</param>
    /// <param name="options">The configured global fan-out options (may be <c>null</c>).</param>
    /// <returns>The maximum number of workers to use.</returns>
    public static int ResolveMaxWorkers(FanOutAttribute attribute, FanOutOptions? options)
    {
        return ResolveInt(attribute.MaxWorkers, options?.DefaultMaxWorkers ?? 5);
    }

    /// <summary>Returns <paramref name="fallback"/> when <paramref name="value"/> is unset or <c>null</c>.</summary>
    private static int ResolveInt(int? value, int fallback)
    {
        if (value is null || value.Value == AttributeDefaults.UnsetInt)
            return fallback;
        return value.Value;
    }

    /// <summary>Returns <paramref name="fallback"/> when <paramref name="value"/> is unset or <c>null</c>.</summary>
    private static double ResolveDouble(double? value, double fallback)
    {
        if (value is null || value.Value == AttributeDefaults.UnsetDouble)
            return fallback;
        return value.Value;
    }
}
