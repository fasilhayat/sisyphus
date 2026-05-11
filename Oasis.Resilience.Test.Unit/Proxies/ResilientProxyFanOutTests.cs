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
        /// A fan-out method — auto-detects the single array parameter.
        /// </summary>
        [FanOut(maxWorkers: 4)]
        Task<Dictionary<int, string>> GetDataFanOutAsync(int[] items);

        /// <summary>
        /// A fan-out method with an explicit splitOn to disambiguate two array parameters.
        /// </summary>
        [FanOut(splitOn: "items", maxWorkers: 2)]
        Task<Dictionary<int, string>> GetDataExplicitAsync(int[] items, string[] filters);
    }

    /// <summary>
    /// Test implementation of <see cref="IFanOutService"/>.
    /// </summary>
    public class FanOutService : IFanOutService
    {
        /// <inheritdoc/>
        [FanOut(maxWorkers: 4)]
        public Task<Dictionary<int, string>> GetDataFanOutAsync(int[] items)
        {
            var result = new Dictionary<int, string>();
            foreach (var item in items)
                result[item] = $"Data for {item}";
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        [FanOut(splitOn: "items", maxWorkers: 2)]
        public Task<Dictionary<int, string>> GetDataExplicitAsync(int[] items, string[] filters)
        {
            var result = new Dictionary<int, string>();
            foreach (var item in items)
                result[item] = $"Data for {item}";
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Verifies that <see cref="FanOutAttribute"/> with auto-detect is cached for fan-out service methods.
    /// </summary>
    [Fact]
    public void FanOut_should_cache_attribute()
    {
        var method = typeof(IFanOutService).GetMethod(nameof(IFanOutService.GetDataFanOutAsync));
        var attribute = method?.GetCustomAttribute<FanOutAttribute>();

        Assert.NotNull(attribute);
        Assert.Null(attribute!.SplitOn);
        Assert.Equal(4, attribute.MaxWorkers);
    }

    /// <summary>
    /// Verifies that a fan-out proxy can be created via <see cref="DispatchProxy"/>.
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
    /// Verifies that an explicit <c>splitOn</c> attribute is stored and readable via reflection.
    /// </summary>
    [Fact]
    public void FanOut_explicit_splitOn_should_be_stored()
    {
        var method = typeof(IFanOutService).GetMethod(nameof(IFanOutService.GetDataExplicitAsync));
        var attribute = method?.GetCustomAttribute<FanOutAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("items", attribute!.SplitOn);
        Assert.Equal(2, attribute.MaxWorkers);
    }
}
