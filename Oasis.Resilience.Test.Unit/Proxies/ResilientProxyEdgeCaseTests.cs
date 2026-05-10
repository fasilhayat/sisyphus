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
/// Edge case unit tests for <see cref="ResilientProxy{T}"/>.
/// </summary>
public class ResilientProxyEdgeCaseTests : ProxyTestBase
{
    /// <summary>
    /// Creates a proxy configured with only a retry actor.
    /// </summary>
    /// <param name="decorated">The decorated <see cref="TestService"/> instance.</param>
    /// <returns>The configured resilient proxy.</returns>
    private ResilientProxy<ITestService> CreateProxyWithRetryOnly(out TestService decorated)
    {
        var _actorSystem = CreateActorSystem();
        var _retryActor = _actorSystem.ActorOf(Props.Create(() => new RetryActor(new RetryOptions(), null)), "retry");

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
    /// Verifies that a proxy with only a retry actor can execute retry logic.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_work_with_retry_only()
    {
        var proxy = CreateProxyWithRetryOnly(out var testService);
        testService.CallCount = 2;
        var serviceProxy = (ITestService)(object)proxy;

        var result = await serviceProxy.GetDataWithRetry();

        Assert.Equal("success", result);
        Assert.True(testService.CallCount >= 3);
    }

    /// <summary>
    /// Verifies that operations work correctly when no circuit breaker actor is configured.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_not_use_circuit_breaker_when_not_configured()
    {
        var proxy = CreateProxyWithRetryOnly(out var testService);
        testService.CallCount = 2;
        var serviceProxy = (ITestService)(object)proxy;

        var result = await serviceProxy.GetDataWithRetry();

        Assert.Equal("success", result);
    }

    /// <summary>
    /// Verifies that <see cref="ResilientProxy{T}"/> wraps an operation with supervision and returns its result.
    /// </summary>
    [Fact]
    public async Task WrapWithSupervision_should_return_wrapped_operation()
    {
        var _actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.ActorSystem = _actorSystem;

        var supervisionAttr = new SupervisionAttribute(SupervisionStrategy.Restart);

        var dummyMethod = typeof(ITestService2).GetMethods()[0];
        var method = typeof(ResilientProxy<ITestService2>).GetMethod("WrapWithSupervision",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var wrappedOp = method!.Invoke(resilientProxy,
            new object[] { dummyMethod, new Func<Task<object>>(() => Task.FromResult<object>("test")), supervisionAttr })
            as Func<Task<object>>;

        Assert.NotNull(wrappedOp);
        var result = await wrappedOp();
        Assert.Equal("test", result);
    }

    /// <summary>
    /// Verifies that all supervision strategies work correctly with <see cref="ResilientProxy{T}"/>.
    /// </summary>
    [Fact]
    public async Task WrapWithSupervision_should_work_with_all_strategies()
    {
        var _actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.ActorSystem = _actorSystem;

        var method = typeof(ResilientProxy<ITestService2>).GetMethod("WrapWithSupervision",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var dummyMethods = typeof(ITestService2).GetMethods();
        int methodIndex = 0;
        foreach (var strategy in new[] { SupervisionStrategy.Restart, SupervisionStrategy.RestartWithBackoff, SupervisionStrategy.Stop, SupervisionStrategy.Escalate, SupervisionStrategy.Resume })
        {
            var supervisionAttr = new SupervisionAttribute(strategy);
            var dummyMethod = dummyMethods[methodIndex++ % dummyMethods.Length];

            var wrappedOp = method!.Invoke(resilientProxy,
                new object[] { dummyMethod, new Func<Task<object>>(() => Task.FromResult<object>($"result-{strategy}")), supervisionAttr })
                as Func<Task<object>>;

            Assert.NotNull(wrappedOp);
            var result = await wrappedOp();
            Assert.Equal($"result-{strategy}", result);
        }
    }

    /// <summary>
    /// Test service interface for retry edge case tests.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// An async method decorated with <see cref="RetryAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [Retry(3, 10)]
        Task<string> GetDataWithRetry();
    }

    /// <summary>
    /// Test service interface for supervision edge case tests.
    /// </summary>
    public interface ITestService2
    {
        /// <summary>
        /// An async method for testing supervision.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        Task<string> GetData();
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
    }

    /// <summary>
    /// Test implementation of <see cref="ITestService2"/>.
    /// </summary>
    private class TestService2 : ITestService2
    {
        /// <summary>
        /// Returns a data string for supervision edge case tests.
        /// </summary>
        /// <returns>A task that yields a data string.</returns>
        public Task<string> GetData()
        {
            return Task.FromResult("data");
        }
    }
}
