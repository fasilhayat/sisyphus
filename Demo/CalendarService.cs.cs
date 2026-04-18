using Demo;
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
    /// <returns>A task that represents the asynchronous operation. The task result contains the response content as a string.</returns>
    [Resilient(maxAttempts: 10, initialDelaySeconds: 2)]
    public async Task<string> GetDanishHolidaysAsync()
    {
        Console.WriteLine("Calling calendar backend...");
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/calendar/holidays/DK/2001");
        request.Headers.Add("accept","*/*");
        request.Headers.Add("X-API-KEY", "Skyw@lker!");

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
}
