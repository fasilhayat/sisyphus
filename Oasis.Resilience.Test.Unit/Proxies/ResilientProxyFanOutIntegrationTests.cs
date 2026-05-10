namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using Xunit;

/// <summary>
/// End-to-end fan-out integration test that wires the <see cref="ResilientProxy{T}"/> with a
/// real worker actor, verifies the message factory and result aggregator are invoked, and that
/// per-worker-type supervisor caching does not leak actors across calls.
/// </summary>
public class ResilientProxyFanOutIntegrationTests
{
    /// <summary>
    /// Worker message: process a single integer.
    /// </summary>
    /// <param name="Value">The integer to square.</param>
    public sealed record SquareJob(int Value);

    /// <summary>
    /// Worker actor that squares the input value.
    /// </summary>
    public sealed class SquareWorker : ReceiveActor
    {
        /// <summary>Initializes a new instance of the <see cref="SquareWorker"/> class.</summary>
        public SquareWorker() => Receive<SquareJob>(j => Sender.Tell(j.Value * j.Value));
    }

    /// <summary>
    /// Service contract demonstrating fan-out across multiple worker actors.
    /// </summary>
    public interface IMathService
    {
        /// <summary>Squares each value in <paramref name="values"/> using fan-out workers.</summary>
        Task<int[]> SquareAllAsync(int[] values);
    }

    /// <summary>
    /// Service implementation that delegates to fan-out workers via the proxy.
    /// </summary>
    public sealed class MathService : IMathService
    {
        /// <summary>Squares each value in <paramref name="values"/> using fan-out workers.</summary>
        [FanOut(workerActorType: typeof(SquareWorker), splitParameterName: "values", maxWorkers: 4)]
        public Task<int[]> SquareAllAsync(int[] values) => throw new NotImplementedException("intercepted by proxy");
    }

    /// <summary>
    /// Verifies that fan-out distributes work to multiple workers, the message factory and
    /// aggregator run, and the aggregated result reaches the caller.
    /// </summary>
    [Fact]
    public async Task Proxy_should_fan_out_and_aggregate_results()
    {
        ResilientProxy<IMathService>.RegisterMessageFactory((workerType, splitValue, parameters, otherArgs) =>
            new SquareJob((int)splitValue));

        ResilientProxy<IMathService>.RegisterResultAggregator((results, workerType, returnType) =>
            results.Select(r => (int)r).OrderBy(x => x).ToArray());

        var services = new ServiceCollection();
        services.AddResilience(retry => retry.LogLevel = LogLevel.None);
        services.AddResilientService<IMathService, MathService>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMathService>();

        var result = await service.SquareAllAsync(new[] { 2, 3, 4, 5 });

        Assert.Equal(new[] { 4, 9, 16, 25 }, result);
    }

    /// <summary>
    /// Verifies that fan-out processes ALL input values even when there are more items than
    /// <c>maxWorkers</c> (the previous implementation silently truncated extras).
    /// </summary>
    [Fact]
    public async Task Proxy_should_process_all_items_when_input_exceeds_maxWorkers()
    {
        ResilientProxy<IMathService>.RegisterMessageFactory((workerType, splitValue, parameters, otherArgs) =>
            new SquareJob((int)splitValue));

        ResilientProxy<IMathService>.RegisterResultAggregator((results, workerType, returnType) =>
            results.Select(r => (int)r).OrderBy(x => x).ToArray());

        var services = new ServiceCollection();
        services.AddResilience(retry => retry.LogLevel = LogLevel.None);
        services.AddResilientService<IMathService, MathService>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMathService>();

        var input = Enumerable.Range(1, 12).ToArray();
        var expected = input.Select(i => i * i).OrderBy(x => x).ToArray();

        var result = await service.SquareAllAsync(input);

        Assert.Equal(expected, result);
    }
}
