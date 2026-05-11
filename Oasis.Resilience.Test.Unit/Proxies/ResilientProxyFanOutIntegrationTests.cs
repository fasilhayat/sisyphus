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
}
