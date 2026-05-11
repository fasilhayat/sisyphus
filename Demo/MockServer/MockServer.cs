using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Demo.MockServer;

/// <summary>Controls how a mocked endpoint responds to requests.</summary>
public enum MockMode { Ok, Fail, Flaky }

/// <summary>Per-endpoint state: mode, flake threshold, and call counter.</summary>
public sealed class EndpointBehavior
{
    private int _callCount;

    public MockMode Mode { get; set; } = MockMode.Ok;
    public int FlakeAfter { get; set; } = 2;
    public string Payload { get; set; } = "{}";

    /// <summary>Returns true when this call should produce a 500 response.</summary>
    public bool ShouldFail()
    {
        if (Mode == MockMode.Ok) return false;
        if (Mode == MockMode.Fail) return true;
        // Flaky: fail for the first FlakeAfter calls, then return OK.
        return Interlocked.Increment(ref _callCount) <= FlakeAfter;
    }

    public void ResetCallCount() => Interlocked.Exchange(ref _callCount, 0);
}

/// <summary>
/// A self-contained HTTP mock server backed by <see cref="HttpListener"/> that runs on a background
/// thread at <c>http://localhost:5080/</c>. Each demo service endpoint has its own
/// <see cref="EndpointBehavior"/> that can be switched between Ok, Fail, and Flaky modes at runtime
/// via <see cref="SetMode"/>, enabling scripted failure scenarios without any external dependency.
/// </summary>
public sealed class MockServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, EndpointBehavior> _behaviors = new();
    private volatile bool _running;

    public const string BaseUrl = "http://localhost:5080/";

    public MockServer()
    {
        _listener.Prefixes.Add(BaseUrl);
        RegisterDefaults();
    }

    private void RegisterDefaults()
    {
        Register("bonds",
            """{"bonds":[{"isin":"DK0002030337","name":"5Y Gov Bond","yield":2.41},{"isin":"DK0002034941","name":"10Y Gov Bond","yield":2.89}]}""");
        Register("calendar.dk",
            """{"country":"DK","year":2024,"holidays":["Nytårsdag","Påskedag","Grundlovsdag","Juledag"]}""");
        Register("calendar.no",
            """{"country":"NO","year":2024,"holidays":["Nyttårsdag","Påskedag","Grunnlovsdag","Juledag"]}""");
        Register("inventory",
            """{"items":[{"id":"ITEM-001","name":"Widget A","qty":150},{"id":"ITEM-002","name":"Widget B","qty":42}]}""");
        Register("inventory.alerts",
            """{"alerts":[{"id":"ITEM-002","name":"Widget B","qty":42,"threshold":50,"level":"LOW"}]}""");
        Register("inventory.update",
            """{"success":true,"message":"Inventory updated successfully"}""");
        Register("pipeline",
            """{"processed":1000,"status":"completed","duration_ms":182}""");
        Register("holidays", "{}"); // payload is built dynamically per year/country
    }

    private void Register(string key, string payload)
        => _behaviors[key] = new EndpointBehavior { Payload = payload };

    /// <summary>Switches an endpoint to the given mode, resetting its call counter.</summary>
    public void SetMode(string endpoint, MockMode mode, int flakeAfter = 2)
    {
        if (_behaviors.TryGetValue(endpoint, out var b))
        {
            b.Mode = mode;
            b.FlakeAfter = flakeAfter;
            b.ResetCallCount();
        }
    }

    public void Start()
    {
        try
        {
            _running = true;
            _listener.Start();
        }
        catch (HttpListenerException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Failed to start mock server: {ex.Message}");
            Console.WriteLine($"  If the error is access denied, run once as administrator or:");
            Console.WriteLine($"    netsh http add urlacl url={BaseUrl} user=%USERNAME%");
            Console.ResetColor();
            throw;
        }

        var thread = new Thread(Listen) { IsBackground = true, Name = "MockServer" };
        thread.Start();
    }

    private void Listen()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try { ctx = _listener.GetContext(); }
            catch { break; }
            Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url!.AbsolutePath.TrimEnd('/');
            var method = ctx.Request.HttpMethod;

            if (method == "POST" && path == "/control")
            {
                await HandleControlAsync(ctx);
                return;
            }

            var key = Resolve(path, method);
            if (key is null) { await RespondAsync(ctx, 404, """{"error":"Not found"}"""); return; }

            if (!_behaviors.TryGetValue(key, out var behavior))
            { await RespondAsync(ctx, 404, """{"error":"Not found"}"""); return; }

            if (behavior.ShouldFail())
            { await RespondAsync(ctx, 500, """{"error":"Service temporarily unavailable"}"""); return; }

            var payload = key == "holidays" ? BuildHolidayPayload(path) : behavior.Payload;
            await RespondAsync(ctx, 200, payload);
        }
        catch
        {
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
        }
    }

    private static string? Resolve(string path, string method)
    {
        if (path == "/bonds") return "bonds";
        if (path == "/calendar/dk") return "calendar.dk";
        if (path == "/calendar/no") return "calendar.no";
        if (path == "/inventory/alerts") return "inventory.alerts";
        if (path == "/inventory") return "inventory";
        if (path.StartsWith("/inventory/") && method == "POST") return "inventory.update";
        if (path == "/pipeline") return "pipeline";
        if (path.StartsWith("/holidays/")) return "holidays";
        return null;
    }

    private static string BuildHolidayPayload(string path)
    {
        // path = /holidays/{country}/{year}
        var parts = path.Split('/');
        var country = parts.Length > 2 ? parts[2] : "??";
        var year = parts.Length > 3 ? parts[3] : "0";
        return $$"""{"country":"{{country}}","year":{{year}},"holidays":["New Year","Easter","Labour Day","Christmas"]}""";
    }

    private async Task HandleControlAsync(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        try
        {
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ep = root.GetProperty("endpoint").GetString()!;
            var modeStr = root.GetProperty("mode").GetString()!;
            var flakeAfter = root.TryGetProperty("flakeAfter", out var fa) ? fa.GetInt32() : 2;
            var mode = modeStr switch { "fail" => MockMode.Fail, "flaky" => MockMode.Flaky, _ => MockMode.Ok };
            SetMode(ep, mode, flakeAfter);
            await RespondAsync(ctx, 200, """{"ok":true}""");
        }
        catch
        {
            await RespondAsync(ctx, 400, """{"error":"Bad request"}""");
        }
    }

    private static async Task RespondAsync(HttpListenerContext ctx, int status, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
    }
}
