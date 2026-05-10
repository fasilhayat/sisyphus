namespace Oasis.Resilience.Test.Unit.Proxies;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// End-to-end integration tests that wire <see cref="ResilientProxy{T}"/> through the full
/// <see cref="ResilienceRegistration"/> pipeline and exercise the proxy-level circuit breaker
/// and retry composition.
/// </summary>
public class ResilientProxyCircuitBreakerIntegrationTests
{
    /// <summary>
    /// Service contract used by the integration tests.
    /// </summary>
    public interface ICircuitProtectedService
    {
        /// <summary>Executes a circuit-breaker-only operation.</summary>
        Task<int> CircuitOnly();

        /// <summary>Executes a circuit-breaker + retry composed operation.</summary>
        Task<int> CircuitWithRetry();
    }

    /// <summary>
    /// Test service implementation whose call counters are inspected by the tests.
    /// </summary>
    public sealed class FlakyService : ICircuitProtectedService
    {
        /// <summary>Gets the number of times <see cref="CircuitOnly"/> has been invoked.</summary>
        public int CircuitOnlyCalls { get; private set; }

        /// <summary>Gets the number of times <see cref="CircuitWithRetry"/> has been invoked.</summary>
        public int CircuitWithRetryCalls { get; private set; }

        /// <summary>Always throws to drive the breaker open.</summary>
        [CircuitBreaker(failureThreshold: 2, resetTimeout: 30000, maxConcurrentCalls: 1)]
        public Task<int> CircuitOnly()
        {
            CircuitOnlyCalls++;
            throw new InvalidOperationException("boom");
        }

        /// <summary>Fails twice then succeeds; the retry policy must observe both failures.</summary>
        [CircuitBreaker(failureThreshold: 5, resetTimeout: 30000, maxConcurrentCalls: 1)]
        [Retry(maxAttempts: 3, initialDelay: 10)]
        public Task<int> CircuitWithRetry()
        {
            CircuitWithRetryCalls++;
            if (CircuitWithRetryCalls < 3) throw new InvalidOperationException("transient");
            return Task.FromResult(42);
        }
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddResilience(retry => retry.LogLevel = LogLevel.None);
        services.AddResilientService<ICircuitProtectedService, FlakyService>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Verifies that consecutive failures through the proxy open the circuit and subsequent calls
    /// fail with <see cref="Actors.CircuitBreakerActor.CircuitBreakerOpenException"/>.
    /// </summary>
    [Fact]
    public async Task Proxy_should_open_circuit_after_threshold_failures()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<ICircuitProtectedService>();
        var implementation = provider.GetRequiredService<FlakyService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CircuitOnly());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CircuitOnly());

        var ex = await Assert.ThrowsAsync<CircuitBreakerActor.CircuitBreakerOpenException>(
            () => service.CircuitOnly());
        Assert.Equal(2, implementation.CircuitOnlyCalls);
        Assert.Contains("Circuit breaker is open", ex.Message);
    }

    /// <summary>
    /// Verifies that a method decorated with both <see cref="CircuitBreakerAttribute"/> and
    /// <see cref="RetryAttribute"/> succeeds via the retry path while remaining under the breaker
    /// threshold.
    /// </summary>
    [Fact]
    public async Task Proxy_should_combine_circuit_breaker_with_retry()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<ICircuitProtectedService>();
        var implementation = provider.GetRequiredService<FlakyService>();

        var result = await service.CircuitWithRetry();

        Assert.Equal(42, result);
        Assert.Equal(3, implementation.CircuitWithRetryCalls);
    }
}
