namespace Demo.Calendar;

using Oasis.Resilience.Attributes;

/// <summary>
/// Demonstrates parallel <see cref="RetryAttribute"/> chains: both DK and NO calendar calls run
/// concurrently via <c>Task.WhenAll</c>, each maintaining an independent retry state.
/// </summary>
public class CalendarService : ICalendarService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("http://localhost:5080")
    };

    /// <summary>Retrieves Danish public holidays. Retries up to 2 times with 500ms initial backoff.</summary>
    [Retry(maxAttempts: 2, initialDelay: 500)]
    public async Task<string> GetDanishHolidaysAsync()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [DK] Calling calendar backend...");
        Console.ResetColor();

        var response = await Client.GetAsync("/calendar/dk");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Retrieves Norwegian public holidays. Retries up to 3 times with 500ms initial backoff.</summary>
    [Retry(maxAttempts: 3, initialDelay: 500)]
    public async Task<string> GetNorwegianHolidaysAsync()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [NO] Calling calendar backend...");
        Console.ResetColor();

        var response = await Client.GetAsync("/calendar/no");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
