namespace Oasis.Resilience.Actors;

using Akka.Actor;
using Microsoft.Extensions.Logging;

/// <summary>
/// An Akka.NET actor that executes operations with retry logic and exponential backoff,
/// optionally filtering which exceptions trigger a retry.
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
    /// <param name="RetryOn">Optional exception types to retry on; <c>null</c> retries every exception.</param>
    public sealed record Execute(
        Func<Task<object>> Operation,
        int MaxAttempts,
        TimeSpan InitialDelay,
        Type[]? RetryOn = null);

    private sealed record ScheduleRetry(
        Func<Task<object>> Operation,
        int MaxAttempts,
        TimeSpan InitialDelay,
        int Attempt,
        IActorRef OriginalSender,
        Type[]? RetryOn);

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
        ReceiveAsync<ScheduleRetry>(HandleScheduleRetry);
    }

    /// <summary>Handles an execute request by starting the first attempt.</summary>
    private async Task HandleExecute(Execute msg)
    {
        await ExecuteAttemptInternal(msg.Operation, msg.MaxAttempts, msg.InitialDelay, attempt: 1, Sender, msg.RetryOn);
    }

    /// <summary>Handles a scheduled retry by executing the next attempt asynchronously.</summary>
    private async Task HandleScheduleRetry(ScheduleRetry msg)
    {
        await ExecuteAttemptInternal(msg.Operation, msg.MaxAttempts, msg.InitialDelay, msg.Attempt, msg.OriginalSender, msg.RetryOn);
    }

    /// <summary>Executes a single attempt and schedules a retry on failure if attempts remain and the exception is retryable.</summary>
    private async Task ExecuteAttemptInternal(
        Func<Task<object>> operation,
        int maxAttempts,
        TimeSpan initialDelay,
        int attempt,
        IActorRef originalSender,
        Type[]? retryOn)
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
            HandleAttemptFailure(operation, maxAttempts, initialDelay, attempt, originalSender, retryOn, ex);
        }
    }

    /// <summary>Decides whether to fail or schedule another retry attempt for the given exception.</summary>
    private void HandleAttemptFailure(
        Func<Task<object>> operation,
        int maxAttempts,
        TimeSpan initialDelay,
        int attempt,
        IActorRef originalSender,
        Type[]? retryOn,
        Exception ex)
    {
        LogDebug("Attempt {0} failed: {1}", attempt, ex.Message);

        if (!IsRetryable(ex, retryOn) || attempt >= maxAttempts)
        {
            originalSender.Tell(new Status.Failure(ex));
            return;
        }

        ScheduleNextAttempt(operation, maxAttempts, initialDelay, attempt, originalSender, retryOn);
    }

    /// <summary>Schedules the next retry attempt with capped exponential backoff and optional jitter.</summary>
    private void ScheduleNextAttempt(
        Func<Task<object>> operation,
        int maxAttempts,
        TimeSpan initialDelay,
        int attempt,
        IActorRef originalSender,
        Type[]? retryOn)
    {
        var delay = ComputeBackoff(initialDelay, attempt);
        LogDebug("Retrying in {0}s...", delay.TotalSeconds);

        _timerCounter++;
        var timerKey = $"retry-{_timerCounter}-{attempt}";
        Timers!.StartSingleTimer(
            timerKey,
            new ScheduleRetry(operation, maxAttempts, initialDelay, attempt + 1, originalSender, retryOn),
            delay);
    }

    /// <summary>Computes exponential backoff capped by <see cref="RetryOptions.MaxDelayMs"/> with optional jitter.</summary>
    private TimeSpan ComputeBackoff(TimeSpan initialDelay, int attempt)
    {
        var capMs = (double)_options.MaxDelayMs;
        var exponent = Math.Min(attempt - 1, 30);
        var rawMs = initialDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var clampedMs = Math.Min(rawMs, capMs);
        var jitteredMs = ApplyJitter(clampedMs, _options.JitterFactor);
        return TimeSpan.FromMilliseconds(jitteredMs);
    }

    /// <summary>Applies a symmetric multiplicative jitter to <paramref name="delayMs"/>.</summary>
    private static double ApplyJitter(double delayMs, double jitterFactor)
    {
        if (jitterFactor <= 0) return delayMs;
        var clamped = Math.Min(jitterFactor, 1.0);
        var rand = Random.Shared.NextDouble();
        var multiplier = 1.0 + ((rand * 2.0 - 1.0) * clamped);
        return delayMs * multiplier;
    }

    /// <summary>Determines whether the supplied exception matches the retry-on filter (or all exceptions when filter is null).</summary>
    private static bool IsRetryable(Exception ex, Type[]? retryOn)
    {
        if (retryOn is null || retryOn.Length == 0)
            return true;

        var exceptionType = ex.GetType();
        foreach (var allowed in retryOn)
        {
            if (allowed.IsAssignableFrom(exceptionType))
                return true;
        }
        return false;
    }

    /// <summary>Logs a debug message via the logger or writes to the console when no logger is provided.</summary>
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
