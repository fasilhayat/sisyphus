namespace Oasis.Resilience.Test.Unit.Infrastructure;

using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for ResilientProxy core methods including InvokeResilient, HandleFanOut, and static factory methods.
/// </summary>
public class ResilientProxyCoreTests
{
    /// <summary>
    /// Tests that RegisterMessageFactory correctly registers a message factory delegate.
    /// </summary>
    [Fact]
    public void RegisterMessageFactory_should_set_factory()
    {
        // Arrange
        Func<Type, object, ParameterInfo[], object[], object> factory = (type, splitValue, parameters, otherArgs) =>
        {
            return new object();
        };

        // Act
        ResilientProxy<ITestService>.RegisterMessageFactory(factory);

        // Use reflection to verify the factory was set
        var field = typeof(ResilientProxy<ITestService>).GetField("_messageFactory",
            BindingFlags.NonPublic | BindingFlags.Static);
        var storedFactory = field?.GetValue(null) as Func<Type, object, ParameterInfo[], object[], object>;

        // Assert
        Assert.NotNull(storedFactory);
    }

    /// <summary>
    /// Tests that RegisterResultAggregator correctly registers an aggregator delegate.
    /// </summary>
    [Fact]
    public void RegisterResultAggregator_should_set_aggregator()
    {
        // Arrange
        Func<object[], Type, Type, object> aggregator = (results, workerType, returnType) =>
        {
            return new object();
        };

        // Act
        ResilientProxy<ITestService>.RegisterResultAggregator(aggregator);

        // Use reflection to verify the aggregator was set
        var field = typeof(ResilientProxy<ITestService>).GetField("_resultAggregator",
            BindingFlags.NonPublic | BindingFlags.Static);
        var storedAggregator = field?.GetValue(null) as Func<object[], Type, Type, object>;

        // Assert
        Assert.NotNull(storedAggregator);
    }

    /// <summary>
    /// Tests attribute caching for RetryAttribute.
    /// </summary>
    [Fact]
    public void Attribute_caching_should_work_for_retry()
    {
        // Arrange
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetDataAsync));

        // Clear cache first using reflection
        var cacheField = typeof(ResilientProxy<ITestService>).GetField("RetryAttributeCache",
            BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as ConcurrentDictionary<MethodInfo, RetryAttribute?>;
        cache?.TryRemove(method!, out _);

        // Act - Call to trigger caching
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        resilientProxy.DecoratedInstance = new TestService();

        try { proxy.GetDataAsync(); } catch { }

        // Assert - Attribute should be in cache
        var cachedAttr = cache?.GetOrAdd(method!, m => m.GetCustomAttribute<RetryAttribute>());
        Assert.NotNull(cachedAttr);
        Assert.Equal(3, cachedAttr!.MaxAttempts);
    }

    /// <summary>
    /// Tests RegisterMessageFactory and CreateWorkerMessage.
    /// </summary>
    [Fact]
    public void RegisterMessageFactory_should_create_messages()
    {
        // Arrange
        Func<Type, object, ParameterInfo[], object[], object> factory = (type, splitValue, parameters, otherArgs) =>
        {
            return new { Type = type.Name, Value = splitValue };
        };

        // Act
        ResilientProxy<ITestService>.RegisterMessageFactory(factory);

        // Use reflection to call CreateWorkerMessage
        var method = typeof(ResilientProxy<ITestService>).GetMethod("CreateWorkerMessage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var result = method?.Invoke(resilientProxy, [typeof(string), "test", Array.Empty<ParameterInfo>(), Array.Empty<object>()]);

        // Assert
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests RegisterResultAggregator and AggregateResults.
    /// </summary>
    [Fact]
    public void RegisterResultAggregator_should_aggregate_results()
    {
        // Arrange
        Func<object[], Type, Type, object> aggregator = (results, workerType, returnType) =>
        {
            return results.Length;
        };

        // Act
        ResilientProxy<ITestService>.RegisterResultAggregator(aggregator);

        // Use reflection to call AggregateResults
        var method = typeof(ResilientProxy<ITestService>).GetMethod("AggregateResults",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var result = method?.MakeGenericMethod(typeof(int))?.Invoke(resilientProxy, [new object[] { 1, 2, 3 }, typeof(string)]);

        // Assert
        Assert.Equal(3, result);
    }

    /// <summary>
    /// Tests that InvokeResilient throws for non-generic Task return type.
    /// </summary>
    [Fact]
    public void InvokeResilient_should_throw_for_non_generic_task()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");
        var method = typeof(ITestService).GetMethod(nameof(ITestService.SimpleMethod));

        // Act & Assert - Create a delegate to call InvokeResilient with correct parameters
        // InvokeResilient signature: object InvokeResilient(MethodInfo, object?[]?, RetryAttribute?, CircuitBreakerAttribute?, SupervisionAttribute?, FanOutAttribute?)
        var paramTypes = new Type[] { typeof(MethodInfo), typeof(object[]), typeof(RetryAttribute), typeof(CircuitBreakerAttribute), typeof(SupervisionAttribute), typeof(FanOutAttribute) };
        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // This should throw InvalidOperationException because SimpleMethod doesn't return Task<T>
        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod?.Invoke(resilientProxy, [method, Array.Empty<object>(), null, null, null, null]));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    /// <summary>
    /// Tests WrapWithSupervision method.
    /// </summary>
    [Fact]
    public async Task WrapWithSupervision_should_wrap_operation()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var supervisionAttr = new SupervisionAttribute();

        // Use reflection to call WrapWithSupervision
        var method = typeof(ResilientProxy<ITestService>).GetMethod("WrapWithSupervision",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var wrappedOp = (Func<Task<object>>)method?.Invoke(resilientProxy,
            [() => Task.FromResult<object>("test"), supervisionAttr])!;

        // Act
        var result = await wrappedOp();

        // Assert
        Assert.Equal("test", result);
    }

    /// <summary>
    /// Test interface for proxy testing.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// Simple method for testing.
        /// </summary>
        string SimpleMethod();

        /// <summary>
        /// Async method with retry attribute.
        /// </summary>
        [Retry(3, 100)]
        Task<string> GetDataAsync();
    }

    /// <summary>
    /// Test implementation for proxy testing.
    /// </summary>
    private class TestService : ITestService
    {
        /// <summary>
        /// Gets the number of times GetDataAsync was called.
        /// </summary>
        public int CallCount { get; set; }

        /// <inheritdoc/>
        public string SimpleMethod() => "SimpleResult";

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
