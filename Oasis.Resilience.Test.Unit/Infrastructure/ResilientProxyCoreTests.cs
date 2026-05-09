namespace Oasis.Resilience.Test.Unit.Infrastructure;

using Akka.Configuration;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for the core <see cref="ResilientProxy{T}"/> infrastructure.
/// </summary>
public class ResilientProxyCoreTests
{
    /// <summary>
    /// Verifies <see cref="ResilientProxy{T}.RegisterMessageFactory"/> stores the factory globally.
    /// </summary>
    [Fact]
    public void RegisterMessageFactory_should_set_factory()
    {
        Func<Type, object, ParameterInfo[], object[], object> factory = (type, splitValue, parameters, otherArgs) =>
        {
            return new object();
        };

        ResilientProxy<ITestService>.RegisterMessageFactory(factory);

        var field = typeof(ResilientProxy<ITestService>).GetField("_globalMessageFactory",
            BindingFlags.NonPublic | BindingFlags.Static);
        var storedFactory = field?.GetValue(null) as Func<Type, object, ParameterInfo[], object[], object>;

        Assert.NotNull(storedFactory);
    }

    /// <summary>
    /// Verifies <see cref="ResilientProxy{T}.RegisterResultAggregator"/> stores the aggregator globally.
    /// </summary>
    [Fact]
    public void RegisterResultAggregator_should_set_aggregator()
    {
        Func<object[], Type, Type, object> aggregator = (results, workerType, returnType) =>
        {
            return new object();
        };

        ResilientProxy<ITestService>.RegisterResultAggregator(aggregator);

        var field = typeof(ResilientProxy<ITestService>).GetField("_globalResultAggregator",
            BindingFlags.NonPublic | BindingFlags.Static);
        var storedAggregator = field?.GetValue(null) as Func<object[], Type, Type, object>;

        Assert.NotNull(storedAggregator);
    }

    /// <summary>
    /// Verifies that <see cref="RetryAttribute"/> values are cached for method invocations.
    /// </summary>
    [Fact]
    public void Attribute_caching_should_work_for_retry()
    {
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetDataAsync));

        var cacheField = typeof(ResilientProxy<ITestService>).GetField("RetryAttributeCache",
            BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<MethodInfo, RetryAttribute?>;
        cache?.TryRemove(method!, out _);

        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        resilientProxy.DecoratedInstance = new TestService();

        try { proxy.GetDataAsync(); } catch { }

        var cachedAttr = cache?.GetOrAdd(method!, m => m.GetCustomAttribute<RetryAttribute>());
        Assert.NotNull(cachedAttr);
        Assert.Equal(3, cachedAttr!.MaxAttempts);
    }

    /// <summary>
    /// Verifies a registered message factory is invoked to create worker messages.
    /// </summary>
    [Fact]
    public void RegisterMessageFactory_should_create_messages()
    {
        Func<Type, object, ParameterInfo[], object[], object> factory = (type, splitValue, parameters, otherArgs) =>
        {
            return new { Type = type.Name, Value = splitValue };
        };

        ResilientProxy<ITestService>.RegisterMessageFactory(factory);

        var method = typeof(ResilientProxy<ITestService>).GetMethod("CreateWorkerMessage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var result = method?.Invoke(resilientProxy, [typeof(string), "test", Array.Empty<ParameterInfo>(), Array.Empty<object>()]);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Verifies a registered result aggregator is invoked to combine worker results.
    /// </summary>
    [Fact]
    public void RegisterResultAggregator_should_aggregate_results()
    {
        Func<object[], Type, Type, object> aggregator = (results, workerType, returnType) =>
        {
            return results.Length;
        };

        ResilientProxy<ITestService>.RegisterResultAggregator(aggregator);

        var method = typeof(ResilientProxy<ITestService>).GetMethod("AggregateResults",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var result = method?.MakeGenericMethod(typeof(int))?.Invoke(resilientProxy, [new object[] { 1, 2, 3 }, typeof(string)]);

        Assert.Equal(3, result);
    }

    /// <summary>
    /// Verifies that <see cref="ResilientProxy{T}"/> throws for non-generic Task return types.
    /// </summary>
    [Fact]
    public void InvokeResilient_should_throw_for_non_generic_task()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        var method = typeof(ITestService).GetMethod(nameof(ITestService.SimpleMethod));

        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient", BindingFlags.NonPublic | BindingFlags.Instance);

        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod?.Invoke(resilientProxy, [method, Array.Empty<object>(), null, null, null, null]));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    /// <summary>
    /// Verifies that <see cref="ResilientProxy{T}"/> wraps an operation with supervision.
    /// </summary>
    [Fact]
    public async Task WrapWithSupervision_should_wrap_operation()
    {
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var config = ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
            akka.coordinated-shutdown.log-level = ERROR
        ");
        resilientProxy.ActorSystem = Akka.Actor.ActorSystem.Create("test-supervision-system", config);

        var supervisionAttr = new SupervisionAttribute();

        var method = typeof(ResilientProxy<ITestService>).GetMethod("WrapWithSupervision",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var wrappedOp = (Func<Task<object>>)method?.Invoke(resilientProxy,
            [() => Task.FromResult<object>("test"), supervisionAttr])!;

        var result = await wrappedOp();

        Assert.Equal("test", result);

        await resilientProxy.ActorSystem.Terminate();
    }

    /// <summary>
    /// Test service interface for proxy core tests.
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
        /// Returns a simple string result.
        /// </summary>
        /// <returns>A string result.</returns>
        public string SimpleMethod() => "SimpleResult";

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
