namespace Demo.Bonds;

/// <summary>
/// Defines the contract for the Tiwaz bonds service.
/// </summary>
internal interface ITiwazService
{
    /// <summary>
    /// Retrieves bonds data from the Tiwaz service asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation containing the bonds data as a string.</returns>
    Task<string> GetBondsAsync();
}