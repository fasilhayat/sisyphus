namespace Oasis.Resilience.Proxies;

using Attributes;

/// <summary>
/// Holds configuration parameters for fan-out operations.
/// </summary>
internal class FanOutParameters
{
    public int MaxWorkers { get; init; }
    public SupervisionStrategy Strategy { get; init; }
    public int BackoffMinMs { get; init; }
    public int BackoffMaxMs { get; init; }
    public double RandomFactor { get; init; }
}
