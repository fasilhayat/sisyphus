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
    /// <param name="OperationKey">Optional label used for metrics (e.g. "IMyService.MyMethod").</param>
    public sealed record Execute(Func<Task<object>> Operation, int MaxAttempts, TimeSpan InitialDelay, Type[]? RetryOn = null, string OperationKey = "");
    
    /// <summary>
    /// Represents the state and configuration for a scheduled retry operation, including the operation to execute,
    /// retry limits, delay strategy, and exception types to retry on.
    /// </summary>
    /// <param name="Operation">A delegate representing the asynchronous operation to be executed and potentially retried.</param>
    /// <param name="MaxAttempts">The maximum number of retry attempts allowed for the operation. Must be greater than zero.</param>
    /// <param name="InitialDelay">The initial delay to wait before the first retry attempt. Subsequent retries may use this value to calculate
    /// backoff.</param>
    /// <param name="Attempt">The current attempt number, starting from zero for the initial execution.</param>
    /// <param name="OriginalSender">The actor reference representing the original sender of the request. Used to reply with the operation result or
    /// failure.</param>
    /// <param name="RetryOn">An optional array of exception types that should trigger a retry if thrown by the operation. If null or empty,
    /// all exceptions may be retried.</param>
    /// <param name="OperationKey">Label used for metrics tagging.</param>
    private sealed record ScheduleRetry(Func<Task<object>> Operation, int MaxAttempts, TimeSpan InitialDelay, int Attempt, IActorRef OriginalSender, Type[]? RetryOn, string OperationKey);

    /// <summary>
    /// Gets or sets the timer scheduler used for scheduling delayed retries.
    /// </summary>
    public ITimerScheduler? Timers { get; set; }

    /// <summary>
    /// Timer counter used to generate unique keys for scheduled retries, ensuring that multiple concurrent retry operations do not interfere with each other's timers.
    /// </summary>
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

    /// <summary>
    /// Processes the specified execute message by initiating the associated operation with retry logic.
    /// </summary>
    /// <param name="msg">The execute message containing the operation to perform, maximum retry attempts, initial delay, and retry
    /// condition.</param>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    private async Task HandleExecute(Execute msg)
    {
        await ExecuteAttemptInternal(msg.Operation, msg.MaxAttempts, msg.InitialDelay, attempt: 1, Sender, msg.RetryOn, msg.OperationKey);
    }

    /// <summary>
    /// Handles a schedule retry message by initiating a retry attempt for the specified operation.
    /// </summary>
    /// <param name="msg">The schedule retry message containing details about the operation to retry, including the operation delegate,
    /// maximum attempts, initial delay, current attempt number, original sender, and retry conditions. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleScheduleRetry(ScheduleRetry msg)
    {
        await ExecuteAttemptInternal(msg.Operation, msg.MaxAttempts, msg.InitialDelay, msg.Attempt, msg.OriginalSender, msg.RetryOn, msg.OperationKey);
    }

    /// <summary>
    /// Attempts to execute the specified asynchronous operation, handling retries and communicating the result or
    /// failure to the original sender.
    /// </summary>
    /// <param name="operation">A delegate representing the asynchronous operation to execute. The delegate should return a task that produces
    /// the operation result.</param>
    /// <param name="maxAttempts">The maximum number of attempts to execute the operation before giving up. Must be greater than zero.</param>
    /// <param name="initialDelay">The initial delay to wait before retrying the operation after a failure. Used to control the retry interval.</param>
    /// <param name="attempt">The current attempt number, starting from 1. Used to track the number of execution attempts.</param>
    /// <param name="originalSender">The actor reference to which the result or failure notification will be sent.</param>
    /// <param name="retryOn">An optional array of exception types that should trigger a retry if thrown by the operation. If null, all
    /// exceptions are considered for retry.</param>
    /// <param name="operationKey">Label used for metrics tagging (e.g. "IMyService.MyMethod").</param>
    /// <returns>A task that represents the asynchronous execution of the operation attempt and any subsequent retries.</returns>
    private async Task ExecuteAttemptInternal(Func<Task<object>> operation, int maxAttempts, TimeSpan initialDelay, int attempt, IActorRef originalSender, Type[]? retryOn, string operationKey)
    {
        try
        {
            LogDebug("Attempt {0} executing...", attempt);
            ResilienceMeter.RetryAttempts.WithLabels(operationKey).Inc();
            var result = await operation();
            LogDebug("Success on attempt {0}", attempt);
            originalSender.Tell(result);
        }
        catch (Exception ex)
        {
            ResilienceMeter.RetryFailures.WithLabels(operationKey).Inc();
            HandleAttemptFailure(operation, maxAttempts, initialDelay, attempt, originalSender, retryOn, ex, operationKey);
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
        Exception ex,
        string operationKey)
    {
        LogDebug("Attempt {0} failed: {1}", attempt, ex.Message);

        if (!IsRetryable(ex, retryOn) || attempt >= maxAttempts)
        {
            originalSender.Tell(new Status.Failure(ex));
            return;
        }

        ScheduleNextAttempt(operation, maxAttempts, initialDelay, attempt, originalSender, retryOn, operationKey);
    }

    /// <summary>Schedules the next retry attempt with capped exponential backoff and optional jitter.</summary>
    private void ScheduleNextAttempt(
        Func<Task<object>> operation,
        int maxAttempts,
        TimeSpan initialDelay,
        int attempt,
        IActorRef originalSender,
        Type[]? retryOn,
        string operationKey)
    {
        var delay = ComputeBackoff(initialDelay, attempt);
        LogDebug("Retrying in {0}s...", delay.TotalSeconds);

        _timerCounter++;
        var timerKey = $"retry-{_timerCounter}-{attempt}";
        Timers!.StartSingleTimer(
            timerKey,
            new ScheduleRetry(operation, maxAttempts, initialDelay, attempt + 1, originalSender, retryOn, operationKey),
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
