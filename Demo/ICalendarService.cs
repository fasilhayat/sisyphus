namespace Demo;

internal interface ICalendarService
{
    Task<string> GetDanishHolidaysAsync();

    Task<string> GetNorwegianHolidaysAsync();
}

