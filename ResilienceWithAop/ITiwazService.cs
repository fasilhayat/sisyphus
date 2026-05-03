namespace ResilienceWithAop;

/// <summary>
/// Defines the contract for the Tiwaz bonds service with resilience support.
/// </summary>
public interface ITiwazService
{
    /// <summary>
    /// Retrieves bonds data from the Tiwaz service asynchronously with retry logic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation containing the bonds data as a string.</returns>
    Task<string> GetBondsAsync();
}