namespace Demo.Calendar;

using Oasis.Resilience.Attributes;

/// <summary>
/// Demonstrates parallel <see cref="RetryAttribute"/> chains: both DK and NO calendar calls run
/// concurrently via <c>Task.WhenAll</c>, each maintaining an independent retry state.
/// Calls the live Calendara finance-service backend on <c>http://localhost:8080</c>.
/// </summary>
public class CalendarService : ICalendarService
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri("http://localhost:8080") };
        client.DefaultRequestHeaders.Add("X-API-KEY", "Skyw@lker!");
        return client;
    }

    /// <summary>Retrieves Danish public holidays for the current year. Retries up to 2 times with 500ms initial backoff.</summary>
    [Retry(maxAttempts: 2, initialDelay: 500)]
    public async Task<string> GetDanishHolidaysAsync()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [DK] Calling Calendara backend...");
        Console.ResetColor();

        var response = await Client.GetAsync($"/v1/calendar/holidays/DK/{DateTime.UtcNow.Year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Retrieves Norwegian public holidays for the current year. Retries up to 3 times with 500ms initial backoff.</summary>
    [Retry(maxAttempts: 3, initialDelay: 500)]
    public async Task<string> GetNorwegianHolidaysAsync()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [NO] Calling Calendara backend...");
        Console.ResetColor();

        var response = await Client.GetAsync($"/v1/calendar/holidays/NO/{DateTime.UtcNow.Year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
