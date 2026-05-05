namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Comprehensive tests for ResilientProxy covering all InvokeGeneric paths.
/// </summary>
public class ResilientProxyComprehensiveTests : ProxyTestBase
{
    /// <summary>
    /// Sets up proxy with all resilience actors.
    /// </summary>
    private object SetupProxyWithAllActors(out TestService decorated)
    {
        var _actorSystem = CreateActorSystem();
        var _retryActor = _actorSystem.ActorOf(Props.Create(() => new RetryActor(new RetryOptions())), "retry");
        var _circuitBreakerActor = _actorSystem.ActorOf(
            Props.Create(() => new CircuitBreakerActor(new RetryOptions())), "circuit-breaker");

        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        decorated = new TestService();
        resilientProxy.DecoratedInstance = decorated;
        resilientProxy.ResilienceActorRef = _retryActor;
        resilientProxy.CircuitBreakerActorRef = _circuitBreakerActor;
        resilientProxy.ActorSystem = _actorSystem;

        return proxy!;
    }

    /// <summary>
    /// Tests retry path with all actors set up.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_execute_retry_path()
    {
        // Arrange
        var proxy = SetupProxyWithAllActors(out var testService);
        testService.CallCount = 2; // Skip first 2 failures
        var serviceProxy = (ITestService)(object)proxy;

        // Act
        var result = await serviceProxy.GetDataWithRetry();

        // Assert
        Assert.Equal("success", result);
        Assert.True(testService.CallCount >= 3);
    }

    /// <summary>
    /// Tests circuit breaker path with all actors set up.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_execute_circuit_breaker_path()
    {
        // Arrange
        var proxy = SetupProxyWithAllActors(out var testService);
        var serviceProxy = (ITestService)(object)proxy;

        // Act
        var result = await serviceProxy.GetDataWithCircuitBreaker();

        // Assert
        Assert.Equal("success", result);
    }

    /// <summary>
    /// Tests supervision path with all actors set up.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_execute_supervision_path()
    {
        // Arrange
        var _actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = new TestService2();
        resilientProxy.ActorSystem = _actorSystem;
        var serviceProxy = (ITestService2)(object)proxy;

        // Act
        var result = await serviceProxy.GetDataWithSupervision();

        // Assert
        Assert.Equal("supervised", result);
    }

    /// <summary>
    /// Tests that InvokeGeneric throws for non-generic Task.
    /// </summary>
    [Fact]
    public void InvokeGeneric_should_throw_for_non_generic_Task()
    {
        // Arrange
        var _actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = new TestService();
        resilientProxy.ActorSystem = _actorSystem;

        var method = typeof(ResilientProxyComprehensiveTests).GetMethod(nameof(NonGenericTask), 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (method is null)
            throw new InvalidOperationException("NonGenericTask method not found");
        
        // Act & Assert - Use reflection to call InvokeResilient
        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod!.Invoke(resilientProxy, new object[] { method, Array.Empty<object>(), null!, null!, null!, null! }));
        
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Only Task<T> supported", ex.InnerException.Message);
    }

    /// <summary>
    /// Helper method that returns non-generic Task.
    /// </summary>
    private Task NonGenericTask() => Task.CompletedTask;

    /// <summary>
    /// Test interface for proxy testing with various attributes.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// Method with retry attribute.
        /// </summary>
        [Retry(3, 10)]
        Task<string> GetDataWithRetry();

        /// <summary>
        /// Method with circuit breaker attribute.
        /// </summary>
        [CircuitBreaker(5, 1000)]
        Task<string> GetDataWithCircuitBreaker();

        /// <summary>
        /// Method with no attributes (for testing error path).
        /// </summary>
        Task<string> GetDataNoAttributes();
    }

    /// <summary>
    /// Test interface for supervision testing.
    /// </summary>
    public interface ITestService2
    {
        /// <summary>
        /// Method with supervision attribute.
        /// </summary>
        [Supervision(SupervisionStrategy.Restart)]
        Task<string> GetDataWithSupervision();
    }

    /// <summary>
    /// Test implementation for ITestService.
    /// </summary>
    private class TestService : ITestService
    {
        /// <summary>
        /// Gets the number of times methods were called.
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

        /// <inheritdoc/>
        public Task<string> GetDataWithCircuitBreaker()
        {
            return Task.FromResult("success");
        }

        /// <inheritdoc/>
        public Task<string> GetDataNoAttributes()
        {
            return Task.FromResult("no-attributes");
        }
    }

    /// <summary>
    /// Test implementation for ITestService2.
    /// </summary>
    private class TestService2 : ITestService2
    {
        /// <inheritdoc/>
        public Task<string> GetDataWithSupervision()
        {
            return Task.FromResult("supervised");
        }
    }
}
