namespace Demo.Holidays;

using Oasis.Resilience.Attributes;

/// <summary>
/// Demonstrates <see cref="FanOutAttribute"/>: <see cref="GetHolidaysForYearsAsync"/> receives an
/// array of years, which the proxy splits and dispatches as parallel per-item invocations
/// (one per year), then merges the partial <c>Dictionary&lt;int,string&gt;</c> results automatically.
/// </summary>
public class HolidayService : IHolidayService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("http://localhost:5080")
    };

    /// <summary>Retrieves Norwegian holidays for a single year with retry and supervision.</summary>
    [Retry(maxAttempts: 3, initialDelay: 500)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    public async Task<string> GetNorwegianHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/holidays/norway/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Retrieves Danish holidays for a single year with retry and supervision.</summary>
    [Retry(maxAttempts: 3, initialDelay: 500)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    public async Task<string> GetDanishHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/holidays/denmark/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Fetches holidays for multiple years in parallel. The proxy intercepts this call, splits
    /// the <paramref name="years"/> array, and invokes this body once per year with a single-element
    /// array. All partial dictionaries are merged automatically into the returned result.
    /// </summary>
    [FanOut(maxWorkers: 4)]
    public async Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country)
    {
        var result = new Dictionary<int, string>();
        foreach (var year in years)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [Worker] Fetching {country}/{year}...");
            Console.ResetColor();

            var response = await Client.GetAsync($"/holidays/{country}/{year}");
            response.EnsureSuccessStatusCode();
            result[year] = await response.Content.ReadAsStringAsync();
        }
        return result;
    }
}
