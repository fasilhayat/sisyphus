namespace Oasis.Resilience.Test.Unit.Proxies;

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
    /// <summary>Creates a proxy configured for fan-out operations.</summary>
    private object CreateFanOutProxy()
    {
        var actorSystem = CreateActorSystem();
        var proxy = DispatchProxy.Create<ITestService, ResilientProxy<ITestService>>();
        var resilientProxy = proxy as ResilientProxy<ITestService> ??
            throw new InvalidOperationException("Failed to create proxy");

        resilientProxy.ActorSystem = actorSystem;
        resilientProxy.FanOutOptions = new FanOutOptions { DefaultMaxWorkers = 2 };
        resilientProxy.DecoratedInstance = new TestServiceImpl();
        return proxy!;
    }

    /// <summary>
    /// Verifies that the proxy throws when the named split parameter is not found on the method.
    /// </summary>
    [Fact]
    public async Task HandleFanOut_should_throw_when_split_parameter_not_found()
    {
        var proxy = CreateFanOutProxy();
        var resilientProxy = (ResilientProxy<ITestService>)(object)proxy;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData));
        var fanOutAttr = new FanOutAttribute(splitOn: "NonExistentParam", maxWorkers: 2);

        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task)invokeMethod!.Invoke(resilientProxy, [method!, new object[] { new int[] { 1, 2, 3 } }, null!, null!, null!, fanOutAttr])!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("not found", ex.Message);
    }

    /// <summary>
    /// Verifies that <see cref="FanOutAttribute"/> is correctly read from method metadata
    /// and that <c>SplitOn</c> is null when auto-detect is used.
    /// </summary>
    [Fact]
    public void HandleFanOut_should_read_attribute_from_method()
    {
        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData));
        var fanOutAttr = method?.GetCustomAttribute<FanOutAttribute>();

        Assert.NotNull(fanOutAttr);
        Assert.Null(fanOutAttr!.SplitOn);
        Assert.Equal(2, fanOutAttr.MaxWorkers);
    }

    /// <summary>
    /// Verifies that the proxy throws when the method has no array parameter for fan-out auto-detect.
    /// </summary>
    [Fact]
    public async Task HandleFanOut_should_throw_when_no_array_parameter()
    {
        var proxy = CreateFanOutProxy();
        var resilientProxy = (ResilientProxy<ITestService>)(object)proxy;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.NoArrayMethod));
        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task)invokeMethod!.Invoke(resilientProxy, [method!, new object[] { 42 }, null!, null!, null!, new FanOutAttribute(maxWorkers: 2)])!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("no array parameter found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleFanOut_should_throw_when_multiple_array_parameters()
    {
        var proxy = CreateFanOutProxy();
        var resilientProxy = (ResilientProxy<ITestService>)(object)proxy;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.MultiArrayMethod));
        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task)invokeMethod!.Invoke(resilientProxy, [method!, new object[] { new int[] { 1, 2 }, new string[] { "a", "b" } }, null!, null!, null!, new FanOutAttribute(maxWorkers: 2)])!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("found 2 array parameters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleFanOut_should_throw_when_worker_returns_null()
    {
        var proxy = CreateFanOutProxy();
        var resilientProxy = (ResilientProxy<ITestService>)(object)proxy;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.NullReturningMethod));
        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task)invokeMethod!.Invoke(resilientProxy, [method!, new object[] { new int[] { 1 } }, null!, null!, null!, new FanOutAttribute(maxWorkers: 2)])!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("returned null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleFanOut_should_work_with_explicit_splitOn()
    {
        var proxy = CreateFanOutProxy();
        var resilientProxy = (ResilientProxy<ITestService>)(object)proxy;

        var method = typeof(ITestService).GetMethod(nameof(ITestService.MultiArrayMethodWorks));
        var invokeMethod = typeof(ResilientProxy<ITestService>).GetMethod("InvokeResilient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task)invokeMethod!.Invoke(resilientProxy, [method!, new object[] { new int[] { 1, 2 }, new string[] { "a", "b" } }, null!, null!, null!, new FanOutAttribute(splitOn: "items", maxWorkers: 2)])!;

        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    /// 
    /// Test service interface for fan-out handle tests.
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// A fan-out method with auto-detected array parameter.
        /// </summary>
        [FanOut(maxWorkers: 2)]
        Task<Dictionary<int, string>> ProcessData(int[] items);

        /// <summary>A method with no array parameter — used to test auto-detect failure.</summary>
        [FanOut(maxWorkers: 2)]
        Task<string> NoArrayMethod(int value);

        /// <summary>A method with two array parameters — used to test ambiguous split detection.</summary>
        [FanOut(maxWorkers: 2)]
        Task<int[]> MultiArrayMethod(int[] items, string[] labels);

        /// <summary>Same as MultiArrayMethod but with explicit splitOn — used to test unambiguous split.</summary>
        [FanOut(splitOn: "items", maxWorkers: 2)]
        Task<int[]> MultiArrayMethodWorks(int[] items, string[] labels);

        /// <summary>A method that returns null — used to test the null result check in fan-out.</summary>
        [FanOut(maxWorkers: 2)]
        Task<string> NullReturningMethod(int[] items);
    }

    /// <summary>
    /// Test implementation that returns one dictionary entry per item.
    /// </summary>
    public class TestServiceImpl : ITestService
    {
        /// <inheritdoc/>
        [FanOut(maxWorkers: 2)]
        public Task<Dictionary<int, string>> ProcessData(int[] items)
        {
            var result = new Dictionary<int, string>();
            foreach (var item in items)
                result[item] = $"Result for {item}";
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        [FanOut(maxWorkers: 2)]
        public Task<string> NoArrayMethod(int value) => Task.FromResult("ok");

        /// <inheritdoc/>
        public Task<int[]> MultiArrayMethod(int[] items, string[] labels) => Task.FromResult(items);

        /// <inheritdoc/>
        public Task<int[]> MultiArrayMethodWorks(int[] items, string[] labels) => Task.FromResult(items);

        /// <inheritdoc/>
        [FanOut(maxWorkers: 2)]
        public Task<string> NullReturningMethod(int[] items) => null!;
    }
}
