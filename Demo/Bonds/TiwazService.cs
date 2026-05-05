namespace Demo.Bonds;

using Oasis.Resilience.Attributes;

/// <summary>
/// Implements the Tiwaz service for retrieving bonds data with resilience support.
/// </summary>
public class TiwazService : ITiwazService
{
    /// <summary>
    /// Provides a static HTTP client configured with the Tiwaz service base address.
    /// </summary>
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("https://tiwaz.hayatnet.local")
    };

    /// <summary>
    /// Retrieves bonds data from the Tiwaz service.
    /// Protected by retry logic (5 attempts, 2ms delay).
    /// </summary>
    /// <returns>A string containing the bonds data.</returns>
    [Retry(maxAttempts: 5, initialDelay: 2)]
    public async Task<string> GetBondsAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/bonds");
        req.Headers.Add("accept", "*/*");
        req.Headers.Add("X-API-KEY", "Skyw@lker!");

        var response = await Client.SendAsync(req);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}