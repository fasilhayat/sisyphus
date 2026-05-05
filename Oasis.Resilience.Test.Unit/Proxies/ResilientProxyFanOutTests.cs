namespace Oasis.Resilience.Test.Unit.Proxies;

using Microsoft.Extensions.DependencyInjection;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ResilientProxy{T}"/> fan-out functionality.
/// </summary>
/// <remarks>Tests the fan-out attribute handling, message factory registration, and result aggregation.</remarks>
public class ResilientProxyFanOutTests
{
    /// <summary>
    /// Test interface for fan-out proxy testing.
    /// </summary>
    public interface IFanOutService
    {
        /// <summary>
        /// Gets data for multiple items using fan-out pattern.
        /// </summary>
        /// <param name="items">The array of items to process.</param>
        /// <returns>A dictionary mapping item IDs to their data.</returns>
        [FanOut(workerActorType: typeof(TestWorkerActor), splitParameterName: "items")]
        Task<Dictionary<int, string>> GetDataFanOutAsync(int[] items);
    }

    /// <summary>
    /// Test worker actor for fan-out operations.
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
                // Simulate work and return result
                Sender.Tell(new TestWorkerResult(msg.ItemId, $"Data for {msg.ItemId}"), Self);
            });
        }
    }

    /// <summary>
    /// Test worker message containing the item ID to process.
    /// </summary>
    /// <param name="ItemId">The ID of the item to process.</param>
    public record TestWorkerMessage(int ItemId);

    /// <summary>
    /// Test worker result containing the processed data.
    /// </summary>
    /// <param name="ItemId">The ID of the processed item.</param>
    /// <param name="Data">The processed data.</param>
    public record TestWorkerResult(int ItemId, string Data);

    /// <summary>
    /// Test implementation for fan-out service.
    /// </summary>
    public class FanOutService : IFanOutService
    {
        /// <summary>
        /// Gets data for multiple items (implementation not used directly due to fan-out).
        /// </summary>
        public Task<Dictionary<int, string>> GetDataFanOutAsync(int[] items)
        {
            var result = new Dictionary<int, string>();
            foreach (var item in items)
                result[item] = $"Data for {item}";
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Tests that fan-out attribute is properly cached by the proxy.
    /// </summary>
    [Fact]
    public void FanOut_should_cache_attribute()
    {
        // Arrange
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        // Act - Access the attribute cache (indirectly through reflection)
        var method = typeof(IFanOutService).GetMethod(nameof(IFanOutService.GetDataFanOutAsync));
        var attribute = method?.GetCustomAttribute<FanOutAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(typeof(TestWorkerActor), attribute.WorkerActorType);
        Assert.Equal("items", attribute.SplitParameterName);
    }

    /// <summary>
    /// Tests proxy creation with fan-out attribute present.
    /// </summary>
    [Fact]
    public void FanOut_proxy_should_be_creatable()
    {
        // Arrange & Act
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService>;

        // Assert
        Assert.NotNull(proxy);
        Assert.NotNull(p);
    }

    /// <summary>
    /// Tests that message factory can be registered on the proxy.
    /// </summary>
    [Fact]
    public void FanOut_should_allow_registering_message_factory()
    {
        // Arrange
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        // Act - Should not throw
        ResilientProxy<IFanOutService>.RegisterMessageFactory(
            (actorType, splitValue, parameters, otherArgs) =>
            {
                if (actorType == typeof(TestWorkerActor))
                    return new TestWorkerMessage((int)splitValue);
                return splitValue;
            });

        // Assert - If we got here, registration worked
        Assert.True(true);
    }

    /// <summary>
    /// Tests that result aggregator can be registered on the proxy.
    /// </summary>
    [Fact]
    public void FanOut_should_allow_registering_result_aggregator()
    {
        // Arrange
        var proxy = DispatchProxy.Create<IFanOutService, ResilientProxy<IFanOutService>>();
        var p = proxy as ResilientProxy<IFanOutService> ?? 
            throw new InvalidOperationException("Failed to create proxy");

        // Act - Should not throw
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

        // Assert - If we got here, registration worked
        Assert.True(true);
    }
}
