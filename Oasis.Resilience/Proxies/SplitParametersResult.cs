namespace Oasis.Resilience.Proxies;

/// <summary>
/// Holds the result of extracting split parameters from a method call.
/// </summary>
internal class SplitParametersResult
{
    /// <summary>
    /// Split values extracted from the method parameters. Each value in this array corresponds to a set of parameters that will be used for an individual operation in a fan-out scenario. The proxy will use these values to create multiple operations, each with its own set of parameters derived from the original method call. 
    /// This allows for parallel processing of multiple operations based on the original input parameters.
    /// </summary>
    public required Array SplitValues { get; init; }

    /// <summary>
    /// Other arguments that are not part of the split values but are still needed for each operation. These arguments will be passed along with each set of split parameters when invoking the operations. 
    /// This allows for additional context or configuration to be included in each operation, even though they are not part of the splitting logic.
    /// </summary>
    public required object[] OtherArgs { get; init; }
}
