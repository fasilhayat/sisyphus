namespace Oasis.Resilience.Actors;

using Akka.Actor;
using Microsoft.Extensions.Logging;

/// <summary>
/// An Akka.NET actor that executes operations with retry logic and exponential backoff.
/// </summary>
public sealed class RetryActor : ReceiveActor, IWithTimers
{
    private readonly RetryOptions _options;
    private readonly ILogger<RetryActor>? _logger;

    /// <summary>
    /// Instructs the actor to execute an operation with retry capability.
    /// </summary>
    /// <param name="Operation">The operation to execute.</param>
    /// <param name="MaxAttempts">Maximum number of attempts before giving up.</param>
    /// <param name="InitialDelay">Initial delay before the first retry (doubles each attempt).</param>
    public sealed record Execute(Func<Task<object>> Operation, int MaxAttempts, TimeSpan InitialDelay);

    private sealed record ExecuteAttempt(
        Func<Task<object>> Operation, int MaxAttempts, TimeSpan InitialDelay, int Attempt, IActorRef OriginalSender);

    private sealed record ScheduleRetry(
        Func<Task<object>> Operation, int MaxAttempts, TimeSpan InitialDelay, int Attempt, IActorRef OriginalSender);

    /// <summary>
    /// Gets or sets the timer scheduler used for scheduling delayed retries.
    /// </summary>
    public ITimerScheduler? Timers { get; set; }

    private int _timerCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryActor"/> class.
    /// </summary>
    /// <param name="options">Retry configuration options.</param>
    /// <param name="logger">Optional logger instance.</param>
    public RetryActor(RetryOptions options, ILogger<RetryActor>? logger = null)
    {
        _options = options;
        _logger = logger;
        ReceiveAsync<Execute>(HandleExecute);
        Receive<ScheduleRetry>(HandleScheduleRetry);
    }

    /// <summary>Handles an execute request by starting the first attempt.</summary>
    private async Task HandleExecute(Execute msg)
    {
        await ExecuteAttemptInternal(msg.Operation, msg.MaxAttempts, msg.InitialDelay, attempt: 1, Sender);
    }

    /// <summary>Executes a single attempt and schedules a retry on failure if attempts remain.</summary>
    private async Task ExecuteAttemptInternal(
        Func<Task<object>> operation, int maxAttempts, TimeSpan initialDelay, int attempt, IActorRef originalSender)
    {
        try
        {
            LogDebug("Attempt {0} executing...", attempt);
            var result = await operation();
            LogDebug("Success on attempt {0}", attempt);
            originalSender.Tell(result);
        }
        catch (Exception ex)
        {
            LogDebug("Attempt {0} failed: {1}", attempt, ex.Message);

            if (attempt >= maxAttempts)
            {
                originalSender.Tell(new Status.Failure(ex));
                return;
            }

            var delay = TimeSpan.FromMilliseconds(initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

            LogDebug("Retrying in {0}s...", delay.TotalSeconds);

            _timerCounter++;
            var timerKey = $"retry-{_timerCounter}-{attempt}";
            Timers!.StartSingleTimer(
                timerKey,
                new ScheduleRetry(operation, maxAttempts, initialDelay, attempt + 1, originalSender),
                delay);
        }
    }

    /// <summary>Handles a scheduled retry by executing the next attempt.</summary>
    private void HandleScheduleRetry(ScheduleRetry msg)
    {
        ExecuteAttemptAsync(msg.Operation, msg.MaxAttempts, msg.InitialDelay, msg.Attempt, msg.OriginalSender);
    }

    /// <summary>Fire-forget wrapper that executes an attempt asynchronously.</summary>
    private async void ExecuteAttemptAsync(
        Func<Task<object>> operation, int maxAttempts, TimeSpan initialDelay, int attempt, IActorRef originalSender)
    {
        await ExecuteAttemptInternal(operation, maxAttempts, initialDelay, attempt, originalSender);
    }

    /// <summary>Logs a debug message via the logger or writes to the console.</summary>
    private void LogDebug(string message, params object?[] args)
    {
        if (_logger is not null)
        {
            _logger.LogDebug(message, args);
        }
        else if (_options.LogLevel <= LogLevel.Debug)
        {
            Console.WriteLine($"[Resilience] {string.Format(message, args)}");
        }
    }
}
