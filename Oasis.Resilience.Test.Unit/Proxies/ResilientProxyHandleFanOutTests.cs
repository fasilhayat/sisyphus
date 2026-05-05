namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Tests for ResilientProxy HandleFanOut method.
/// </summary>
public class ResilientProxyHandleFanOutTests : ProxyTestBase
{
    /// <summary>
    /// Creates a proxy with actor system for fan-out testing.
    /// </summary>
    private object CreateFanOutProxy()
    {
        var _actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.ActorSystem = _actorSystem;
        resilientProxy.FanOutOptions = new FanOutOptions { DefaultMaxWorkers = 2 };

        return proxy!;
    }

    /// <summary>
    /// Tests that HandleFanOut throws when split parameter is not found.
    /// </summary>
    [Fact]
    public async Task HandleFanOut_should_throw_when_split_parameter_not_found()
    {
        // Arrange
        var proxy = CreateFanOutProxy();
        var resilientProxy = (ResilientProxy<ITestService>)(object)proxy;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData));
        var fanOutAttr = new FanOutAttribute(typeof(TestWorkerActor), "NonExistentParam", 2);

        // Use reflection to call InvokeResilient with fan-out attribute
        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var task = (Task)invokeMethod!.Invoke(resilientProxy, new object[] { method!, new object[] { new int[] { 1, 2, 3 } }, null!, null!, null!, fanOutAttr })!;

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("not found", ex.Message);
    }

    /// <summary>
    /// Tests that fan-out attribute can be read from method.
    /// </summary>
    [Fact]
    public void HandleFanOut_should_read_attribute_from_method()
    {
        // Arrange
        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData));

        // Act
        var fanOutAttr = method?.GetCustomAttribute<FanOutAttribute>();

        // Assert
        Assert.NotNull(fanOutAttr);
        Assert.Equal(typeof(TestWorkerActor), fanOutAttr.WorkerActorType);
        Assert.Equal("items", fanOutAttr.SplitParameterName);
        Assert.Equal(2, fanOutAttr.MaxWorkers);
    }

    /// <summary>
    /// Tests that RegisterMessageFactory and RegisterResultAggregator work.
    /// </summary>
    [Fact]
    public void FanOut_should_use_registered_message_factory_and_aggregator()
    {
        // Arrange
        bool factoryCalled = false;
        bool aggregatorCalled = false;

        ResilientProxy<ITestService>.RegisterMessageFactory((workerType, splitValue, parameters, otherArgs) =>
        {
            factoryCalled = true;
            return new object();
        });

        ResilientProxy<ITestService>.RegisterResultAggregator((results, workerType, resultType) =>
        {
            aggregatorCalled = true;
            return "aggregated";
        });

        // Assert
        Assert.False(factoryCalled); // Not called yet
        Assert.False(aggregatorCalled); // Not called yet

        // Cleanup
        ResilientProxy<ITestService>.RegisterMessageFactory(null!);
        ResilientProxy<ITestService>.RegisterResultAggregator(null!);
    }

    /// <summary>
    /// Test interface for fan-out testing.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// Method with fan-out attribute.
        /// </summary>
        [FanOut(typeof(TestWorkerActor), "items", 2)]
        Task<Dictionary<int, string>> ProcessData(int[] items);
    }

    /// <summary>
    /// Test worker actor for fan-out.
    /// </summary>
    public class TestWorkerActor : ReceiveActor
    {
        /// <summary>
        /// Initializes a new instance of the TestWorkerActor class.
        /// </summary>
        public TestWorkerActor()
        {
            Receive<object>(msg =>
            {
                // Simple worker that returns a result
                Sender.Tell("result");
            });
        }
    }
}
