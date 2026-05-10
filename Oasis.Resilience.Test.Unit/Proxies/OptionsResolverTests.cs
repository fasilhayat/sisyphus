namespace Oasis.Resilience.Test.Unit.Proxies;

using Oasis.Resilience;
using Oasis.Resilience.Attributes;
using Oasis.Resilience.Proxies;
using System.Reflection;
using Xunit;

/// <summary>
/// Unit tests for <see cref="OptionsResolver"/> covering attribute-vs-options fallback semantics.
/// The resolver is internal so these tests use reflection to invoke its public static methods.
/// </summary>
public class OptionsResolverTests
{
    private static readonly Type ResolverType = typeof(ResilientProxy<object>).Assembly
        .GetType("Oasis.Resilience.Proxies.OptionsResolver", throwOnError: true)!;

    private static T Invoke<T>(string method, params object?[] args)
    {
        var info = ResolverType.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!;
        return (T)info.Invoke(null, args)!;
    }

    /// <summary>
    /// Verifies that when retry attribute parameters are unset (sentinel <c>-1</c>), the resolver
    /// falls back to the configured global retry options.
    /// </summary>
    [Fact]
    public void ResolveRetry_should_use_global_options_when_attribute_unset()
    {
        var attribute = new RetryAttribute();
        var options = new RetryOptions
        {
            DefaultMaxAttempts = 7,
            DefaultInitialDelayMs = 1234
        };

        dynamic resolved = Invoke<object>("ResolveRetry", attribute, options);

        Assert.Equal(7, (int)resolved.MaxAttempts);
        Assert.Equal(1234, (int)resolved.InitialDelayMs);
    }

    /// <summary>
    /// Verifies that explicitly supplied attribute values take precedence over global retry options.
    /// </summary>
    [Fact]
    public void ResolveRetry_should_prefer_attribute_values_over_options()
    {
        var attribute = new RetryAttribute(maxAttempts: 2, initialDelay: 50);
        var options = new RetryOptions
        {
            DefaultMaxAttempts = 7,
            DefaultInitialDelayMs = 1234
        };

        dynamic resolved = Invoke<object>("ResolveRetry", attribute, options);

        Assert.Equal(2, (int)resolved.MaxAttempts);
        Assert.Equal(50, (int)resolved.InitialDelayMs);
    }

    /// <summary>
    /// Verifies that fan-out <c>maxWorkers</c> falls back to the configured default when the
    /// attribute is unset (the sentinel <c>-1</c>).
    /// </summary>
    [Fact]
    public void ResolveMaxWorkers_should_use_global_default_when_attribute_unset()
    {
        var attribute = new FanOutAttribute(typeof(string), "param");
        var options = new FanOutOptions { DefaultMaxWorkers = 12 };

        var workers = Invoke<int>("ResolveMaxWorkers", attribute, options);

        Assert.Equal(12, workers);
    }

    /// <summary>
    /// Verifies that explicit fan-out <c>maxWorkers</c> wins over the configured default,
    /// fixing the prior bug where the magic value <c>5</c> was treated as unset.
    /// </summary>
    [Fact]
    public void ResolveMaxWorkers_should_honor_explicit_value_of_five()
    {
        var attribute = new FanOutAttribute(typeof(string), "param", maxWorkers: 5);
        var options = new FanOutOptions { DefaultMaxWorkers = 99 };

        var workers = Invoke<int>("ResolveMaxWorkers", attribute, options);

        Assert.Equal(5, workers);
    }
}
