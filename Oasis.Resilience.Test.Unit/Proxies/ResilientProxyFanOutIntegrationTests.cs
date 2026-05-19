namespace Oasis.Resilience.Test.Unit.Proxies;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// End-to-end fan-out integration tests that wire the <see cref="Proxies.ResilientProxy{T}"/> through
/// the DI stack and verify that the proxy correctly splits, invokes, and aggregates results without
/// any Akka worker actors or manual message/aggregator registration.
/// </summary>
public class ResilientProxyFanOutIntegrationTests
{
    /// <summary>
    /// Contract for a service that squares integers in parallel.
    /// </summary>
    public interface IMathService
    {
        /// <summary>Squares each value in <paramref name="values"/> using fan-out.</summary>
        Task<int[]> SquareAllAsync(int[] values);
    }

    /// <summary>
    /// Implementation — the body works correctly with a single-element array because the proxy
    /// calls it once per item and merges the results.
    /// </summary>
    public sealed class MathService : IMathService
    {
        /// <inheritdoc/>
        [FanOut(maxWorkers: 4)]
        public Task<int[]> SquareAllAsync(int[] values)
            => Task.FromResult(values.Select(v => v * v).ToArray());
    }

    private static IMathService BuildService()
    {
        var services = new ServiceCollection();
        services.AddResilience(retry => retry.LogLevel = LogLevel.None);
        services.AddResilientService<IMathService, MathService>();
        return services.BuildServiceProvider().GetRequiredService<IMathService>();
    }

    /// <summary>
    /// Verifies that fan-out distributes work and the results are aggregated correctly.
    /// </summary>
    [Fact]
    public async Task Proxy_should_fan_out_and_aggregate_results()
    {
        var service = BuildService();

        var result = await service.SquareAllAsync([2, 3, 4, 5]);

        Assert.Equal(4, result.Length);
        Assert.Contains(4, result);
        Assert.Contains(9, result);
        Assert.Contains(16, result);
        Assert.Contains(25, result);
    }

    /// <summary>
    /// Verifies that all items are processed even when the input count exceeds <c>maxWorkers</c>.
    /// This is a regression test for the previous implementation that silently truncated extras.
    /// </summary>
    [Fact]
    public async Task Proxy_should_process_all_items_when_input_exceeds_maxWorkers()
    {
        var service = BuildService();

        var input = Enumerable.Range(1, 12).ToArray();
        var expected = input.Select(i => i * i).ToHashSet();

        var result = await service.SquareAllAsync(input);

        Assert.Equal(12, result.Length);
        Assert.All(result, r => Assert.Contains(r, expected));
    }

    /// <summary>
    /// Verifies that a fan-out over a Dictionary return type merges all partial results.
    /// </summary>
    public interface ILookupService
    {
        /// <summary>Looks up each key in parallel and returns a merged dictionary.</summary>
        Task<Dictionary<int, string>> LookUpAsync(int[] keys);
    }

    /// <summary>Implementation that returns one entry per key.</summary>
    public sealed class LookupService : ILookupService
    {
        /// <inheritdoc/>
        [FanOut(maxWorkers: 3)]
        public Task<Dictionary<int, string>> LookUpAsync(int[] keys)
            => Task.FromResult(keys.ToDictionary(k => k, k => $"value-{k}"));
    }

    /// <summary>
    /// Verifies that partial dictionaries are merged into one result.
    /// </summary>
    [Fact]
    public async Task Proxy_should_merge_dictionary_results()
    {
        var services = new ServiceCollection();
        services.AddResilience(retry => retry.LogLevel = LogLevel.None);
        services.AddResilientService<ILookupService, LookupService>();
        var service = services.BuildServiceProvider().GetRequiredService<ILookupService>();

        var result = await service.LookUpAsync([10, 20, 30]);

        Assert.Equal(3, result.Count);
        Assert.Equal("value-10", result[10]);
        Assert.Equal("value-20", result[20]);
        Assert.Equal("value-30", result[30]);
    }

    /// <summary>
    /// Contract for a service that concatenates strings into a list using fan-out.
    /// </summary>
    public interface IConcatService
    {
        /// <summary>Concats each prefix with a suffix using fan-out.</summary>
        Task<List<string>> ConcatAllAsync(string[] prefixes);
    }

    /// <summary>
    /// Implementation — each fan-out call returns a single-element list.
    /// </summary>
    public sealed class ConcatService : IConcatService
    {
        /// <inheritdoc/>
        [FanOut(maxWorkers: 4)]
        public Task<List<string>> ConcatAllAsync(string[] prefixes)
            => Task.FromResult(prefixes.Select(p => $"{p}-suffix").ToList());
    }

    /// <summary>
    /// Verifies that partial <c>List&lt;T&gt;</c> results are concatenated into one list.
    /// Covers <see cref="Proxies.ResilientProxy{T}.ConcatLists"/>.
    /// </summary>
    [Fact]
    public async Task Proxy_should_concat_list_results_for_fanout()
    {
        var services = new ServiceCollection();
        services.AddResilience(retry => retry.LogLevel = LogLevel.None);
        services.AddResilientService<IConcatService, ConcatService>();
        var service = services.BuildServiceProvider().GetRequiredService<IConcatService>();

        var result = await service.ConcatAllAsync(["a", "b", "c"]);

        Assert.Equal(3, result.Count);
        Assert.Contains("a-suffix", result);
        Assert.Contains("b-suffix", result);
        Assert.Contains("c-suffix", result);
    }

    /// <summary>
    /// Contract for a service whose fan-out worker can fail.
    /// </summary>
    public interface IFaultyFanOutService
    {
        /// <summary>Returns strings but the first item throws.</summary>
        Task<List<string>> GetWithFailureAsync(int[] items);
    }

    /// <summary>
    /// Implementation that throws for item 0.
    /// </summary>
    public sealed class FaultyFanOutService : IFaultyFanOutService
    {
        /// <inheritdoc/>
        [FanOut(maxWorkers: 3)]
        public Task<List<string>> GetWithFailureAsync(int[] items)
        {
            if (items.Length == 1 && items[0] == 0)
                throw new InvalidOperationException("Simulated fan-out worker failure");
            return Task.FromResult(items.Select(i => $"ok-{i}").ToList());
        }
    }

    /// <summary>
    /// Verifies that when a fan-out worker throws, the exception propagates and other
    /// workers are still awaited. Covers <see cref="Proxies.ResilientProxy{T}.InvokeForSingleItemTracked"/> catch block.
    /// </summary>
    [Fact]
    public async Task Proxy_should_propagate_fanout_worker_failure()
    {
        var services = new ServiceCollection();
        services.AddResilience(retry => retry.LogLevel = LogLevel.None);
        services.AddResilientService<IFaultyFanOutService, FaultyFanOutService>();
        var service = services.BuildServiceProvider().GetRequiredService<IFaultyFanOutService>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetWithFailureAsync([0, 1, 2]));
        Assert.Contains("Simulated fan-out worker failure", ex.Message);
    }
}
