namespace Demo.Pipeline;

using Oasis.Resilience.Attributes;

/// <summary>
/// A pipeline service that demonstrates <see cref="SupervisionAttribute"/> combined with
/// <see cref="RetryAttribute"/>. Each retry attempt is dispatched through a supervised Akka.NET
/// actor with <see cref="SupervisionStrategy.RestartWithBackoff"/>.
/// </summary>
public class DataPipelineService : IDataPipelineService
{
    private static readonly HttpClient Client = new(new HttpClientHandler { UseProxy = false })
    {
        BaseAddress = new Uri("http://localhost:5080")
    };

    /// <summary>
    /// Executes the pipeline. Retried up to 3 times with 500ms initial backoff.
    /// Each attempt runs inside a supervised actor; the supervisor restarts the worker
    /// with exponential backoff if the actor itself crashes.
    /// </summary>
    [Retry(maxAttempts: 3, initialDelay: 500)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff, maxRetries: 4, backoffMinMs: 500, backoffMaxMs: 3000, randomFactor: 0.0)]
    public async Task<string> ProcessAsync()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [Pipeline] Actor executing operation...");
        Console.ResetColor();

        var response = await Client.GetAsync("/pipeline");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
