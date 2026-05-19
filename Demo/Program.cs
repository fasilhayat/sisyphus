using Demo.Calendar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
// using Prometheus;

// // -- Metrics server -----------------------------------------------------------
// var metricServer = new MetricServer(port: 9464);
// metricServer.Start();

// -- DI setup -----------------------------------------------------------------
var services = new ServiceCollection();
services.AddResilience(options =>
{
    options.LogLevel = LogLevel.Debug;
    options.MaxDelayMs = 5_000;
    options.JitterFactor = 0.0;
    options.AskTimeout = TimeSpan.FromSeconds(60);
});
services.AddResilientService<ICalendarService, CalendarService>();

using var provider = services.BuildServiceProvider();
var calendar = provider.GetRequiredService<ICalendarService>();

// =============================================================================
Banner("OASIS RESILIENCE — CALENDAR SERVICE DEMO");
Dim("  Calendar API : http://localhost:8080  (X-API-KEY authenticated)");
Dim("  Countries    : DK  NO  SE            (years 1976-2066)");
Console.WriteLine();

// =============================================================================
Chapter(1, "RETRY",
    "[Retry(maxAttempts: 3, initialDelay: 500ms · doubles each attempt)]");
// =============================================================================
Info("Scenario : Each round fires up to 3 attempts with 500ms / 1 000ms backoff.");
Info("           When all retries are exhausted you are prompted to start the service.");
Info("Expected : Failed attempts visible in console → start service → round succeeds.");
Console.WriteLine();

Hint("Stop the Calendar service now  (docker compose stop <service>)");
Step("Press ENTER when the service is stopped...");

