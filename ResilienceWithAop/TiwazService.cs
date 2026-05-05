namespace ResilienceWithAop;

using Oasis.Resilience.Attributes;

/// <summary>
/// Implements the Tiwaz service for retrieving bonds data with AOP-based resilience.
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

    [Retry(maxAttempts: 5, initialDelay: 3000)]
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