namespace Oasis.Resilience.Proxies;

/// <summary>
/// Holds the result of extracting split parameters from a method call.
/// </summary>
internal class SplitParametersResult
{
    public required Array SplitValues { get; init; }
    public required object[] OtherArgs { get; init; }
}
