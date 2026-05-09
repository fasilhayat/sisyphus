namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;

/// <summary>
/// Unit tests for circuit breaker behavior in <see cref="ResilientProxy{T}"/>.
/// </summary>
public class ResilientProxyCircuitBreakerTests : ProxyTestBase
{
    /// <summary>
    /// The actor system used by the proxy.
    /// </summary>
    private ActorSystem? _actorSystem;

    /// <summary>
    /// Creates a proxy with a fan-out service and actor system configured.
    /// </summary>
    /// <returns>The created proxy instance.</returns>
    private object CreateFanOutProxy()
    {
        _actorSystem = CreateActorSystem();

        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = new TestService();
        resilientProxy.FanOutOptions = new FanOutOptions();

        return proxy!;
    }

    /// <summary>
    /// Test service interface for circuit breaker proxy tests.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// An async method decorated with <see cref="CircuitBreakerAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [CircuitBreaker(5, 1000)]
        Task<string> GetDataAsync();
    }

    /// <summary>
    /// Test implementation of <see cref="ITestService"/>.
    /// </summary>
    private class TestService : ITestService
    {
        /// <summary>
        /// Gets or sets the number of times methods have been called.
        /// </summary>
        public int CallCount { get; set; }

        /// <summary>
        /// Throws on the first two calls and succeeds on the third.
        /// </summary>
        /// <returns>A task that yields a success string.</returns>
        public Task<string> GetDataAsync()
        {
            CallCount++;
            if (CallCount < 3)
                throw new Exception($"Attempt {CallCount} failed");
            return Task.FromResult("success");
        }
    }
}
