namespace Demo.Holidays;

using Oasis.Resilience.Attributes;

public class HolidayService : IHolidayService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("https://holidays.api")
    };

    public async Task<string> GetNorwegianHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/norway/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetDanishHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/denmark/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // AOP interceptor won't call this directly for FanOut
    // Instead, it uses this as a template for worker actors
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
