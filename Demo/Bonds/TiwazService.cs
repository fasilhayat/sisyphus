namespace Demo.Bonds;

using Oasis.Resilience.Attributes;

/// <summary>
/// Implements the Tiwaz service for retrieving bonds data with resilience support.
/// </summary>
public class TiwazService : ITiwazService
{
    private static readonly HttpClient Client = new(new HttpClientHandler { UseProxy = false })
    {
        BaseAddress = new Uri("http://localhost:5080")
    };

    /// <summary>
    /// Retrieves bonds data from the mock server.
    /// Protected by retry logic (3 attempts, 500ms initial backoff).
    /// </summary>
    [Retry(maxAttempts: 3, initialDelay: 500)]
    public async Task<string> GetBondsAsync()
    {
        var response = await Client.GetAsync("/bonds");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}