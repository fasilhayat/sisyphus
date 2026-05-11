namespace Oasis.Resilience.Attributes;

/// <summary>
/// Specifies that a method should fan-out work across parallel invocations, one per item in the
/// split array parameter. The proxy calls the implementation once per item with a single-element
/// array and merges the results automatically.
/// </summary>
/// <remarks>
/// <para>
/// The implementation method body <b>must</b> be able to handle a single-element array — it will
/// be called once per item, not with the full array. Results are merged by the proxy using
/// built-in aggregation rules: <c>Dictionary&lt;TKey,TValue&gt;</c> entries are merged,
/// <c>T[]</c> and <c>List&lt;T&gt;</c> elements are concatenated.
/// </para>
/// <para>
/// When the method has exactly one array parameter, <see cref="SplitOn"/> may be omitted and the
/// proxy auto-detects the parameter. Specify <see cref="SplitOn"/> when the method has more than
/// one array parameter.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FanOutAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the array parameter to split across parallel invocations.
    /// When <c>null</c> the proxy auto-detects the single array parameter on the method.
    /// </summary>
    public string? SplitOn { get; }

    /// <summary>
    /// Gets the maximum number of concurrent invocations. Use <c>-1</c> (default) to inherit from
    /// <see cref="FanOutOptions.DefaultMaxWorkers"/>.
    /// </summary>
    public int MaxWorkers { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FanOutAttribute"/> class.
    /// </summary>
    /// <param name="splitOn">
    /// Name of the array parameter to split. Omit when the method has exactly one array parameter.
    /// </param>
    /// <param name="maxWorkers">
    /// Maximum concurrent invocations. Use <c>-1</c> (default) to inherit the global default.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an explicit <paramref name="maxWorkers"/> value is less than 1.
    /// </exception>
    public FanOutAttribute(
        string? splitOn = null,
        int maxWorkers = AttributeDefaults.UnsetInt)
    {
        if (maxWorkers != AttributeDefaults.UnsetInt)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxWorkers, 1);

        SplitOn = splitOn;
        MaxWorkers = maxWorkers;
    }
}
