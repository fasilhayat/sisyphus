namespace Oasis.Resilience;

using Akka.Actor;

/// <summary>
/// An Akka.NET actor that executes asynchronous operations with retry logic and exponential backoff.
/// </summary>
/// <remarks>The ResilienceActor receives Execute messages containing an operation to perform, the maximum number
/// of retry attempts, and the initial delay between retries. If the operation fails, the actor retries it up to the
/// specified maximum attempts, increasing the delay between each attempt exponentially. The result of the operation is
/// sent back to the sender upon success; if all attempts fail, a Status.Failure message containing the last exception
/// is returned. This actor is useful for adding resilience to operations that may intermittently fail, such as network
/// or I/O calls.</remarks>
public class ResilienceActor : ReceiveActor
{
    /// <summary>
    /// Represents the configuration for executing an asynchronous operation with retry logic.
    /// </summary>
    /// <remarks>This class encapsulates the parameters required to perform an operation that may be retried
    /// multiple times with a configurable delay between attempts. It is typically used to specify the operation to
    /// execute, the maximum number of retry attempts, and the initial delay before retrying after a failure.</remarks>
    public class Execute
    {
        /// <summary>
        /// Gets or sets the asynchronous operation to execute, represented as a function that returns a task producing
        /// a string result.
        /// </summary>
        /// <remarks>The assigned function should perform the desired operation asynchronously and return
        /// a string upon completion. The returned task should not be null. Exceptions thrown by the operation will
        /// propagate to the caller when the task is awaited.</remarks>
        public Func<Task<string>> Operation { get; set; } = null!;

        /// <summary>
        /// Gets or sets the maximum number of attempts allowed for an operation.
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        /// Gets or sets the initial delay before the first operation is attempted.
        /// </summary>
        public TimeSpan InitialDelay { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the ResilienceActor class, configuring it to handle Execute messages with retry
    /// logic.
    /// </summary>
    /// <remarks>The actor processes Execute messages by attempting the specified operation up to the maximum
    /// number of attempts, applying exponential backoff between retries. If all attempts fail, the actor replies with a
    /// Status.Failure containing the last encountered exception. This constructor sets up the message handling behavior
    /// for the actor; it does not perform any operation until a message is received.</remarks>
    public ResilienceActor()
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

