namespace Oasis.Resilience.Actors;

using Akka.Actor;

/// <summary>
/// An Akka.NET actor that executes operations with configurable retry logic and exponential backoff for resilience.
/// </summary>
/// <remarks>Handles execution requests by retrying failed operations up to a specified number of attempts,
/// applying an exponential delay between retries. Reports success or failure to the sender.</remarks>
public sealed class ResilienceActor : ReceiveActor
{
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
    public ResilienceActor()
    {
        ReceiveAsync<Execute>(ExecuteWithRetry);
    }

    /// <summary>
    /// Executes the specified operation with retry logic, sending the result or failure to the sender.
    /// </summary>
    /// <param name="msg">The execution parameters, including the operation to perform, maximum attempts, and initial delay.</param>
    /// <returns>A task that represents the asynchronous execution with retry logic.</returns>
    private async Task ExecuteWithRetry(Execute msg)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= msg.MaxAttempts; attempt++)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Resilience] Attempt {attempt} executing...");
                Console.ResetColor();

                var result = await msg.Operation();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Resilience] Success on attempt {attempt}");
                Console.ResetColor();

                Sender.Tell(result);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Resilience] Attempt {attempt} failed: {ex.Message}");
                Console.ResetColor();

                if (attempt == msg.MaxAttempts)
                    break;

                var delay = TimeSpan.FromMilliseconds(msg.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[Resilience] Retrying in {delay.TotalSeconds}s...");
                Console.ResetColor();

                await Task.Delay(delay);
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[Resilience] Max retry attempts reached. Failing.");
        Console.ResetColor();

        Sender.Tell(new Status.Failure(lastException!));
    }
}