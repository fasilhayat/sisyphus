namespace Oasis.Resilience.Proxies;

using Attributes;

/// <summary>
/// Holds configuration parameters for fan-out operations.
/// </summary>
internal class FanOutParameters
{
    /// <summary>
    /// Maximum number of concurrent workers to use for processing the fan-out operations. This limits the degree of parallelism and helps control resource usage.
    /// </summary>
    public int MaxWorkers { get; init; }

    /// <summary>
    /// Supervision strategy to apply to the worker actors handling the fan-out operations. This determines how failures in individual workers are handled (e.g., restart, stop, escalate) and helps ensure resilience in the face of errors.
    /// </summary>
    public SupervisionStrategy Strategy { get; init; }

    /// <summary>
    /// Backoff parameters for retrying failed operations in the fan-out workers. In milliseconds, this is the minimum delay before the first retry attempt. The actual delay will be calculated using an exponential backoff strategy, starting from BackoffMinMs and increasing up to BackoffMaxMs, with some randomization applied based on the RandomFactor to avoid thundering herd problems.
    /// These parameters control the delay between retry attempts and help prevent overwhelming the system with rapid retries in case of transient failures.
    /// </summary>
    public int BackoffMinMs { get; init; }

    /// <summary>
    /// Backoff parameters for retrying failed operations in the fan-out workers. 
    /// In milliseconds, this is the maximum delay between retry attempts. The actual delay will be calculated using an exponential backoff strategy, starting from BackoffMinMs and increasing up to BackoffMaxMs, with some randomization applied based on the RandomFactor to avoid thundering herd problems.
    /// </summary>
    public int BackoffMaxMs { get; init; }

    /// <summary>
    /// Gets the randomization factor used in calculations.
    /// </summary>
    public double RandomFactor { get; init; }
}
