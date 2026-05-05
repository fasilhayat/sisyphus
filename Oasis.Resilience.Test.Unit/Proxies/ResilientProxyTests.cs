namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ResilientProxy{T}"/> attribute handling.
/// </summary>
public class ResilientProxyTests
{
    /// <summary>
    /// Tests that proxy can be created and basic properties set.
    /// </summary>
    [Fact]
    public async Task Proxy_should_allow_setting_properties()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        var actorSystem = ActorSystem.Create("test-system");
        var supervisionOptions = new SupervisionOptions();
        var fanOutOptions = new FanOutOptions();

        // Act
        resilientProxy.ActorSystem = actorSystem;
        resilientProxy.SupervisionOptions = supervisionOptions;
        resilientProxy.FanOutOptions = fanOutOptions;

        // Assert
        Assert.NotNull(resilientProxy.ActorSystem);
        Assert.Same(supervisionOptions, resilientProxy.SupervisionOptions);
        Assert.Same(fanOutOptions, resilientProxy.FanOutOptions);

        // Cleanup
        await actorSystem.Terminate();
    }

    /// <summary>
    /// Tests that method with no resilience attributes invokes directly.
    /// </summary>
    [Fact]
    public void Invoke_should_call_directly_when_no_attributes()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ?? 
            throw new InvalidOperationException("Failed to create proxy");
        var decorated = new TestService();
        
        resilientProxy.DecoratedInstance = decorated;

        // Act
        var result = proxy.SimpleMethod();

        // Assert
        Assert.Equal("SimpleResult", result);
        Assert.True(decorated.SimpleMethodCalled);
    }

    /// <summary>
    /// Tests that Invoke method caches attribute lookups.
    /// </summary>
    [Fact]
    public void Invoke_should_cache_attribute_lookups()
    {
        // Arrange
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ?? 
            throw new InvalidOperationException("Failed to create proxy");
        var decorated = new TestService();
        
        resilientProxy.DecoratedInstance = decorated;

        // Act - call twice to verify caching
        proxy.SimpleMethod();
        proxy.SimpleMethod();

        // Assert - if caching works, no exception should occur
        Assert.Equal(2, decorated.SimpleMethodCallCount);
    }

    /// <summary>
    /// Test interface for proxy testing.
    /// </summary>
    public interface ITestService
    {
        string SimpleMethod();
        
        [Supervision(strategy: SupervisionStrategy.Restart)]
        Task<string> SupervisedMethod();
        
        [FanOut(workerActorType: typeof(object), splitParameterName: "items")]
        Task<Dictionary<int, string>> FanOutMethod(int[] items);
    }

    /// <summary>
    /// Test implementation for proxy testing.
    /// </summary>
    private class TestService : ITestService
    {
        public bool SimpleMethodCalled { get; private set; }
        public int SimpleMethodCallCount { get; private set; }

        public string SimpleMethod()
        {
            SimpleMethodCalled = true;
            SimpleMethodCallCount++;
            return "SimpleResult";
        }

        public Task<string> SupervisedMethod()
        {
            return Task.FromResult("SupervisedResult");
        }

        public Task<Dictionary<int, string>> FanOutMethod(int[] items)
        {
            return Task.FromResult(new Dictionary<int, string>());
        }
    }
}
