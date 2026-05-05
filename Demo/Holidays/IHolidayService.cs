namespace Demo.Holidays;

using Oasis.Resilience.Attributes;

public interface IHolidayService
{
    // Simple: Single supervised actor with retry
    [Retry(maxAttempts: 3, initialDelay: 2000)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    Task<string> GetNorwegianHolidaysAsync(int year);

    // Simple: Single supervised actor with retry
    [Retry(maxAttempts: 3, initialDelay: 2000)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    Task<string> GetDanishHolidaysAsync(int year);

    // Advanced: Fan-out to multiple actors with supervision
    [FanOut(workerActorType: typeof(Actors.HolidayWorkerActor), splitParameterName: "years", maxWorkers: 5)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country);
}
