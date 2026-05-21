namespace Oasis.Resilience.Actors;

using Akka.Actor;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

/// <summary>
/// An Akka.NET actor that implements the circuit breaker pattern, tracking failure counts per operation key
/// and transitioning between Closed, Open, and HalfOpen states.
/// </summary>
public sealed class CircuitBreakerActor : ReceiveActor
{
    /// <summary>
    /// Defines the possible states of a circuit breaker.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>Circuit is closed; calls are allowed through.</summary>
        Closed,
        /// <summary>Circuit is open; calls are rejected.</summary>
        Open,
        /// <summary>Circuit is half-open; a limited number of test calls are allowed.</summary>
        HalfOpen
    }

    /// <summary>
    /// Instructs the actor to execute an operation with circuit breaker protection.
    /// </summary>
    /// <param name="OperationKey">Unique key identifying the operation.</param>
    /// <param name="Operation">The operation to execute.</param>
    /// <param name="FailureThreshold">Number of consecutive failures before opening.</param>
    /// <param name="ResetTimeout">Duration before transitioning to half-open.</param>
    /// <param name="MaxConcurrentCalls">Max concurrent test calls in half-open state.</param>
    public sealed record ExecuteWithBreaker(string OperationKey, Func<Task<object>> Operation, int FailureThreshold, TimeSpan ResetTimeout, int MaxConcurrentCalls);

    /// <summary>
    /// Signals a successful operation execution.
    /// </summary>
    /// <param name="OperationKey">Unique key identifying the operation.</param>
    public sealed record Success(string OperationKey);

    /// <summary>
    /// Signals a failed operation execution.
    /// </summary>
    /// <param name="OperationKey">Unique key identifying the operation.</param>
    /// <param name="Exception">The exception that occurred.</param>
    public sealed record Failure(string OperationKey, Exception Exception);

    /// <summary>
    /// Requests the current state of a circuit breaker.
    /// </summary>
    /// <param name="OperationKey">Unique key identifying the operation.</param>
    public sealed record GetState(string OperationKey);

    /// <summary>
    /// Response containing the current state and failure count of a circuit breaker.
    /// </summary>
    /// <param name="OperationKey">Unique key identifying the operation.</param>
    /// <param name="State">The current circuit state.</param>
    /// <param name="FailureCount">The number of consecutive failures.</param>
    public sealed record StateResponse(string OperationKey, CircuitState State, int FailureCount);

    /// <summary>
    /// Exception thrown when a circuit breaker is open and calls are being rejected.
    /// </summary>
    public sealed class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
        /// </summary>
        /// <param name="operationKey">The operation key that is blocked.</param>
        /// <param name="remainingTime">The remaining time before the circuit transitions to half-open.</param>
        public CircuitBreakerOpenException(string operationKey, TimeSpan remainingTime)
            : base($"Circuit breaker is open for '{operationKey}'. Retry after {remainingTime.TotalMilliseconds}ms.")
        {
            OperationKey = operationKey;
            RemainingTime = remainingTime;
        }

        /// <summary>Gets the operation key that is blocked.</summary>
        public string OperationKey { get; }

        /// <summary>Gets the remaining time before the circuit transitions to half-open.</summary>
        public TimeSpan RemainingTime { get; }
    }

    private readonly LogLevel _logLevel;
    private readonly ILogger<CircuitBreakerActor>? _logger;
    private readonly ConcurrentDictionary<string, BreakerState> _breakers = new();
    private readonly ConcurrentDictionary<string, int> _inFlightCounts = new();

    /// <summary>Carries the result of an async operation back to the actor via <c>PipeTo</c>,
    /// preserving the original sender so the response reaches the correct ask-actor.</summary>
    private sealed record OperationResult(string OperationKey, object? Result, Exception? Exception, IActorRef OriginalSender);

    private sealed record BreakerState(
        CircuitState State,
        int FailureCount,
        int SuccessCount,
        DateTime? OpenedAt,
        int MaxConcurrentCalls,
        TimeSpan ResetTimeout,
        int FailureThreshold);

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerActor"/> class.
    /// </summary>
    /// <param name="logLevel">The log level for console output when no logger is provided.</param>
    /// <param name="logger">Optional logger instance.</param>
    public CircuitBreakerActor(LogLevel logLevel = LogLevel.Debug, ILogger<CircuitBreakerActor>? logger = null)
    {
        _logLevel = logLevel;
        _logger = logger;

        Receive<ExecuteWithBreaker>(HandleExecuteWithBreaker);
        Receive<Success>(HandleSuccess);
        Receive<Failure>(HandleFailure);
        Receive<GetState>(HandleGetState);
        Receive<OperationResult>(HandleOperationResult);
    }

    /// <summary>Handles a breaker execution request by checking state and running the operation if allowed.</summary>
    private void HandleExecuteWithBreaker(ExecuteWithBreaker msg)
    {
        var breaker = _breakers.GetOrAdd(msg.OperationKey, _ =>
            new BreakerState(CircuitState.Closed, 0, 0, null, msg.MaxConcurrentCalls, msg.ResetTimeout, msg.FailureThreshold));

        var currentState = GetEffectiveState(breaker, breaker.ResetTimeout);

        // Detect time-based Open → HalfOpen transition and emit metric once.
        if (breaker.State == CircuitState.Open && currentState == CircuitState.HalfOpen)
        {
            Log($"Circuit breaker for '{msg.OperationKey}' is now HalfOpen (reset timeout elapsed)");
        }

        if (currentState == CircuitState.Open)
        {
            RejectOpenCircuit(msg, breaker);
            return;
        }

        if (IsHalfOpenLimitReached(msg.OperationKey, currentState, breaker))
            return;

        StartOperation(msg, breaker);
    }

    /// <summary>Rejects the operation with a <see cref="CircuitBreakerOpenException"/> when the circuit is open.</summary>
    private void RejectOpenCircuit(ExecuteWithBreaker msg, BreakerState breaker)
    {
        var remainingTime = msg.ResetTimeout - (DateTime.UtcNow - breaker.OpenedAt!.Value);
        Sender.Tell(new Status.Failure(new CircuitBreakerOpenException(msg.OperationKey, remainingTime)));
    }

    /// <summary>Checks whether the half-open concurrent call limit has been reached.</summary>
    private bool IsHalfOpenLimitReached(string operationKey, CircuitState currentState, BreakerState breaker)
    {
        if (currentState != CircuitState.HalfOpen) return false;
        var inFlight = _inFlightCounts.GetOrAdd(operationKey, 0);
        if (inFlight >= breaker.MaxConcurrentCalls)
        {
            Sender.Tell(new Status.Failure(new CircuitBreakerOpenException(operationKey, TimeSpan.Zero)));
            return true;
        }
        return false;
    }

    /// <summary>Starts the wrapped operation via <c>PipeTo</c> and returns immediately so the mailbox stays free.
    /// Tracks the in-flight call count so the half-open limit is enforced by concurrent calls, not accumulated history.</summary>
    private void StartOperation(ExecuteWithBreaker msg, BreakerState breaker)
    {
        _inFlightCounts.AddOrUpdate(msg.OperationKey, 1, (_, count) => count + 1);
        var originalSender = Sender;
        var operationKey = msg.OperationKey;

        try
        {
            var task = msg.Operation();
            task.PipeTo(
                Self,
                sender: Self,
                success: result => new OperationResult(operationKey, result, null, originalSender),
                failure: exception => new OperationResult(operationKey, null, exception, originalSender));
        }
        catch (Exception ex)
        {
            // Pass the original exception through — do not re-wrap so callers retain token/stack identity.
            if (ex is OperationCanceledException)
            {
                originalSender.Tell(new Status.Failure(ex));
            }
            else
            {
                HandleFailure(new Failure(operationKey, ex));
                originalSender.Tell(new Status.Failure(ex));
            }

            _inFlightCounts.AddOrUpdate(operationKey, 0, (_, count) => Math.Max(0, count - 1));
        }
    }

    /// <summary>Handles the async operation result delivered via <c>PipeTo</c>.
    /// <see cref="OperationCanceledException"/> is not counted as a failure — the circuit stays in its current state.</summary>
    private void HandleOperationResult(OperationResult msg)
    {
        _inFlightCounts.AddOrUpdate(msg.OperationKey, 0, (_, count) => Math.Max(0, count - 1));

        if (msg.Exception is null)
        {
            HandleSuccess(new Success(msg.OperationKey));
            msg.OriginalSender.Tell(msg.Result!);
        }
        else if (msg.Exception is OperationCanceledException)
        {
            // Pass the original exception through — do not re-wrap so callers retain token/stack identity.
            msg.OriginalSender.Tell(new Status.Failure(msg.Exception));
        }
        else
        {
            HandleFailure(new Failure(msg.OperationKey, msg.Exception));
            msg.OriginalSender.Tell(new Status.Failure(msg.Exception));
        }
    }

    /// <summary>Records a successful operation and resets the circuit breaker state. If the breaker
    /// entry doesn't exist (e.g. the message arrived after a state reset), the message is ignored —
    /// the entry will be re-created on the next <see cref="HandleExecuteWithBreaker"/> call with the
    /// correct configuration from the originating attribute.</summary>
    private void HandleSuccess(Success msg)
    {
        if (!_breakers.TryGetValue(msg.OperationKey, out var existing)) return;

        var updated = existing.State == CircuitState.HalfOpen
            ? existing with { State = CircuitState.Closed, FailureCount = 0, SuccessCount = existing.SuccessCount + 1 }
            : existing with { State = CircuitState.Closed, FailureCount = 0 };

        _breakers.TryUpdate(msg.OperationKey, updated, existing);
        Log($"Circuit breaker for '{msg.OperationKey}' is now Closed");
    }

    /// <summary>Records a failed operation. In <see cref="CircuitState.HalfOpen"/> the circuit re-opens
    /// immediately; in <see cref="CircuitState.Closed"/> it opens once the failure threshold is reached.
    /// If the breaker entry doesn't exist the message is ignored (configuration-less defaults could
    /// otherwise be applied silently).</summary>
    private void HandleFailure(Failure msg)
    {
        if (!_breakers.TryGetValue(msg.OperationKey, out var existing)) return;

        var updated = UpdateOnFailure(msg.OperationKey, existing);
        _breakers.TryUpdate(msg.OperationKey, updated, existing);
    }

    /// <summary>Computes the next breaker state after a failure, honoring the half-open re-open rule.</summary>
    private BreakerState UpdateOnFailure(string operationKey, BreakerState state)
    {
        if (state.State == CircuitState.HalfOpen)
        {
            Log($"Circuit breaker for '{operationKey}' re-opened from HalfOpen on failed trial call");
            return state with
            {
                State = CircuitState.Open,
                FailureCount = state.FailureCount + 1,
                OpenedAt = DateTime.UtcNow,
                SuccessCount = 0
            };
        }

        var newFailureCount = state.FailureCount + 1;
        if (newFailureCount >= state.FailureThreshold)
        {
            Log($"Circuit breaker for '{operationKey}' opened after {newFailureCount} failures");
            return state with
            {
                State = CircuitState.Open,
                FailureCount = newFailureCount,
                OpenedAt = DateTime.UtcNow
            };
        }

        return state with { FailureCount = newFailureCount };
    }

    /// <summary>Responds with the current state and failure count for the requested operation key.</summary>
    private void HandleGetState(GetState msg)
    {
        if (_breakers.TryGetValue(msg.OperationKey, out var state))
        {
            var effectiveState = GetEffectiveState(state, state.ResetTimeout);
            Sender.Tell(new StateResponse(msg.OperationKey, effectiveState, state.FailureCount));
        }
        else
        {
            Sender.Tell(new StateResponse(msg.OperationKey, CircuitState.Closed, 0));
        }
    }

    /// <summary>Returns the effective circuit state, transitioning to half-open if the reset timeout has elapsed.</summary>
    private CircuitState GetEffectiveState(BreakerState state, TimeSpan resetTimeout)
    {
        if (state.State == CircuitState.Open && state.OpenedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - state.OpenedAt.Value;
            if (elapsed >= resetTimeout)
                return CircuitState.HalfOpen;
        }

        return state.State;
    }

    /// <summary>Logs a debug message via the logger or writes to the console.</summary>
    private void Log(string message)
    {
        if (_logger is not null)
        {
            _logger.LogDebug("{Message}", message);
        }
        else if (_logLevel <= LogLevel.Debug)
        {
            Console.WriteLine($"[CircuitBreaker] {message}");
        }
    }
}
