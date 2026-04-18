namespace Oasis.Resilience.Actors;

using Akka.Actor;

public sealed class ResilienceActor : ReceiveActor
{
    public sealed record Execute(Func<Task<object>> Operation, int MaxAttempts, TimeSpan InitialDelay);

    public ResilienceActor()
    {
        ReceiveAsync<Execute>(ExecuteWithRetry);
    }

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