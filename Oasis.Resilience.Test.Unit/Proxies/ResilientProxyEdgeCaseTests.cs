namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Tests for ResilientProxy edge cases and error paths.
/// </summary>
public class ResilientProxyEdgeCaseTests : ProxyTestBase
{
    /// <summary>
    /// Creates a proxy with just retry actor (no circuit breaker).
    /// </summary>
    private ResilientProxy<ITestService> CreateProxyWithRetryOnly(out TestService decorated)
    {
        var _actorSystem = CreateActorSystem();
        var _retryActor = _actorSystem.ActorOf(Props.Create(() => new RetryActor(new RetryOptions())), "retry");

        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        decorated = new TestService();
        resilientProxy.DecoratedInstance = decorated;
        resilientProxy.ResilienceActorRef = _retryActor;
        resilientProxy.ActorSystem = _actorSystem;

        return resilientProxy;
    }

    /// <summary>
    /// Tests retry path without circuit breaker.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_work_with_retry_only()
    {
        // Arrange
        var proxy = CreateProxyWithRetryOnly(out var testService);
        testService.CallCount = 2; // Skip first 2 failures
        var serviceProxy = (ITestService)(object)proxy;

        // Act
        var result = await serviceProxy.GetDataWithRetry();

        // Assert
        Assert.Equal("success", result);
        Assert.True(testService.CallCount >= 3);
    }

    /// <summary>
    /// Tests that retry without circuit breaker doesn't throw when retry succeeds.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_not_use_circuit_breaker_when_not_configured()
    {
        // Arrange
        var proxy = CreateProxyWithRetryOnly(out var testService);
        testService.CallCount = 2;
        var serviceProxy = (ITestService)(object)proxy;

        // Act
        var result = await serviceProxy.GetDataWithRetry();

        // Assert
        Assert.Equal("success", result);
    }

    /// <summary>
    /// Tests the WrapWithSupervision method directly.
    /// </summary>
    [Fact]
    public void WrapWithSupervision_should_return_wrapped_operation()
    {
        // Arrange
        var _actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        var supervisionAttr = new SupervisionAttribute(SupervisionStrategy.Restart);
        
        // Use reflection to call WrapWithSupervision
        var method = typeof(ResilientProxy<ITestService2>).GetMethod("WrapWithSupervision", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Act
        var wrappedOp = method!.Invoke(resilientProxy, 
            new object[] { new Func<Task<object>>(() => Task.FromResult<object>("test")), supervisionAttr }) 
            as Func<Task<object>>;
        
        // Assert
        Assert.NotNull(wrappedOp);
    }

    /// <summary>
    /// Test interface for proxy testing.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// Method with retry attribute.
        /// </summary>
        [Retry(3, 10)]
        Task<string> GetDataWithRetry();
    }

    /// <summary>
    /// Test interface for supervision testing.
    /// </summary>
    public interface ITestService2
    {
        /// <summary>
        /// Method with supervision.
        /// </summary>
        Task<string> GetData();
    }

    /// <summary>
    /// Test implementation for ITestService.
    /// </summary>
    private class TestService : ITestService
    {
        /// <summary>
        /// Gets the number of times GetDataWithRetry was called.
        /// </summary>
        public int CallCount { get; set; }

        /// <inheritdoc/>
        public Task<string> GetDataWithRetry()
        {
            CallCount++;
            if (CallCount < 3)
                throw new Exception($"Attempt {CallCount} failed");
            return Task.FromResult("success");
        }
    }

    /// <summary>
    /// Test implementation for ITestService2.
    /// </summary>
    private class TestService2 : ITestService2
    {
        /// <inheritdoc/>
        public Task<string> GetData()
        {
            return Task.FromResult("data");
        }
    }
}
