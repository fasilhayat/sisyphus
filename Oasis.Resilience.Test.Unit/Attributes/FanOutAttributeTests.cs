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
    /// Tests that constructor throws when workerActorType is null.
    /// </summary>
    [Fact]
    public void Constructor_should_throw_when_workerActorType_is_null()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new FanOutAttribute(null!, "param"));
        Assert.Equal("workerActorType", ex.ParamName);
    }

    /// <summary>
    /// Tests that constructor throws when splitParameterName is null.
    /// </summary>
    [Fact]
    public void Constructor_should_throw_when_splitParameterName_is_null()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new FanOutAttribute(typeof(object), null!));
        Assert.Equal("splitParameterName", ex.ParamName);
    }

    /// <summary>
    /// Tests that constructor throws when maxWorkers is less than 1.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_should_throw_when_maxWorkers_less_than_one(int invalidMaxWorkers)
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new FanOutAttribute(typeof(object), "param", invalidMaxWorkers));
        Assert.Equal("maxWorkers", ex.ParamName);
    }

    /// <summary>
    /// Tests that default constructor values are applied correctly.
    /// </summary>
    [Fact]
    public void Default_constructor_should_set_default_values()
    {
        // Arrange & Act
        var attribute = new FanOutAttribute(typeof(string), "test");

        // Assert
        Assert.Equal(typeof(string), attribute.WorkerActorType);
        Assert.Equal("test", attribute.SplitParameterName);
        Assert.Equal(5, attribute.MaxWorkers);
    }

    /// <summary>
    /// Tests that custom maxWorkers value is applied correctly.
    /// </summary>
    [Fact]
    public void Custom_maxWorkers_should_override_default()
    {
        // Arrange & Act
        var attribute = new FanOutAttribute(typeof(int), "data", maxWorkers: 10);

        // Assert
        Assert.Equal(10, attribute.MaxWorkers);
    }

    /// <summary>
    /// Tests that attribute can be retrieved from method using reflection.
    /// </summary>
    [Fact]
    public void Attribute_should_be_retrievable_via_reflection()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.FanOutMethod));

        // Act
        var attribute = method?.GetCustomAttribute<FanOutAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(typeof(string), attribute!.WorkerActorType);
        Assert.Equal("years", attribute.SplitParameterName);
    }

    /// <summary>
    /// Test service class for reflection tests.
    /// </summary>
    private class TestService
    {
        [FanOut(workerActorType: typeof(string), splitParameterName: "years")]
        public void FanOutMethod(int[] years) { }
    }
}
