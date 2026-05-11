namespace Demo.Pipeline;

/// <summary>Defines the contract for the data pipeline service.</summary>
public interface IDataPipelineService
{
    /// <summary>Runs the data pipeline, returning a status summary on success.</summary>
    Task<string> ProcessAsync();
}
