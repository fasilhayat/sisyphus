namespace ResilienceWithAop;

using Akka.Actor;

public class RetryActor : ReceiveActor
{
    public class Execute
    {
        public Func<Task<string>> Operation { get; set; } = null!;
        public int MaxAttempts { get; set; }
        public TimeSpan InitialDelay { get; set; }
    }

    public RetryActor()
    {
        ReceiveAsync<Execute>(async msg =>
        {
            int attempts = 0;
            Exception? lastException = null;
            while (attempts < msg.MaxAttempts)
            {
                try
                {
                    var result = await msg.Operation();
                    Sender.Tell(result);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    Console.WriteLine($"Attempt {attempts} failed: {ex.Message}");
                    if (attempts < msg.MaxAttempts)
                    {
                        var delay = msg.InitialDelay.TotalMilliseconds * Math.Pow(2, attempts - 1);
                        Console.WriteLine($"Retrying in {delay / 1000} seconds...");
                        await Task.Delay((int)delay);
                    }
                }
            }
            Sender.Tell(new Status.Failure(lastException));
        });
    }
}