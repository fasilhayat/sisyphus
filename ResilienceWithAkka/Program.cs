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

/// <summary>
/// Message to initiate a bonds fetch operation.
/// </summary>
public record FetchBonds;

/// <summary>
/// Message to schedule a retry attempt.
/// </summary>
/// <param name="Attempt">The current attempt number.</param>
internal record Retry(int Attempt);


// ===== Actor =====

/// <summary>
/// An Akka.NET actor that fetches bonds data with retry logic and exponential backoff.
/// </summary>
public sealed class TiwazClientActor : ReceiveActor, IWithTimers
{
    /// <summary>
    /// HTTP client used for making requests to the Tiwaz service.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Maximum number of retry attempts before giving up.
    /// </summary>
    private readonly int _maxAttempts;

    /// <summary>
    /// Base delay duration for the first retry, used for exponential backoff calculations.
    /// </summary>
    private readonly TimeSpan _initialDelay;

    /// <summary>
    /// Gets or sets the timer scheduler for scheduling delayed retry messages.
    /// </summary>
    public ITimerScheduler? Timers { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TiwazClientActor"/> class.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of retry attempts.</param>
    /// <param name="initialDelay">The base delay for the first retry.</param>
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

    /// <summary>
    /// Executes the bonds fetch operation with retry logic and exponential backoff on failure.
    /// </summary>
    /// <param name="attempt">The current attempt number.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Called when the actor stops. Disposes the HTTP client to release resources.
    /// </summary>
    protected override void PostStop()
    {
        _httpClient.Dispose();
        base.PostStop();
    }
}
