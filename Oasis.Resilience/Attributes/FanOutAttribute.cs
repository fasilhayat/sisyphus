namespace Oasis.Resilience.Attributes;

/// <summary>
/// Specifies that a method should fan-out work to multiple actor workers, allowing parallel processing of split data.
/// </summary>
/// <remarks>Apply to methods that need to distribute work across multiple actors for parallel processing.</remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FanOutAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the worker actor to spawn for processing.
    /// </summary>
    public Type WorkerActorType { get; }

    /// <summary>
    /// Gets the name of the parameter to split for fan-out distribution.
    /// </summary>
    public string SplitParameterName { get; }

    /// <summary>
    /// Gets the maximum number of worker actors to spawn.
    /// </summary>
    public int MaxWorkers { get; }

    /// <summary>
    /// Initializes a new instance of the FanOutAttribute class.
    /// </summary>
    /// <param name="workerActorType">The type of worker actor to spawn.</param>
    /// <param name="splitParameterName">The name of the parameter to split for distribution.</param>
    /// <param name="maxWorkers">The maximum number of workers. Default is 5.</param>
    /// <exception cref="ArgumentNullException">Thrown when workerActorType or splitParameterName is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxWorkers is less than 1.</exception>
    public FanOutAttribute(
        Type workerActorType,
        string splitParameterName,
        int maxWorkers = 5)
    {
        ArgumentNullException.ThrowIfNull(workerActorType);
        ArgumentNullException.ThrowIfNull(splitParameterName);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWorkers, 1);

        WorkerActorType = workerActorType;
        SplitParameterName = splitParameterName;
        MaxWorkers = maxWorkers;
    }
}
