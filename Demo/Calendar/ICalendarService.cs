namespace Demo.Calendar;

/// <summary>
/// Demonstrates every Oasis.Resilience pattern against the live Calendara API
/// (http://localhost:8080 · countries: DK, NO, SE · years: 1976–2066).
/// </summary>
internal interface ICalendarService
{
    /// <summary>
    /// Fetches holidays for a single country/year.
    /// Decorated with <c>[Retry]</c> — retries up to 3 times with exponential backoff.
    /// </summary>
    Task<string> GetHolidaysAsync(string country, int year);

    /// <summary>
    /// Fetches holidays guarded by a circuit breaker.
    /// Opens after 3 exhausted retry sequences; probes again after 5 seconds (Half-Open).
    /// Decorated with <c>[CircuitBreaker] + [Retry]</c>.
    /// </summary>
    Task<string> GetHolidaysWithBreakerAsync(string country, int year);

    /// <summary>
    /// Fetches holidays inside a supervised Akka actor.
    /// The supervisor rebuilds the actor with exponential backoff on unexpected crashes.
    /// Decorated with <c>[Retry] + [Supervision(RestartWithBackoff)]</c>.
    /// </summary>
    Task<string> GetHolidaysWithSupervisionAsync(string country, int year);

    /// <summary>
    /// Fetches holidays for multiple years in parallel.
    /// The proxy splits the <paramref name="years"/> array and dispatches one worker actor
    /// per year (bounded by <c>maxWorkers</c>), then merges the partial results.
    /// Decorated with <c>[FanOut(maxWorkers: 5)]</c>.
    /// </summary>
    Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country);
}

