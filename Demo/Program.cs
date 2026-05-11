using Demo.Bonds;
using Demo.Calendar;
using Demo.Holidays;
using Demo.Inventory;
using Demo.MockServer;
using Demo.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;

// ── Mock server ───────────────────────────────────────────────────────────────
using var mock = new MockServer();
mock.Start();

// ── DI setup ──────────────────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddResilience(options =>
{
    options.LogLevel = LogLevel.Debug; // surfaces [Resilience] attempt/retry/circuit logs
    options.MaxDelayMs = 5_000;
    options.JitterFactor = 0.0;        // no jitter: predictable demo timing
    options.AskTimeout = TimeSpan.FromSeconds(30);
});
services.AddResilientService<ITiwazService, TiwazService>();
services.AddResilientService<ICalendarService, CalendarService>();
services.AddResilientService<IInventoryService, InventoryService>();
services.AddResilientService<IHolidayService, HolidayService>();
services.AddResilientService<IDataPipelineService, DataPipelineService>();

using var provider = services.BuildServiceProvider();

var bonds     = provider.GetRequiredService<ITiwazService>();
var calendar  = provider.GetRequiredService<ICalendarService>();
var inventory = provider.GetRequiredService<IInventoryService>();
var holidays  = provider.GetRequiredService<IHolidayService>();
var pipeline  = provider.GetRequiredService<IDataPipelineService>();

// ══════════════════════════════════════════════════════════════════════════════
Banner("OASIS RESILIENCE — LIVE DEMO");
Dim("  Mock backend running on http://localhost:5080");
Console.WriteLine();

// ══════════════════════════════════════════════════════════════════════════════
Chapter(1, "RETRY", "[Retry(maxAttempts: 3, initialDelay: 500)]");
// ══════════════════════════════════════════════════════════════════════════════

Info("Scenario : Bonds service is FLAKY — fails 2 times then recovers.");
Info("Delays   : 500ms → 1 000ms (exponential backoff, no jitter).");
Info("Expected : 2 failures with retries, success on the 3rd attempt.");
Console.WriteLine();

mock.SetMode("bonds", MockMode.Flaky, flakeAfter: 2);
MockLine("bonds → FLAKY (fails 2x then OK)");
Console.WriteLine();

try
{
    var result = await bonds.GetBondsAsync();
    Ok($"GetBondsAsync succeeded: {Clip(result, 80)}");
}
catch (Exception ex)
{
    Err($"GetBondsAsync failed after all retries: {ex.Message}");
}

await PauseAsync();

// ══════════════════════════════════════════════════════════════════════════════
Chapter(2, "PARALLEL RETRY", "Task.WhenAll — independent retry chains per call");
// ══════════════════════════════════════════════════════════════════════════════

Info("Scenario : DK and NO calendar calls run concurrently via Task.WhenAll.");
Info("DK       : [Retry(maxAttempts: 2)] — FLAKY (1 failure before OK).");
Info("NO       : [Retry(maxAttempts: 3)] — FLAKY (2 failures before OK).");
Info("Expected : Both complete independently. NO retries more but never blocks DK.");
Console.WriteLine();

mock.SetMode("calendar.dk", MockMode.Flaky, flakeAfter: 1);
mock.SetMode("calendar.no", MockMode.Flaky, flakeAfter: 2);
MockLine("calendar.dk → FLAKY (1 failure)  |  calendar.no → FLAKY (2 failures)");
Console.WriteLine();

var dkTask = calendar.GetDanishHolidaysAsync();
var noTask = calendar.GetNorwegianHolidaysAsync();

try { await Task.WhenAll(dkTask, noTask); }
catch { /* inspect each task individually below */ }

PrintResult("DK", dkTask);
PrintResult("NO", noTask);

await PauseAsync();

// ══════════════════════════════════════════════════════════════════════════════
Chapter(3, "CIRCUIT BREAKER + RETRY",
    "[CircuitBreaker(failureThreshold: 3, resetTimeout: 5 000ms)] + [Retry(maxAttempts: 2)]");
// ══════════════════════════════════════════════════════════════════════════════

Info("Scenario : Inventory service is DOWN. After 3 exhausted retry sequences the");
Info("           circuit OPENS and rejects all further calls immediately (fast-fail).");
Info("           After 5s it goes HALF-OPEN; one successful test call closes it.");
Console.WriteLine();
Info("Phase 1  : 3 calls exhaust retries  →  circuit opens  (Closed → Open)");
Info("Phase 2  : Circuit is open          →  fast-fail, no HTTP request made");
Info("Phase 3  : 5.5s wait               →  circuit → Half-Open → test OK → Closed");
Console.WriteLine();

mock.SetMode("inventory", MockMode.Fail);
MockLine("inventory → FAIL (service is down)");
Console.WriteLine();

Info("── Phase 1: exhaust 3 call sequences to open the circuit ──────────────────");
for (int i = 1; i <= 3; i++)
{
    Info($"Call {i}/3 (each call retries 2× before the breaker counts it as a failure)...");
    try { await inventory.GetInventoryAsync(); }
    catch (CircuitBreakerActor.CircuitBreakerOpenException cbEx)
    {
        Ok($"  Circuit already open: {cbEx.Message}");
    }
    catch (Exception ex)
    {
        Warn($"  Failed (expected): {ex.GetType().Name}");
    }
}