int retryRound = 1;
string? retryResult = null;
while (retryResult is null)
{
    Info($"Round {retryRound} — GetHolidaysAsync(DK, 2024):");
    Console.WriteLine();
    try
    {
        retryResult = await calendar.GetHolidaysAsync("DK", 2024);
        Console.WriteLine();
        Ok($"Success on round {retryRound}: {Summarize(retryResult)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Err($"Round {retryRound} — all retries exhausted: {ex.GetType().Name}");
        Console.WriteLine();
        Hint("Start the Calendar service now  (docker compose start <service>)");
        Step("Press ENTER when the service is up...");
        retryRound++;
    }
}

await PauseAsync();

// =============================================================================
Chapter(2, "CIRCUIT BREAKER",
    "[CircuitBreaker(failureThreshold: 3, resetTimeout: 5 000ms)] + [Retry(maxAttempts: 2)]");
// =============================================================================
Info("Phase 1  : 3 call sequences exhaust retries  →  circuit opens  (Closed → Open)");
Info("Phase 2  : Circuit OPEN                      →  fast-fail, no HTTP call made");
Info("Phase 3  : Start service + wait 5 s          →  Half-Open → probe OK → Closed");
Console.WriteLine();

Hint("Stop the Calendar service now  (docker compose stop <service>)");
Step("Press ENTER when the service is stopped...");

Console.WriteLine();
Info("── Phase 1: exhaust 3 call sequences to open the circuit ──────────────────");
Hint("Service should be DOWN — watch retries fail and the circuit trip");
Console.WriteLine();

for (int i = 1; i <= 3; i++)
{
    Info($"Call {i}/3  (retries 2x before the breaker counts this as a failure):");
    Console.WriteLine();
    try { await calendar.GetHolidaysWithBreakerAsync("DK", 2023); }
    catch (CircuitBreakerActor.CircuitBreakerOpenException cbEx)
    {
        Ok($"  Circuit already open — {cbEx.Message}");
    }
    catch (Exception ex)
    {
        Warn($"  Failed as expected: {ex.GetType().Name}");
    }
    Console.WriteLine();
}

Info("── Phase 2: circuit is OPEN — confirming fast-fail ────────────────────────");
Hint("No HTTP call should leave the process for this one");
Console.WriteLine();

try { await calendar.GetHolidaysWithBreakerAsync("DK", 2023); }
catch (CircuitBreakerActor.CircuitBreakerOpenException cbEx)
{
    Ok($"Fast-fail confirmed — no HTTP call was made.  {cbEx.Message}");
}
catch (Exception ex)
{
    Err($"Unexpected: {ex.GetType().Name}: {ex.Message}");
}

Console.WriteLine();
Info("── Phase 3: recover ────────────────────────────────────────────────────────");
Console.WriteLine();

var circuitOpenedAt = DateTime.UtcNow;

Hint("Start the Calendar service now  (docker compose start <service>)");
Step("Press ENTER when the service is up...");

var elapsed = DateTime.UtcNow - circuitOpenedAt;
var remaining = TimeSpan.FromMilliseconds(5500) - elapsed;
if (remaining > TimeSpan.Zero)
{
    Hint($"Waiting {remaining.TotalSeconds:F0}s for the 5s circuit reset timeout to elapse...");
    for (int s = (int)Math.Ceiling(remaining.TotalSeconds); s >= 1; s--)
    {
        Console.Write($"\r  {s}s remaining... ");
        await Task.Delay(1_000);
    }
    Console.WriteLine("\r  Reset timeout elapsed.      ");
}

Console.WriteLine();
Info("Sending probe call through HALF-OPEN circuit...");
Console.WriteLine();
try
{
    var result = await calendar.GetHolidaysWithBreakerAsync("DK", 2023);
    Ok($"Probe succeeded — circuit CLOSED: {Summarize(result)}");
}
catch (Exception ex)
{
    Err($"Probe failed — circuit re-opened: {ex.Message}");
}

await PauseAsync();

// =============================================================================
Chapter(3, "SUPERVISION",
    "[Retry(maxAttempts: 2, initialDelay: 500ms)] + [Supervision(RestartWithBackoff · maxRetries: 10)]");
// =============================================================================
Info("Scenario : Each retry attempt runs inside a supervised Akka actor.");
Info("           When retries are exhausted the supervisor restarts the actor");
Info("           with exponential backoff (500ms..2 000ms) before the next round.");
Info("Expected : Retry attempts visible → actor restarts → start service → succeeds.");
Console.WriteLine();

Hint("Stop the Calendar service now  (docker compose stop <service>)");
Step("Press ENTER when the service is stopped...");

int superRound = 1;
string? superResult = null;
while (superResult is null)
{
    Info($"Round {superRound} — GetHolidaysWithSupervisionAsync(SE, 2022):");
    Console.WriteLine();
    try
    {
        superResult = await calendar.GetHolidaysWithSupervisionAsync("SE", 2022);
        Console.WriteLine();
        Ok($"Supervised actor succeeded on round {superRound}: {Summarize(superResult)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Err($"Round {superRound} — supervision exhausted: {ex.GetType().Name}");
        Console.WriteLine();
        Hint("Start the Calendar service now  (docker compose start <service>)");
        Step("Press ENTER when the service is up...");
        superRound++;
    }
}

await PauseAsync();

// =============================================================================
Chapter(4, "FAN-OUT",
    "[FanOut(maxWorkers: 5)]");
// =============================================================================
Info("Scenario : Fetch DK holidays for 6 years simultaneously. The proxy splits");
Info("           the int[] parameter and dispatches one worker actor per year");
Info("           (bounded by maxWorkers: 5). Partial results are auto-merged.");
Info("Expected : 6 concurrent HTTP calls → 1 merged Dictionary<int, string>.");
Console.WriteLine();

Hint("Service should be RUNNING — press ENTER to fire the fan-out");
Step("Press ENTER to start...");
Console.WriteLine();

var years = new[] { 2019, 2020, 2021, 2022, 2023, 2024 };
Info($"GetHolidaysForYearsAsync([{string.Join(", ", years)}], \"DK\")");
Console.WriteLine();

try
{
    var result = await calendar.GetHolidaysForYearsAsync(years, "DK");
    Console.WriteLine();
    Ok($"Fan-out merged {result.Count} year(s):");
    foreach (var kvp in result.OrderBy(k => k.Key))
        Console.WriteLine($"    {kvp.Key}  →  {Summarize(kvp.Value)}");
}
catch (Exception ex)
{
    Err($"Fan-out failed: {ex.Message}");
}

Console.WriteLine();
Banner("DEMO COMPLETE");
Console.WriteLine("Press ENTER to exit...");
Console.ReadLine();

// metricServer.Stop();

// =============================================================================
// Helpers
// =============================================================================

static void Banner(string text)
{
    var bar = new string('=', Math.Max(62, text.Length + 4));
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine(bar);
    Console.WriteLine($"  {text}");
    Console.WriteLine(bar);
    Console.ResetColor();
    Console.WriteLine();
}

static void Chapter(int n, string title, string subtitle)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"+-- CHAPTER {n}: {title}");
    Console.WriteLine($"|   {subtitle}");
    Console.WriteLine("+" + new string('-', 75));
    Console.ResetColor();
    Console.WriteLine();
}

static void Info(string text)
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"  {text}");
    Console.ResetColor();
}

/// <summary>Dimmed instruction telling the user what to do with Docker right now.</summary>
static void Hint(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  --  {text}");
    Console.ResetColor();
}

static void Ok(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  OK  {text}");
    Console.ResetColor();
}

static void Warn(string text)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  !!  {text}");
    Console.ResetColor();
}

static void Err(string text)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  XX  {text}");
    Console.ResetColor();
}

static void Dim(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(text);
    Console.ResetColor();
}


/// <summary>Blocking pause — waits for the user to press ENTER before continuing.</summary>
static void Step(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.Write($"  >>  {text}");
    Console.ResetColor();
    Console.ReadLine();
}

static string Summarize(string json)
{
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var items = root.EnumerateArray().ToList();
            var names = items.Take(3).Select(h =>
                h.TryGetProperty("localName", out var p) ? p.GetString() :
                h.TryGetProperty("name", out var n) ? n.GetString() : "?");
            var suffix = items.Count > 3 ? $", ... (+{items.Count - 3} more)" : "";
            return $"{items.Count} holidays: {string.Join(", ", names)}{suffix}";
        }
        return Clip(json, 80);
    }
    catch { return Clip(json, 80); }
}

static string Clip(string s, int max) => s.Length <= max ? s : s[..max] + "...";

static async Task PauseAsync()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("  Press ENTER to continue to the next chapter...");
    Console.ResetColor();
    Console.ReadLine();
}
