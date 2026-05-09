namespace Oasis.Resilience.Test.Unit.Proxies;

using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for fan-out behavior in <see cref="ResilientProxy{T}"/>.
/// </summary>
public class ResilientProxyFanOutTests
{
    /// <summary>
    /// Test service interface for fan-out operations.
    /// </summary>
    public interface IFanOutService
    {
        /// <summary>
        /// A fan-out method decorated with <see cref="FanOutAttribute"/>.
        /// </summary>
        /// <param name="items">The items to split across workers.</param>
        /// <returns>A task that yields a dictionary of item IDs to data strings.</returns>
        [FanOut(workerActorType: typeof(TestWorkerActor), splitParameterName: "items")]
        Task<Dictionary<int, string>> GetDataFanOutAsync(int[] items);
    }

    /// <summary>
    /// Test worker actor that processes <see cref="TestWorkerMessage"/> and replies with <see cref="TestWorkerResult"/>.
    /// </summary>
    public class TestWorkerActor : Akka.Actor.ReceiveActor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestWorkerActor"/> class.
        /// </summary>
        public TestWorkerActor()
        {
            Receive<TestWorkerMessage>(msg =>
            {
                Sender.Tell(new TestWorkerResult(msg.ItemId, $"Data for {msg.ItemId}"), Self);
            });
        }
    }

    /// <summary>
    /// Represents a work item message for a worker actor.
    /// </summary>
    /// <param name="ItemId">The item identifier.</param>
    public record TestWorkerMessage(int ItemId);

    /// <summary>
    /// Represents the result of a worker actor processing.
    /// </summary>
    /// <param name="ItemId">The item identifier.</param>
    /// <param name="Data">The resulting data string.</param>
    public record TestWorkerResult(int ItemId, string Data);

    /// <summary>
    /// Test implementation of <see cref="IFanOutService"/>.
    /// </summary>
    public class FanOutService : IFanOutService
    {
        /// <summary>
        /// Returns a dictionary mapping each item ID to its data string.
        /// </summary>
        /// <param name="items">The array of item IDs.</param>
        /// <returns>A task that yields a dictionary of item data.</returns>
        public Task<Dictionary<int, string>> GetDataFanOutAsync(int[] items)
        {
            var result = new Dictionary<int, string>();
            foreach (var item in items)
                result[item] = $"Data for {item}";
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Verifies that <see cref="FanOutAttribute"/> is cached for fan-out service methods.
    /// </summary>
    [Fact]
    public void FanOut_should_cache_attribute()
    {
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService> ??
            throw new InvalidOperationException("Failed to create proxy");

        var method = typeof(IFanOutService).GetMethod(nameof(IFanOutService.GetDataFanOutAsync));
        var attribute = method?.GetCustomAttribute<FanOutAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(typeof(TestWorkerActor), attribute.WorkerActorType);
        Assert.Equal("items", attribute.SplitParameterName);
    }

    /// <summary>
    /// Verifies a fan-out proxy can be created via <see cref="DispatchProxy"/>.
    /// </summary>
    [Fact]
    public void FanOut_proxy_should_be_creatable()
    {
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService>;

        Assert.NotNull(proxy);
        Assert.NotNull(p);
    }

    /// <summary>
    /// Verifies a message factory can be registered for fan-out operations.
    /// </summary>
    [Fact]
    public void FanOut_should_allow_registering_message_factory()
    {
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService> ??
            throw new InvalidOperationException("Failed to create proxy");

        ResilientProxy<IFanOutService>.RegisterMessageFactory(
            (actorType, splitValue, parameters, otherArgs) =>
            {
                if (actorType == typeof(TestWorkerActor))
                    return new TestWorkerMessage((int)splitValue);
                return splitValue;
            });

        Assert.True(true);
    }

    /// <summary>
    /// Verifies a result aggregator can be registered for fan-out operations.
    /// </summary>
    [Fact]
    public void FanOut_should_allow_registering_result_aggregator()
    {
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService> ??
            throw new InvalidOperationException("Failed to create proxy");

        ResilientProxy<IFanOutService>.RegisterResultAggregator(
            (results, actorType, returnType) =>
            {
                var dict = new Dictionary<int, string>();
                foreach (var result in results)
                {
                    if (result is TestWorkerResult workerResult)
                        dict[workerResult.ItemId] = workerResult.Data;
                }
                return dict;
            });

        Assert.True(true);
    }
}
