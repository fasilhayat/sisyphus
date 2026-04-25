namespace Demo.Bonds;

using Oasis.Resilience.Attributes;

public class TiwazService : ITiwazService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri( "https://tiwaz.hayatnet.local")
    };

    [Retry(maxAttempts: 5, initialDelaySeconds: 2)]
    public async Task<string> GetBondsAsync()
    {
        var req =  new HttpRequestMessage(HttpMethod.Get, "/v1/bonds");
        req.Headers.Add("accept", "*/*");
        req.Headers.Add("X-API-KEY", "Skyw@lker!");

        var response = await Client.SendAsync(req);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}