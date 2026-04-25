using Demo.Calendar;
using Oasis.Resilience.Attributes;

/// <summary>
/// Provides methods for interacting with calendar-related backend services.
/// </summary>
public class CalendarService : ICalendarService
{
    /// <summary>
    /// Provides a static instance of HttpClient configured with a base address of http://localhost:8080.
    /// </summary>
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("http://localhost:8080")
    };

    /// <summary>
    /// Asynchronously retrieves Danish public holidays for the year 2001 from the calendar backend.
    /// </summary>
    [Retry(maxAttempts: 2, initialDelaySeconds: 2)]
    public async Task<string> GetDanishHolidaysAsync()
    {
        Console.WriteLine("Calling DanishHolidays backend...");
        return await GetHolidaysAsync("/v1/calendar/holidays/DK/2001");
    }

    /// <summary>
    /// Asynchronously retrieves Norwegian public holidays for the year 2023 from the calendar backend.
    /// </summary>
    [Retry(maxAttempts: 8, initialDelaySeconds: 2)]
    public async Task<string> GetNorwegianHolidaysAsync()
    {
        Console.WriteLine("Calling Norwegian backend...");
        return await GetHolidaysAsync("/v1/calendar/holidays/NO/2023");
    }

    /// <summary>
    /// Shared HTTP execution logic for holiday endpoints.
    /// </summary>
    private async Task<string> GetHolidaysAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddDefaultHeaders(request);

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Centralized request headers for calendar API calls.
    /// </summary>
    private void AddDefaultHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("accept", "*/*");
        request.Headers.Add("X-API-KEY", "Skyw@lker!");
    }
}
