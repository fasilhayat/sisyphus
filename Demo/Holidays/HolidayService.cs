namespace Demo.Holidays;

using Oasis.Resilience.Attributes;

/// <summary>
/// Implementation of the holiday service that retrieves holiday data from an external API.
/// </summary>
public class HolidayService : IHolidayService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("https://holidays.api")
    };

    /// <summary>
    /// Retrieves Norwegian holidays for the specified year.
    /// </summary>
    /// <param name="year">The year to retrieve holidays for.</param>
    /// <returns>A task containing the holiday data as a string.</returns>
    [Retry(maxAttempts: 3, initialDelay: 2000)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    public async Task<string> GetNorwegianHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/norway/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Retrieves Danish holidays for the specified year.
    /// </summary>
    /// <param name="year">The year to retrieve holidays for.</param>
    /// <returns>A task containing the holiday data as a string.</returns>
    [Retry(maxAttempts: 3, initialDelay: 2000)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    public async Task<string> GetDanishHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/denmark/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Retrieves holidays for multiple years. The AOP interceptor routes fan-out calls to worker actors instead of this method body.
    /// </summary>
    /// <param name="years">The years to retrieve holidays for.</param>
    /// <param name="country">The country code.</param>
    /// <returns>A task containing a dictionary mapping year to holiday data.</returns>
    [FanOut(workerActorType: typeof(Actors.HolidayWorkerActor), splitParameterName: "years", maxWorkers: 5)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    public async Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country)
    {
        var result = new Dictionary<int, string>();
        foreach (var year in years)
        {
            var response = await Client.GetAsync($"/{country}/{year}");
            response.EnsureSuccessStatusCode();
            result[year] = await response.Content.ReadAsStringAsync();
        }
        return result;
    }
}
