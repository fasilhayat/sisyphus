using Akka.Actor;

// ===== Configuration =====
const int maxAttempts = 5;
var initialDelay = TimeSpan.FromSeconds(2);

// ===== Actor System =====
using var system = ActorSystem.Create("tiwaz-system");

var client = system.ActorOf(
    Props.Create(() => new TiwazClientActor(maxAttempts, initialDelay)),
    "tiwazClient"
);

client.Tell(new FetchBonds());

Console.WriteLine("Press ENTER to terminate...");
Console.ReadLine();

await system.Terminate();


// ===== Messages =====
public record FetchBonds;
internal record Retry(int Attempt);


// ===== Actor =====
public sealed class TiwazClientActor : ReceiveActor, IWithTimers
{
    private readonly HttpClient _httpClient;
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;

    public ITimerScheduler? Timers { get; set; } // Made nullable to satisfy CS8618

    public TiwazClientActor(int maxAttempts, TimeSpan initialDelay)
    {
        _maxAttempts = maxAttempts;
        _initialDelay = initialDelay;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://tiwaz.hayatnet.local")
        };

        ReceiveAsync<FetchBonds>(_ => ExecuteAsync(1));
        ReceiveAsync<Retry>(r => ExecuteAsync(r.Attempt));
    }

    private async Task ExecuteAsync(int attempt)
    {
        Console.WriteLine($"Attempt {attempt}...");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/v1/bonds");
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("X-API-KEY", "Skyw@lker!");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"HTTP {response.StatusCode}");

            var body = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Success:");
            Console.WriteLine(body);

            Context.Stop(Self); // finished successfully
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failure on attempt {attempt}: {ex.Message}");

            if (attempt >= _maxAttempts)
            {
                Console.WriteLine("Circuit opened. Max retry attempts reached.");
                Context.Stop(Self);
                return;
            }

            var delay = TimeSpan.FromMilliseconds(
                _initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)
            );

            Console.WriteLine($"Retrying in {delay.TotalSeconds} seconds...");

            // Use Akka.Timers instead of Scheduler to avoid AK1004
            Timers.StartSingleTimer("retry", new Retry(attempt + 1), delay);
        }
    }

    protected override void PostStop()
    {
        _httpClient.Dispose();
        base.PostStop();
    }
}
