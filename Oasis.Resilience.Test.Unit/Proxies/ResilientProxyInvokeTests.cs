namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Akka.Configuration;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for the invoke behavior of <see cref="ResilientProxy{T}"/>.
/// </summary>
public class ResilientProxyInvokeTests : ProxyTestBase
{
    /// <summary>
    /// The actor system used by the proxy.
    /// </summary>
    private ActorSystem? _actorSystem;
    /// <summary>
    /// The retry actor reference.
    /// </summary>
    private IActorRef? _retryActor;
    /// <summary>
    /// The circuit breaker actor reference.
    /// </summary>
    private IActorRef? _circuitBreakerActor;

    /// <summary>
    /// Creates a proxy with retry and circuit breaker actors configured.
    /// </summary>
    /// <param name="decorated">The decorated <see cref="TestService"/> instance.</param>
    /// <returns>The created proxy instance.</returns>
    private object CreateProxyWithActors(out ITestService decorated)
    {
        var config = ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
        ");
        _actorSystem = CreateActorSystem($"test-system-{Guid.NewGuid()}");
        _retryActor = _actorSystem.ActorOf(Props.Create(() => new RetryActor(new RetryOptions(), null)), "retry");
        _circuitBreakerActor = _actorSystem.ActorOf(
            Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)), "circuit-breaker");

        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        decorated = new TestService();
        resilientProxy.DecoratedInstance = decorated;
        resilientProxy.ActorSystem = _actorSystem;
        resilientProxy.ResilienceActorRef = _retryActor;
        resilientProxy.CircuitBreakerActorRef = _circuitBreakerActor;

        return resilientProxy;
    }

    /// <summary>
    /// Verifies that the retry path is used when a method is decorated with <see cref="RetryAttribute"/>.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_use_retry_when_retry_attribute_present()
    {
        var proxy = CreateProxyWithActors(out var decorated);
        var testService = (TestService)decorated;
        testService.CallCount = 2;

        var serviceProxy = (ITestService)proxy;

        var result = await serviceProxy.GetDataAsync();

        Assert.Equal("success", result);
        Assert.True(testService.CallCount >= 3);
    }

    /// <summary>
    /// Verifies that supervision-only methods work without retry or circuit breaker actors.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_handle_supervision_only()
    {
        _actorSystem = CreateActorSystem();

        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ??
            throw new InvalidOperationException("Failed to create proxy");

        var decorated = new TestService2();
        resilientProxy.DecoratedInstance = decorated;
        resilientProxy.ActorSystem = _actorSystem;

        var result = await proxy.SupervisedOnlyMethod();

        Assert.Equal("SupervisedResult", result);
    }

    /// <summary>
    /// Verifies that <see cref="ResilientProxy{T}"/> throws for non-generic Task return types.
    /// </summary>
    [Fact]
    public void InvokeGeneric_should_throw_for_non_generic_task()
    {
        var proxy = DispatchProxy.Create<INonGenericService, ResilientProxy<INonGenericService>>();
        var resilientProxy = proxy as ResilientProxy<INonGenericService> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = new NonGenericService();

        var method = typeof(ResilientProxy<INonGenericService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var doWorkMethod = typeof(INonGenericService).GetMethod(nameof(INonGenericService.DoWork));

        var ex = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(resilientProxy, [doWorkMethod!, Array.Empty<object>(), null, null, null, null]));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("supported return type", ex.InnerException.Message);
    }

    /// <summary>
    /// Verifies that <see cref="ResilientProxy{T}"/> creates a supervised actor and returns the result.
    /// </summary>
    [Fact]
    public async Task WrapWithSupervision_should_create_supervised_actor()
    {
        _actorSystem = CreateActorSystem();

        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.ActorSystem = _actorSystem;

        var method = typeof(ResilientProxy<ITestService2>).GetMethod("WrapWithSupervision",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var supervisionAttr = new SupervisionAttribute(SupervisionStrategy.Restart);

        var dummyMethod = typeof(ITestService2).GetMethods()[0];
        var wrappedOp = method!.Invoke(resilientProxy,
            [dummyMethod, new Func<Task<object>>(() => Task.FromResult<object>("test")), supervisionAttr])
            as Func<Task<object>>;

        Assert.NotNull(wrappedOp);
        var result = await wrappedOp();

        Assert.Equal("test", result);
    }

    /// <summary>
    /// Test service interface for retry invocation tests.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// An async method decorated with <see cref="RetryAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [Retry(3, 10)]
        Task<string> GetDataAsync();
    }

    /// <summary>
    /// Test service interface for supervision invocation tests.
    /// </summary>
    public interface ITestService2
    {
        /// <summary>
        /// An async method decorated with <see cref="SupervisionAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [Supervision(SupervisionStrategy.Restart)]
        Task<string> SupervisedOnlyMethod();
    }

    /// <summary>
    /// Test service interface for non-generic Task tests.
    /// </summary>
    public interface INonGenericService
    {
        /// <summary>
        /// A non-generic Task method.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DoWork();
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

    /// <summary>
    /// Test implementation of <see cref="ITestService2"/>.
    /// </summary>
    private class TestService2 : ITestService2
    {
        /// <summary>
        /// Returns a supervised result for supervision invocation tests.
        /// </summary>
        /// <returns>A task that yields a supervised result string.</returns>
        public Task<string> SupervisedOnlyMethod()
        {
            return Task.FromResult("SupervisedResult");
        }
    }

    /// <summary>
    /// Test implementation of <see cref="INonGenericService"/>.
    /// </summary>
    private class NonGenericService : INonGenericService
    {
        /// <summary>
        /// Returns a completed task for non-generic Task tests.
        /// </summary>
        /// <returns>A completed task.</returns>
        public Task DoWork() => Task.CompletedTask;
    }
}
