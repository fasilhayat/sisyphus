namespace Demo.Calendar;

/// <summary>
/// Defines the contract for calendar-related service operations.
/// </summary>
internal interface ICalendarService
{
    /// <summary>
    /// Retrieves Danish public holidays asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation containing the holiday data as a string.</returns>
    Task<string> GetDanishHolidaysAsync();

    /// <summary>
    /// Retrieves Norwegian public holidays asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation containing the holiday data as a string.</returns>
    Task<string> GetNorwegianHolidaysAsync();
}

