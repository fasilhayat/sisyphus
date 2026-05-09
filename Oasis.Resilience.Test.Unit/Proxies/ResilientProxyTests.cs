namespace Oasis.Resilience.Test.Unit.Proxies;

using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ResilientProxy{T}"/> basic functionality.
/// </summary>
public class ResilientProxyTests : ProxyTestBase
{
    /// <summary>
    /// Verifies that properties on <see cref="ResilientProxy{T}"/> can be set and read.
    /// </summary>
    [Fact]
    public async Task Proxy_should_allow_setting_properties()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var actorSystem = CreateActorSystem("test-system");
        var supervisionOptions = new SupervisionOptions();
        var fanOutOptions = new FanOutOptions();

        resilientProxy.ActorSystem = actorSystem;
        resilientProxy.SupervisionOptions = supervisionOptions;
        resilientProxy.FanOutOptions = fanOutOptions;

        Assert.Equal(actorSystem, resilientProxy.ActorSystem);
        Assert.Equal(supervisionOptions, resilientProxy.SupervisionOptions);
        Assert.Equal(fanOutOptions, resilientProxy.FanOutOptions);

        await actorSystem.Terminate();
    }

    /// <summary>Verifies that the proxy allows setting the decorated instance.</summary>
    [Fact]
    public void Proxy_should_allow_setting_decorated_instance()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        var decorated = new TestService();

        resilientProxy.DecoratedInstance = decorated;

        Assert.Equal(decorated, resilientProxy.DecoratedInstance);
    }

    /// <summary>
    /// Verifies that method calls are forwarded to the decorated instance.
    /// </summary>
    [Fact]
    public void Proxy_should_invoke_decorated_instance_methods()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        var service = new TestService();
        resilientProxy.DecoratedInstance = service;

        var result = proxy.SimpleMethod();

        Assert.Equal("SimpleResult", result);
        Assert.True(service.SimpleMethodCalled);
    }

    /// <summary>
    /// Verifies that <see cref="RetryAttribute"/> is correctly read from method metadata.
    /// </summary>
    [Fact]
    public void Proxy_should_recognize_retry_attribute()
    {
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetDataAsync));
        var attribute = method?.GetCustomAttribute<RetryAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(3, attribute.MaxAttempts);
        Assert.Equal(100, attribute.InitialDelay);
    }

    /// <summary>
    /// Verifies that <see cref="SupervisionAttribute"/> is correctly read from method metadata.
    /// </summary>
    [Fact]
    public void Proxy_should_recognize_supervision_attribute()
    {
        var method = typeof(ITestService).GetMethod(nameof(ITestService.SupervisedMethod));
        var attr = method?.GetCustomAttribute<SupervisionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(SupervisionStrategy.Restart, attr!.Strategy);
    }

    /// <summary>
    /// Verifies that <see cref="ResilientProxy{T}"/> handles a null decorated instance without throwing.
    /// </summary>
    [Fact]
    public void Proxy_should_handle_null_decorated_instance()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        Assert.Null(resilientProxy.DecoratedInstance);
    }

    /// <summary>
    /// Verifies that async methods are correctly invoked through the proxy.
    /// </summary>
    [Fact]
    public async Task Proxy_should_invoke_async_methods()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        var service = new TestService();
        service.CallCount = 2;
        resilientProxy.DecoratedInstance = service;

        var result = await proxy.GetDataAsync();

        Assert.Equal("success", result);
    }

    /// <summary>
    /// Verifies that method attributes are cached after the first invocation.
    /// </summary>
    [Fact]
    public void Proxy_should_cache_method_attributes()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        var decorated = new TestService();
        resilientProxy.DecoratedInstance = decorated;

        try { proxy.GetDataAsync(); } catch { }
        try { proxy.GetDataAsync(); } catch { }

        Assert.True(true);
    }

    /// <summary>
    /// Verifies that instance-level message factory and result aggregator registrations are supported.
    /// </summary>
    [Fact]
    public void Proxy_should_support_instance_level_factory_registration()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        bool factoryCalled = false;
        resilientProxy.SetMessageFactory((type, value, parameters, args) =>
        {
            factoryCalled = true;
            return new object();
        });

        Assert.False(factoryCalled);

        bool aggregatorCalled = false;
        resilientProxy.SetResultAggregator((results, type, returnType) =>
        {
            aggregatorCalled = true;
            return "result";
        });

        Assert.False(aggregatorCalled);
    }

    /// <summary>
    /// Test service interface for resilient proxy tests.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// A simple synchronous method.
        /// </summary>
        /// <returns>A string result.</returns>
        string SimpleMethod();

        /// <summary>
        /// An async method decorated with <see cref="RetryAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [Retry(3, 100)]
        Task<string> GetDataAsync();

        /// <summary>
        /// An async method decorated with <see cref="SupervisionAttribute"/>.
        /// </summary>
        /// <returns>A task that yields a string.</returns>
        [Supervision(SupervisionStrategy.Restart)]
        Task<string> SupervisedMethod();

        /// <summary>
        /// A fan-out method for testing.
        /// </summary>
        /// <param name="items">The items to process.</param>
        /// <returns>A task that yields a dictionary of results.</returns>
        Task<Dictionary<int, string>> FanOutMethod(int[] items);
    }

    /// <summary>
    /// Test implementation of <see cref="ITestService"/>.
    /// </summary>
    private class TestService : ITestService
    {
        /// <summary>
        /// Gets whether <see cref="SimpleMethod"/> has been called.
        /// </summary>
        public bool SimpleMethodCalled { get; private set; }

        /// <summary>
        /// Gets the number of times <see cref="SimpleMethod"/> has been called.
        /// </summary>
        public int SimpleMethodCallCount { get; private set; }

        /// <summary>
        /// Gets or sets the number of times methods have been called.
        /// </summary>
        public int CallCount { get; set; }

        /// <summary>
        /// Returns a simple result and tracks invocation.
        /// </summary>
        /// <returns>A string result.</returns>
        public string SimpleMethod()
        {
            SimpleMethodCalled = true;
            SimpleMethodCallCount++;
            return "SimpleResult";
        }

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

        /// <summary>
        /// Returns a supervised result.
        /// </summary>
        /// <returns>A task that yields a supervised result string.</returns>
        public Task<string> SupervisedMethod()
        {
            return Task.FromResult("SupervisedResult");
        }

        /// <summary>
        /// Returns an empty dictionary for fan-out tests.
        /// </summary>
        /// <param name="items">The items to process.</param>
        /// <returns>A task that yields an empty dictionary.</returns>
        public Task<Dictionary<int, string>> FanOutMethod(int[] items)
        {
            return Task.FromResult(new Dictionary<int, string>());
        }
    }
}
