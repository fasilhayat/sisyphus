namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Akka.Configuration;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Tests for ResilientProxy with circuit breaker attribute.
/// </summary>
public class ResilientProxyCircuitBreakerTests : ProxyTestBase
{
    private ActorSystem? _actorSystem;

    /// <summary>
    /// Creates a proxy with actor system for fan-out testing.
    /// </summary>
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

    /// <inheritdoc/>
    protected new void Dispose()
    {
        base.Dispose();
    }

    /// <summary>
    /// Test interface for proxy testing.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// Async method with circuit breaker attribute.
        /// </summary>
        [CircuitBreaker(5, 1000)]
        Task<string> GetDataAsync();
    }

    /// <summary>
    /// Test implementation for ITestService.
    /// </summary>
    private class TestService : ITestService
    {
        /// <summary>
        /// Gets the number of times GetDataAsync was called.
        /// </summary>
        public int CallCount { get; set; }

        /// <inheritdoc/>
        public Task<string> GetDataAsync()
        {
            CallCount++;
            if (CallCount < 3)
                throw new Exception($"Attempt {CallCount} failed");
            return Task.FromResult("success");
        }
    }
}