Console.WriteLine();
Info("── Phase 2: circuit is OPEN — next call must fast-fail ────────────────────");

try { await inventory.GetInventoryAsync(); }
catch (CircuitBreakerActor.CircuitBreakerOpenException cbEx)
{
    Ok($"  Fast-fail confirmed — no HTTP request was made. {cbEx.Message}");
}
catch (Exception ex)
{
    Err($"  Unexpected: {ex.GetType().Name}: {ex.Message}");
}

Console.WriteLine();
Info("── Phase 3: waiting for reset timeout (circuit → Half-Open) ───────────────");

for (int s = 5; s >= 1; s--)
{
    Console.Write($"\r  {s}s remaining... ");
    await Task.Delay(1_000);
}
Console.WriteLine("\r  Timeout elapsed.    ");
Console.WriteLine();

mock.SetMode("inventory", MockMode.Ok);
MockLine("inventory → OK (service recovered)");
Console.WriteLine();

Info("Sending test call through HALF-OPEN circuit...");
try
{
    var result = await inventory.GetInventoryAsync();
    Ok($"Test call succeeded — circuit is now CLOSED: {Clip(result, 60)}");
}
catch (Exception ex)
{
    Err($"Test call failed — circuit re-opened: {ex.Message}");
}

await PauseAsync();

// ══════════════════════════════════════════════════════════════════════════════
Chapter(4, "SUPERVISION + RETRY",
    "[Retry(maxAttempts: 3)] + [Supervision(RestartWithBackoff, backoffMinMs: 500)]");
// ══════════════════════════════════════════════════════════════════════════════

Info("Scenario : Data pipeline fails on first 2 calls. Each retry attempt runs inside");
Info("           a supervised Akka actor with RestartWithBackoff strategy.");
Info("           The supervisor rebuilds the worker on unexpected crashes.");
Info("Expected : Attempt 1 → fail  |  retry 500ms  |  Attempt 2 → fail");
Info("           retry 1 000ms     |  Attempt 3 → success");
Console.WriteLine();

mock.SetMode("pipeline", MockMode.Flaky, flakeAfter: 2);
MockLine("pipeline → FLAKY (fails 2x then OK)");
Console.WriteLine();

try
{
    var result = await pipeline.ProcessAsync();
    Ok($"Pipeline succeeded: {Clip(result, 80)}");
}
catch (Exception ex)
{
    Err($"Pipeline failed after all supervised retries: {ex.Message}");
}

await PauseAsync();

// ══════════════════════════════════════════════════════════════════════════════
Chapter(5, "FAN-OUT", "[FanOut(maxWorkers: 4)]");
// ══════════════════════════════════════════════════════════════════════════════

Info("Scenario : Fetch holidays for 4 years. The proxy intercepts the call, splits");
Info("           the int[] parameter, and dispatches one parallel worker per year");
Info("           (bounded by maxWorkers: 4). Partial dictionaries are auto-merged.");
Info("Expected : 4 concurrent HTTP calls → 4 partial results → 1 merged Dictionary.");
Console.WriteLine();

mock.SetMode("holidays", MockMode.Ok);
MockLine("holidays → OK");
Console.WriteLine();

var years = new[] { 2022, 2023, 2024, 2025 };
Info($"Calling GetHolidaysForYearsAsync([{string.Join(", ", years)}], \"norway\")...");
Console.WriteLine();

try
{
    var result = await holidays.GetHolidaysForYearsAsync(years, "norway");
    Ok($"Fan-out merged {result.Count} results:");
    foreach (var (year, data) in result.OrderBy(k => k.Key))
        Console.WriteLine($"    {year} → {Clip(data, 70)}");
}
catch (Exception ex)
{
    Err($"Fan-out failed: {ex.Message}");
}

Console.WriteLine();
Banner("DEMO COMPLETE");
Console.WriteLine("Press ENTER to exit...");
Console.ReadLine();

// ════════════════════════════════════════════════════════════════════════════
// Helpers
// ════════════════════════════════════════════════════════════════════════════

static void Banner(string text)
{
    var bar = new string('═', Math.Max(60, text.Length + 4));
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
    Console.WriteLine($"┌─ CHAPTER {n}: {title}");
    Console.WriteLine($"│  {subtitle}");
    Console.WriteLine("└" + new string('─', 75));
    Console.ResetColor();
    Console.WriteLine();
}

static void Info(string text)
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"  {text}");
    Console.ResetColor();
}

static void MockLine(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine($"  [MOCK] {text}");
    Console.ResetColor();
}

static void Ok(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✅ {text}");
    Console.ResetColor();
}

static void Warn(string text)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  ⚠  {text}");
    Console.ResetColor();
}

static void Err(string text)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ✗  {text}");
    Console.ResetColor();
}

static void Dim(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(text);
    Console.ResetColor();
}

static void PrintResult(string name, Task<string> task)
{
    if (task.IsCompletedSuccessfully)
        Ok($"{name}: {Clip(task.Result, 70)}");
    else if (task.IsFaulted)
        Err($"{name} failed: {task.Exception?.GetBaseException().Message}");
    else
        Warn($"{name} was cancelled");
}

static string Clip(string s, int max) => s.Length <= max ? s : s[..max] + "…";

static async Task PauseAsync()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("  Press ENTER to continue to the next chapter...");
    Console.ResetColor();
    Console.ReadLine();
}

