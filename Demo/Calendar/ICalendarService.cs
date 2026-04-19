namespace Demo.Calendar;

internal interface ICalendarService
{
    Task<string> GetDanishHolidaysAsync();

    Task<string> GetNorwegianHolidaysAsync();
}

