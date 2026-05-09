namespace Demo.Holidays;

using Oasis.Resilience.Attributes;

/// <summary>
/// Defines the contract for holiday-related service operations with resilience support.
/// </summary>
public interface IHolidayService
{
    /// <summary>
    /// Retrieves Norwegian holidays for a given year using a supervised actor with retry.
    /// </summary>
    /// <param name="year">The year to retrieve holidays for.</param>
    /// <returns>A task containing the holiday data as a string.</returns>
    Task<string> GetNorwegianHolidaysAsync(int year);

    /// <summary>
    /// Retrieves Danish holidays for a given year using a supervised actor with retry.
    /// </summary>
    /// <param name="year">The year to retrieve holidays for.</param>
    /// <returns>A task containing the holiday data as a string.</returns>
    Task<string> GetDanishHolidaysAsync(int year);

    /// <summary>
    /// Retrieves holidays for multiple years in parallel using fan-out to worker actors with supervision.
    /// </summary>
    /// <param name="years">The years to retrieve holidays for.</param>
    /// <param name="country">The country code.</param>
    /// <returns>A task containing a dictionary mapping year to holiday data.</returns>
    Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country);
}
