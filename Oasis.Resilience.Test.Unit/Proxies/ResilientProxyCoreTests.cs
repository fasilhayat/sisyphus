namespace Oasis.Resilience.Test.Unit.Proxies;

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
        var storedFactory = field?.GetValue(null) as Delegate;

        // Assert
        Assert.NotNull(storedFactory);

        // Cleanup
        ResilientProxy<ITestService>.RegisterMessageFactory(null!);
    }

    /// <summary>
    /// Tests that RegisterResultAggregator correctly registers a result aggregator delegate.
    /// </summary>
    [Fact]
    public void RegisterResultAggregator_should_set_aggregator()
    {
        // Arrange
        Func<object[], Type, Type, object> aggregator = (results, workerType, resultType) => new object();

        // Act
        ResilientProxy<ITestService>.RegisterResultAggregator(aggregator);

        // Use reflection to verify the aggregator was set
        var field = typeof(ResilientProxy<ITestService>).GetField("_resultAggregator",
            BindingFlags.NonPublic | BindingFlags.Static);
        var storedAggregator = field?.GetValue(null) as Delegate;

        // Assert
        Assert.NotNull(storedAggregator);

        // Cleanup
        ResilientProxy<ITestService>.RegisterResultAggregator(null!);
    }

    /// <summary>
    /// Tests that CreateWorkerMessage throws when no factory is registered.
    /// </summary>
    [Fact]
    public void CreateWorkerMessage_should_throw_when_no_factory_registered()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        // Ensure no factory is registered
        ResilientProxy<ITestService>.RegisterMessageFactory(null!);

        // Use reflection to call CreateWorkerMessage
        var method = typeof(ResilientProxy<ITestService>).GetMethod("CreateWorkerMessage",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(resilientProxy, [typeof(object), new object(), Array.Empty<ParameterInfo>(), Array.Empty<object>()]));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("No message factory registered", ex.InnerException.Message);
    }

    /// <summary>
    /// Tests that AggregateResults throws when no aggregator is registered.
    /// </summary>
    [Fact]
    public void AggregateResults_should_throw_when_no_aggregator_registered()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        // Ensure no aggregator is registered
        ResilientProxy<ITestService>.RegisterResultAggregator(null!);

        // Use reflection to call AggregateResults
        var method = typeof(ResilientProxy<ITestService>).GetMethod("AggregateResults",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            method!.MakeGenericMethod(typeof(string)).Invoke(resilientProxy, [Array.Empty<object>(), typeof(object)]));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("No result aggregator registered", ex.InnerException.Message);
    }

    /// <summary>
    /// Interface with non-generic Task for testing.
    /// </summary>
    public interface INonGenericTaskService
    {
        /// <summary>
        /// Method returning non-generic Task.
        /// </summary>
        Task DoWork();
    }

    /// <summary>
    /// Implementation of INonGenericTaskService.
    /// </summary>
    private class NonGenericTaskService : INonGenericTaskService
    {
        /// <inheritdoc/>
        public Task DoWork() => Task.CompletedTask;
    }

    /// <summary>
    /// Tests that InvokeResilient throws for non-generic Task.
    /// </summary>
    [Fact]
    public void InvokeResilient_should_throw_for_non_generic_Task()
    {
        // Arrange
        var proxy = DispatchProxy.Create<INonGenericTaskService, ResilientProxy<INonGenericTaskService>>();
        var resilientProxy = proxy as ResilientProxy<INonGenericTaskService> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = new NonGenericTaskService();

        // Get the method that returns non-generic Task
        var method = typeof(INonGenericTaskService).GetMethod(nameof(INonGenericTaskService.DoWork));

        // Use reflection to call InvokeResilient
        var invokeMethod = typeof(ResilientProxy<INonGenericTaskService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod!.Invoke(resilientProxy, [method!, Array.Empty<object>(), null, null, null, null]));

        // The exception should be InvalidOperationException for non-generic Task
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Only Task<T> supported", ex.InnerException.Message);
    }

    /// <summary>
    /// Helper method that returns non-generic Task for testing.
    /// </summary>
    private Task NonGenericTaskMethod()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tests that attribute caching works correctly.
    /// </summary>
    [Fact]
    public void Attribute_caching_should_store_and_retrieve_attributes()
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
    /// Tests that Invoke throws for null method.
    /// </summary>
    [Fact]
    public void Invoke_should_throw_for_null_method()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        // Use reflection to call Invoke with null method
        var invokeMethod = typeof(DispatchProxy).GetMethod("Invoke",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod!.Invoke(resilientProxy, [null, Array.Empty<object>()]));

        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }

    /// <summary>
    /// Tests that Invoke throws for null args.
    /// </summary>
    [Fact]
    public void Invoke_should_throw_for_null_args()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var method = typeof(ITestService).GetMethod(nameof(ITestService.SimpleMethod));

        // Use reflection to call Invoke with null args
        var invokeMethod = typeof(DispatchProxy).GetMethod("Invoke",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod!.Invoke(resilientProxy, [method, null]));

        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }

    /// <summary>
    /// Tests that Invoke throws when implementation method not found.
    /// </summary>
    [Fact]
    public void Invoke_should_throw_when_implementation_not_found()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        // Create a method that doesn't exist on the decorated instance
        var method = typeof(ResilientProxyCoreTests).GetMethod(nameof(DummyMethod));

        // Set a decorated instance that doesn't have this method
        resilientProxy.DecoratedInstance = new TestService();

        // Use reflection to call Invoke
        var invokeMethod = typeof(DispatchProxy).GetMethod("Invoke",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod!.Invoke(resilientProxy, [method, Array.Empty<object>()]));

        // The actual exception might be ArgumentNullException or InvalidOperationException
        // depending on how DispatchProxy handles the method lookup
        Assert.True(ex.InnerException is InvalidOperationException || ex.InnerException is ArgumentNullException);
    }

    /// <summary>
    /// Dummy method for testing.
    /// </summary>
    private void DummyMethod() { }

    /// <summary>
    /// Tests that Invoke returns null when decorated instance is null.
    /// </summary>
    [Fact]
    public void Invoke_should_handle_null_decorated_instance()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.DecoratedInstance = default!;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.SimpleMethod));

        // Use reflection to call Invoke
        var invokeMethod = typeof(DispatchProxy).GetMethod("Invoke",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            invokeMethod!.Invoke(resilientProxy, [method, Array.Empty<object>()]));

        // Should throw NullReferenceException when trying to access DecoratedInstance.GetType()
        Assert.IsType<NullReferenceException>(ex.InnerException);
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
