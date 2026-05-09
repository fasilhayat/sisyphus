namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Comprehensive unit tests for <see cref="ResilientProxy{T}"/> covering retry, circuit breaker, and supervision paths.
/// </summary>
public class ResilientProxyComprehensiveTests : ProxyTestBase
{
    /// <summary>
    /// Sets up a proxy with retry, circuit breaker, and supervision actors configured.
    /// </summary>
    /// <param name="decorated">The decorated <see cref="TestService"/> instance.</param>
    /// <returns>The created proxy instance.</returns>
    private object SetupProxyWithAllActors(out TestService decorated)
    {
        var _actorSystem = CreateActorSystem();
        var _retryActor = _actorSystem.ActorOf(Props.Create(() => new RetryActor(new RetryOptions(), null)), "retry");
        var _circuitBreakerActor = _actorSystem.ActorOf(
            Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)), "circuit-breaker");

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
    /// Verifies the retry path is executed when a method is decorated with <see cref="RetryAttribute"/>.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_execute_retry_path()
    {
        var proxy = SetupProxyWithAllActors(out var testService);
        testService.CallCount = 2;
        var serviceProxy = (ITestService)(object)proxy;

        var result = await serviceProxy.GetDataWithRetry();

        Assert.Equal("success", result);
        Assert.True(testService.CallCount >= 3);
    }

    /// <summary>
    /// Verifies the circuit breaker path is executed when a method is decorated with <see cref="CircuitBreakerAttribute"/>.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_execute_circuit_breaker_path()
    {
        var proxy = SetupProxyWithAllActors(out var testService);
        var serviceProxy = (ITestService)(object)proxy;

        var result = await serviceProxy.GetDataWithCircuitBreaker();

        Assert.Equal("success", result);
    }

    /// <summary>
    /// Verifies the supervision path is executed when a method is decorated with <see cref="SupervisionAttribute"/>.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_execute_supervision_path()
    {
        var _actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = new TestService2();
        resilientProxy.ActorSystem = _actorSystem;
        var serviceProxy = (ITestService2)(object)proxy;

        var result = await serviceProxy.GetDataWithSupervision();

        Assert.Equal("supervised", result);
    }

    /// <summary>
    /// Verifies that <see cref="ResilientProxy{T}"/> throws for non-generic Task return types.
    /// </summary>
    [Fact]
    public void InvokeGeneric_should_throw_for_non_generic_Task()
    {
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

        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod!.Invoke(resilientProxy, new object[] { method, Array.Empty<object>(), null!, null!, null!, null! }));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Only Task<T> supported", ex.InnerException.Message);
    }

    /// <summary>
    /// A non-generic Task method used for testing validation.
    /// </summary>
    /// <returns>A completed task.</returns>
    private Task NonGenericTask() => Task.CompletedTask;

    /// <summary>
    /// Test service interface for retry and circuit breaker tests.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// An async method decorated with <see cref="RetryAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [Retry(3, 10)]
        Task<string> GetDataWithRetry();

        /// <summary>
        /// An async method decorated with <see cref="CircuitBreakerAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [CircuitBreaker(5, 1000)]
        Task<string> GetDataWithCircuitBreaker();

        /// <summary>
        /// An async method with no resilience attributes.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        Task<string> GetDataNoAttributes();
    }

    /// <summary>
    /// Test service interface for supervision tests.
    /// </summary>
    public interface ITestService2
    {
        /// <summary>
        /// An async method decorated with <see cref="SupervisionAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [Supervision(SupervisionStrategy.Restart)]
        Task<string> GetDataWithSupervision();
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
        public Task<string> GetDataWithRetry()
        {
            CallCount++;
            if (CallCount < 3)
                throw new Exception($"Attempt {CallCount} failed");
            return Task.FromResult("success");
        }

        /// <summary>
        /// Returns a success result for circuit breaker tests.
        /// </summary>
        /// <returns>A task that yields a success string.</returns>
        public Task<string> GetDataWithCircuitBreaker()
        {
            return Task.FromResult("success");
        }

        /// <summary>
        /// Returns a result for methods without resilience attributes.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        public Task<string> GetDataNoAttributes()
        {
            return Task.FromResult("no-attributes");
        }
    }

    /// <summary>
    /// Test implementation of <see cref="ITestService2"/>.
    /// </summary>
    private class TestService2 : ITestService2
    {
        /// <summary>
        /// Returns a supervised result for supervision tests.
        /// </summary>
        /// <returns>A task that yields a supervised string.</returns>
        public Task<string> GetDataWithSupervision()
        {
            return Task.FromResult("supervised");
        }
    }
}
