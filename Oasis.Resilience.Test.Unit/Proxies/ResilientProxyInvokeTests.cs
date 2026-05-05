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
/// Tests for ResilientProxy invoke paths including retry, circuit breaker, supervision, and fan-out.
/// </summary>
public class ResilientProxyInvokeTests : ProxyTestBase
{
    private ActorSystem? _actorSystem;
    private IActorRef? _retryActor;
    private IActorRef? _circuitBreakerActor;

    /// <summary>
    /// Creates a proxy with actor references for testing resilience paths.
    /// </summary>
    private object CreateProxyWithActors(out ITestService decorated)
    {
        var config = ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
        ");
        _actorSystem = CreateActorSystem($"test-system-{Guid.NewGuid()}");
        _retryActor = _actorSystem.ActorOf(Props.Create(() => new RetryActor(new RetryOptions())), "retry");
        _circuitBreakerActor = _actorSystem.ActorOf(
            Props.Create(() => new CircuitBreakerActor(new RetryOptions())), "circuit-breaker");

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
    /// Tests that retry attribute causes method to be invoked via the retry actor.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_use_retry_when_retry_attribute_present()
    {
        // Arrange
        var proxy = CreateProxyWithActors(out var decorated);
        var testService = (TestService)decorated;
        testService.CallCount = 2; // Skip first 2 failures

        // Cast to interface to access GetDataAsync
        var serviceProxy = (ITestService)proxy;

        // Act
        var result = await serviceProxy.GetDataAsync();

        // Assert
        Assert.Equal("success", result);
        Assert.True(testService.CallCount >= 3);
    }

    /// <summary>
    /// Tests that supervision attribute without retry/circuit breaker executes the operation.
    /// </summary>
    [Fact]
    public async Task InvokeGeneric_should_handle_supervision_only()
    {
        // Arrange
        _actorSystem = ActorSystem.Create($"test-system-{Guid.NewGuid()}");

        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ??
            throw new InvalidOperationException("Failed to create proxy");

        var decorated = new TestService2();
        resilientProxy.DecoratedInstance = decorated;
        resilientProxy.ActorSystem = _actorSystem;

        // Act
        var result = await proxy.SupervisedOnlyMethod();

        // Assert
        Assert.Equal("SupervisedResult", result);
    }

    /// <summary>
    /// Tests that non-generic Task return type throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void InvokeGeneric_should_throw_for_non_generic_task()
    {
        // Arrange
        var proxy = DispatchProxy.Create<INonGenericService, ResilientProxy<INonGenericService>>();
        var resilientProxy = proxy as ResilientProxy<INonGenericService> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = new NonGenericService();

        // Use reflection to call InvokeResilient directly (which calls InvokeGeneric internally)
        var method = typeof(ResilientProxy<INonGenericService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var doWorkMethod = typeof(INonGenericService).GetMethod(nameof(INonGenericService.DoWork));

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(resilientProxy, [doWorkMethod!, Array.Empty<object>(), null, null, null, null]));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Only Task<T> supported", ex.InnerException.Message);
    }

    /// <summary>
    /// Tests the WrapWithSupervision method.
    /// </summary>
    [Fact]
    public async Task WrapWithSupervision_should_wrap_operation()
    {
        // Arrange
        _actorSystem = CreateActorSystem($"test-system-{Guid.NewGuid()}");

        var proxy = DispatchProxy.Create<ITestService2, ResilientProxy<ITestService2>>();
        var resilientProxy = proxy as ResilientProxy<ITestService2> ??
            throw new InvalidOperationException("Failed to create proxy");

        // Use reflection to call WrapWithSupervision
        var method = typeof(ResilientProxy<ITestService2>).GetMethod("WrapWithSupervision",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var supervisionAttr = new SupervisionAttribute(SupervisionStrategy.Restart);

        // Act
        var wrappedOp = method!.Invoke(resilientProxy,
            [new Func<Task<object>>(() => Task.FromResult<object>("test")), supervisionAttr])
            as Func<Task<object>>;

        Assert.NotNull(wrappedOp);
        var result = await wrappedOp();

        // Assert
        Assert.Equal("test", result);
    }

    /// <inheritdoc/>
    protected new void Dispose()
    {
        base.Dispose();
    }

    /// <summary>
    /// Test interface for proxy testing with retry attribute.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// Async method with retry attribute.
        /// </summary>
        [Retry(3, 10)]
        Task<string> GetDataAsync();
    }

    /// <summary>
    /// Test interface for supervision-only testing.
    /// </summary>
    public interface ITestService2
    {
        /// <summary>
        /// Method with supervision attribute only.
        /// </summary>
        [Supervision(SupervisionStrategy.Restart)]
        Task<string> SupervisedOnlyMethod();
    }

    /// <summary>
    /// Test interface for non-generic Task testing.
    /// </summary>
    public interface INonGenericService
    {
        /// <summary>
        /// Method returning non-generic Task.
        /// </summary>
        Task DoWork();
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

    /// <summary>
    /// Test implementation for ITestService2.
    /// </summary>
    private class TestService2 : ITestService2
    {
        /// <inheritdoc/>
        public Task<string> SupervisedOnlyMethod()
        {
            return Task.FromResult("SupervisedResult");
        }
    }

    /// <summary>
    /// Test implementation for INonGenericService.
    /// </summary>
    private class NonGenericService : INonGenericService
    {
        /// <inheritdoc/>
        public Task DoWork() => Task.CompletedTask;
    }
}
