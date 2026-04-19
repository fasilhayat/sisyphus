namespace Oasis.Resilience.Actors;

using Akka.Actor;

/// <summary>
/// An Akka.NET actor that executes operations with configurable retry logic and exponential backoff for resilience.
/// </summary>
/// <remarks>
/// Handles execution requests by retrying failed operations up to a specified number of attempts,
/// applying an exponential delay between retries. Reports success or failure to the sender.
/// </remarks>
public sealed class ResilienceActor : ReceiveActor
{
    /// <summary>
    /// Provides resilience configuration settings used by the actor, including whether
    /// verbose retry diagnostics should be written during execution.
    /// </summary>
    private readonly ResilienceOptions _options;

    /// <summary>
    /// Represents an executable operation with retry logic and configurable delay between attempts.
    /// </summary>
    /// <param name="Operation">A delegate representing the asynchronous operation to execute.</param>
    /// <param name="MaxAttempts">The maximum number of retry attempts.</param>
    /// <param name="InitialDelay">The initial delay between retry attempts.</param>
    public sealed record Execute(Func<Task<object>> Operation, int MaxAttempts, TimeSpan InitialDelay);

    /// <summary>
    /// Initializes a new instance of the ResilienceActor class and sets up message handling with retry logic.
    /// </summary>
    /// <param name="options">The resilience configuration options.</param>
    public ResilienceActor(ResilienceOptions options)
    {
        _options = options;
        ReceiveAsync<Execute>(ExecuteWithRetry);
    }

    /// <summary>
    /// Executes the specified operation with retry logic, sending the result or failure to the sender.
    /// </summary>
    /// <param name="msg">The execution parameters, including the operation to perform, maximum attempts, and initial delay.</param>
    /// <returns>
    /// A task that represents the asynchronous execution with retry logic.
    /// </returns>
    private async Task ExecuteWithRetry(Execute msg)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= msg.MaxAttempts; attempt++)
        {
            try
            {
                Log($"Attempt {attempt} executing...");
                var result = await msg.Operation();
                Log($"Success on attempt {attempt}");

                Sender.Tell(result);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log($"Attempt {attempt} failed: {ex.Message}");

                if (attempt == msg.MaxAttempts)
                    break;

                var delay = TimeSpan.FromMilliseconds(msg.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

                Log($"Retrying in {delay.TotalSeconds}s...");
                await Task.Delay(delay);
            }
        }

        Log("Max retry attempts reached. Failing.");
        Sender.Tell(new Status.Failure(lastException!));
    }

    /// <summary>
    /// Logs a message to the console when verbose logging is enabled.
    /// </summary>
    /// <param name="message">The message to write to the console.</param>
    private void Log(string message)
    {
        if (!_options.VerboseLogging) 
            return;
        Console.WriteLine($"[Resilience] {message}");
    }
}