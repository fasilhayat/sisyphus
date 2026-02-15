namespace ResilienceWithAop;

public class TiwazService : ITiwazService
{
    private readonly HttpClient _client = new()
    {
        BaseAddress = new Uri("https://tiwaz.hayatnet.local")
    };

    public async Task<string> GetBondsAsync()
    {
        Console.WriteLine("Calling API...");

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/bonds");
        request.Headers.Add("accept", "*/*");
        request.Headers.Add("X-API-KEY", "Skyw@lker!");

        var response = await _client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine("Success");

        return body;
    }
}
