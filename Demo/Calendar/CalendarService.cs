namespace Demo.Calendar;

using Oasis.Resilience.Attributes;

/// <summary>
/// Implements all five resilience patterns against the live Calendara API
/// (http://localhost:8080 · DK / NO / SE · years 1976-2066).
///
/// No synthetic failures are injected here. All failure scenarios in the Retry,
/// Circuit Breaker, and Supervision chapters are driven by stopping and starting
/// the Calendar Docker container during the demo.
/// </summary>
public class CalendarService : ICalendarService
{
    private const string BaseUrl = "http://localhost:8080";
    private const string ApiKey = "Skyw@lker!";

    private static readonly HttpClient Client = CreateClient();

    // -- API methods -----------------------------------------------------------

    /// <summary>
    /// Fetches holidays for a country/year with up to 3 retry attempts and exponential backoff.
    /// </summary>
    [Retry(maxAttempts: 3, initialDelay: 500)]
    public async Task<string> GetHolidaysAsync(string country, int year)
    {
        Log($"[{country}/{year}] Calling Calendar API...");
        var response = await Client.GetAsync($"/v1/calendar/holidays/{country}/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Circuit-breaker guarded fetch.
    /// Opens after 3 exhausted retry sequences; half-opens after 5 s.
    /// </summary>
    [CircuitBreaker(failureThreshold: 3, resetTimeout: 5000)]
    [Retry(maxAttempts: 2, initialDelay: 300)]
    public async Task<string> GetHolidaysWithBreakerAsync(string country, int year)
    {
        Log($"[{country}/{year}] Calling Calendar API (circuit breaker)...");
        var response = await Client.GetAsync($"/v1/calendar/holidays/{country}/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Supervised fetch. Each retry attempt runs inside a BackoffSupervisor-managed actor.
    /// The supervisor restarts the actor with exponential backoff between retry sequences.
    /// </summary>
    [Retry(maxAttempts: 2, initialDelay: 500)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff, maxRetries: 10, backoffMinMs: 500, backoffMaxMs: 2000, randomFactor: 0.0)]
    public async Task<string> GetHolidaysWithSupervisionAsync(string country, int year)
    {
        Log($"[{country}/{year}] Calling Calendar API (supervised actor)...");
        var response = await Client.GetAsync($"/v1/calendar/holidays/{country}/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Fan-out: the proxy splits <paramref name="years"/>, calls this body once per year
    /// across up to 5 parallel worker actors, then merges the partial dictionaries.
    /// </summary>
    [FanOut(splitOn: "years", maxWorkers: 5)]
    public async Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country)
    {
        // The proxy splits the years array and invokes this body once per year in parallel.
        // Each invocation receives a single-element array — years[0] is always the one assigned year.
        Log($"[Worker] {country}/{years[0]} → Calendar API...");
        var response = await Client.GetAsync($"/v1/calendar/holidays/{country}/{years[0]}");
        response.EnsureSuccessStatusCode();
        return new Dictionary<int, string> { [years[0]] = await response.Content.ReadAsStringAsync() };
    }

    // -- Helpers ---------------------------------------------------------------

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(BaseUrl)
        };
        client.DefaultRequestHeaders.Add("X-API-KEY", ApiKey);
        return client;
    }

    private static void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }
}
