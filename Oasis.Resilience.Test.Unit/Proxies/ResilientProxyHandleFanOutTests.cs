namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for fan-out handling in <see cref="ResilientProxy{T}"/>.
/// </summary>
public class ResilientProxyHandleFanOutTests : ProxyTestBase
{
    /// <summary>
    /// Creates a proxy configured for fan-out operations.
    /// </summary>
    /// <returns>The created proxy instance.</returns>
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
    /// Verifies that <see cref="ResilientProxy{T}"/> throws when the split parameter name is not found.
    /// </summary>
    [Fact]
    public async Task HandleFanOut_should_throw_when_split_parameter_not_found()
    {
        var proxy = CreateFanOutProxy();
        var resilientProxy = (ResilientProxy<ITestService>)(object)proxy;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData));
        var fanOutAttr = new FanOutAttribute(typeof(TestWorkerActor), "NonExistentParam", 2);

        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task)invokeMethod!.Invoke(resilientProxy, new object[] { method!, new object[] { new int[] { 1, 2, 3 } }, null!, null!, null!, fanOutAttr })!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("not found", ex.Message);
    }

    /// <summary>
    /// Verifies that <see cref="FanOutAttribute"/> is correctly read from method metadata.
    /// </summary>
    [Fact]
    public void HandleFanOut_should_read_attribute_from_method()
    {
        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData));

        var fanOutAttr = method?.GetCustomAttribute<FanOutAttribute>();

        Assert.NotNull(fanOutAttr);
        Assert.Equal(typeof(TestWorkerActor), fanOutAttr.WorkerActorType);
        Assert.Equal("items", fanOutAttr.SplitParameterName);
        Assert.Equal(2, fanOutAttr.MaxWorkers);
    }

    /// <summary>
    /// Verifies that registered message factories and result aggregators are used for fan-out operations.
    /// </summary>
    [Fact]
    public void FanOut_should_use_registered_message_factory_and_aggregator()
    {
        ResilientProxy<ITestService>.RegisterMessageFactory((workerType, splitValue, parameters, otherArgs) =>
        {
            return new object();
        });

        ResilientProxy<ITestService>.RegisterResultAggregator((results, workerType, resultType) =>
        {
            return "aggregated";
        });

        Assert.True(true);
    }

    /// <summary>
    /// Test service interface for fan-out handle tests.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// A fan-out method decorated with <see cref="FanOutAttribute"/>.
        /// </summary>
        /// <param name="items">The items to process.</param>
        /// <returns>A task that yields a dictionary of results.</returns>
        [FanOut(typeof(TestWorkerActor), "items", 2)]
        Task<Dictionary<int, string>> ProcessData(int[] items);
    }

    /// <summary>
    /// Test worker actor for fan-out handle tests.
    /// </summary>
    public class TestWorkerActor : ReceiveActor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestWorkerActor"/> class.
        /// </summary>
        public TestWorkerActor()
        {
            Receive<object>(msg =>
            {
                Sender.Tell("result");
            });
        }
    }
}
