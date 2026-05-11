namespace Oasis.Resilience.Test.Unit.Attributes;

using System.Reflection;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="FanOutAttribute"/>.
/// </summary>
public class FanOutAttributeTests
{
    /// <summary>
    /// Tests that constructor throws when maxWorkers is less than 1 (the sentinel <c>-1</c> is permitted).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Constructor_should_throw_when_maxWorkers_less_than_one(int invalidMaxWorkers)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new FanOutAttribute(maxWorkers: invalidMaxWorkers));
        Assert.Equal("maxWorkers", ex.ParamName);
    }

    /// <summary>
    /// Tests that the default constructor leaves <c>SplitOn</c> null and <c>MaxWorkers</c> at the sentinel value.
    /// </summary>
    [Fact]
    public void Default_constructor_should_set_default_values()
    {
        var attribute = new FanOutAttribute();

        Assert.Null(attribute.SplitOn);
        Assert.Equal(AttributeDefaults.UnsetInt, attribute.MaxWorkers);
    }

    /// <summary>
    /// Tests that an explicit <c>splitOn</c> value is stored correctly.
    /// </summary>
    [Fact]
    public void Constructor_should_store_splitOn()
    {
        var attribute = new FanOutAttribute(splitOn: "years");

        Assert.Equal("years", attribute.SplitOn);
    }

    /// <summary>
    /// Tests that a custom <c>maxWorkers</c> value is applied correctly.
    /// </summary>
    [Fact]
    public void Custom_maxWorkers_should_override_default()
    {
        var attribute = new FanOutAttribute(maxWorkers: 10);

        Assert.Equal(10, attribute.MaxWorkers);
    }

    /// <summary>
    /// Tests that the attribute can be retrieved from a method via reflection.
    /// </summary>
    [Fact]
    public void Attribute_should_be_retrievable_via_reflection()
    {
        var method = typeof(TestService).GetMethod(nameof(TestService.FanOutMethod));
        var attribute = method?.GetCustomAttribute<FanOutAttribute>();

        Assert.NotNull(attribute);
        Assert.Null(attribute!.SplitOn);
        Assert.Equal(4, attribute.MaxWorkers);
    }

    /// <summary>
    /// Tests that the attribute with an explicit splitOn is retrievable via reflection.
    /// </summary>
    [Fact]
    public void Attribute_with_splitOn_should_be_retrievable_via_reflection()
    {
        var method = typeof(TestService).GetMethod(nameof(TestService.FanOutMethodExplicit));
        var attribute = method?.GetCustomAttribute<FanOutAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("years", attribute!.SplitOn);
    }

    /// <summary>Test service class for reflection tests.</summary>
    private class TestService
    {
        [FanOut(maxWorkers: 4)]
        public void FanOutMethod(int[] years) { }

        [FanOut(splitOn: "years")]
        public void FanOutMethodExplicit(int[] years, string[] countries) { }
    }
}
