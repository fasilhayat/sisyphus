namespace Oasis.Resilience.Actors;

using Akka.Actor;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

/// <summary>
/// An Akka.NET actor that implements the circuit breaker pattern to prevent cascading failures.
/// </summary>
/// <remarks>
/// Tracks failure counts per operation and transitions between Closed, Open, and Half-Open states.
/// When the circuit is open, requests fail fast without executing the underlying operation.
/// </remarks>
public sealed class CircuitBreakerActor : ReceiveActor
{
    /// <summary>
    /// Represents the state of a circuit breaker.
    /// </summary>
    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Message to execute an operation through the circuit breaker.
    /// </summary>
    public sealed record ExecuteWithBreaker(string OperationKey, Func<Task<object>> Operation, int FailureThreshold, TimeSpan ResetTimeout, int MaxConcurrentCalls);

    /// <summary>
    /// Message reporting a successful operation.
    /// </summary>
    public sealed record Success(string OperationKey);

    /// <summary>
    /// Message reporting a failed operation.
    /// </summary>
    public sealed record Failure(string OperationKey, Exception Exception);

    /// <summary>
    /// Message to query the current state of a circuit breaker.
    /// </summary>
    public sealed record GetState(string OperationKey);

    /// <summary>
    /// Message containing the current state of a circuit breaker.
    /// </summary>
    public sealed record StateResponse(string OperationKey, CircuitState State, int FailureCount);

    /// <summary>
    /// Exception thrown when the circuit breaker is open and calls are not permitted.
    /// </summary>
    public sealed class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
        /// </summary>
        /// <param name="operationKey">The operation key associated with the circuit breaker.</param>
        /// <param name="remainingTime">The remaining time before the circuit transitions to half-open.</param>
        public CircuitBreakerOpenException(string operationKey, TimeSpan remainingTime)
            : base($"Circuit breaker is open for '{operationKey}'. Retry after {remainingTime.TotalMilliseconds}ms.")
        {
            OperationKey = operationKey;
            RemainingTime = remainingTime;
        }

        /// <summary>
        /// Gets the operation key associated with the circuit breaker.
        /// </summary>
        public string OperationKey { get; }

        /// <summary>
        /// Gets the remaining time before the circuit transitions to half-open.
        /// </summary>
        public TimeSpan RemainingTime { get; }
    }

    /// <summary>
    /// Provides resilience configuration settings used by the actor.
    /// </summary>
    private readonly RetryOptions _options;

    /// <summary>
    /// Stores the state of circuit breakers keyed by operation.
    /// </summary>
    private readonly ConcurrentDictionary<string, BreakerState> _breakers = new();

    /// <summary>
    /// Represents the immutable state of a circuit breaker.
    /// </summary>
    /// <param name="State">The current state of the circuit breaker.</param>
    /// <param name="FailureCount">The number of consecutive failures.</param>
    /// <param name="SuccessCount">The number of successful calls in half-open state.</param>
    /// <param name="OpenedAt">The time when the circuit transitioned to open state.</param>
    /// <param name="MaxConcurrentCalls">The maximum concurrent calls allowed in half-open state.</param>
    /// <param name="ResetTimeout">The duration before an open circuit transitions to half-open.</param>
    /// <param name="FailureThreshold">The number of consecutive failures required to open the circuit.</param>
    private sealed record BreakerState(
        CircuitState State,
        int FailureCount,
        int SuccessCount,
        DateTime? OpenedAt,
        int MaxConcurrentCalls,
        TimeSpan ResetTimeout,
        int FailureThreshold);

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerActor"/> class and configures message handlers.
    /// </summary>
    /// <param name="options">The resilience configuration options.</param>
    public CircuitBreakerActor(RetryOptions options)
    {
        _options = options;

        ReceiveAsync<ExecuteWithBreaker>(HandleExecuteWithBreaker);
        Receive<Success>(HandleSuccess);
        Receive<Failure>(HandleFailure);
        Receive<GetState>(HandleGetState);
    }

    /// <summary>
    /// Handles execute requests by checking circuit breaker state and executing the operation if permitted.
    /// </summary>
    /// <param name="msg">The execution request containing the operation key, delegate, and circuit breaker configuration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleExecuteWithBreaker(ExecuteWithBreaker msg)
    {
        var breaker = _breakers.GetOrAdd(msg.OperationKey, _ =>
            new BreakerState(CircuitState.Closed, 0, 0, null, msg.MaxConcurrentCalls, msg.ResetTimeout, msg.FailureThreshold));

        var currentState = GetEffectiveState(breaker, breaker.ResetTimeout);

        if (currentState == CircuitState.Open)
        {
            var remainingTime = msg.ResetTimeout - (DateTime.UtcNow - breaker.OpenedAt!.Value);
            Sender.Tell(new Status.Failure(new CircuitBreakerOpenException(msg.OperationKey, remainingTime)));
            return;
        }

        if (currentState == CircuitState.HalfOpen &&
            breaker.SuccessCount >= breaker.MaxConcurrentCalls)
        {
            Sender.Tell(new Status.Failure(new CircuitBreakerOpenException(msg.OperationKey, TimeSpan.Zero)));
            return;
        }

        try
        {
            var result = await msg.Operation();
            // Update state synchronously before sending response
            HandleSuccess(new Success(msg.OperationKey));
            Sender.Tell(result);
        }
        catch (Exception ex)
        {
            // Update state synchronously before sending response
            HandleFailure(new Failure(msg.OperationKey, ex));
            Sender.Tell(new Status.Failure(ex));
        }
    }

    /// <summary>
    /// Handles success messages by resetting the circuit breaker to closed state.
    /// </summary>
    /// <param name="msg">The success message containing the operation key.</param>
    private void HandleSuccess(Success msg)
    {
        _breakers.AddOrUpdate(
            msg.OperationKey,
            key => new BreakerState(CircuitState.Closed, 0, 1, null, 1, TimeSpan.FromSeconds(30), 5),
            (key, state) =>
            {
                if (state.State == CircuitState.HalfOpen)
                {
                    var newSuccessCount = state.SuccessCount + 1;
                    return new BreakerState(CircuitState.Closed, 0, newSuccessCount, null, state.MaxConcurrentCalls, state.ResetTimeout, state.FailureThreshold);
                }

                return new BreakerState(CircuitState.Closed, 0, state.SuccessCount, null, state.MaxConcurrentCalls, state.ResetTimeout, state.FailureThreshold);
            });

        Log($"Circuit breaker for '{msg.OperationKey}' is now Closed");
    }

    /// <summary>
    /// Handles failure messages by incrementing the failure count and potentially opening the circuit.
    /// </summary>
    /// <param name="msg">The failure message containing the operation key and exception.</param>
    private void HandleFailure(Failure msg)
    {
        _breakers.AddOrUpdate(
            msg.OperationKey,
            // This initial lambda should rarely be called since state is created in HandleExecuteWithBreaker
            key => new BreakerState(CircuitState.Open, 1, 0, DateTime.UtcNow, 1, TimeSpan.FromSeconds(30), 5),
            (key, state) =>
            {
                var newFailureCount = state.FailureCount + 1;
                if (newFailureCount >= state.FailureThreshold)
                {
                    Log($"Circuit breaker for '{msg.OperationKey}' opened after {newFailureCount} failures");
                    return new BreakerState(CircuitState.Open, newFailureCount, 0, DateTime.UtcNow, state.MaxConcurrentCalls, state.ResetTimeout, state.FailureThreshold);
                }

                return new BreakerState(state.State, newFailureCount, state.SuccessCount, state.OpenedAt, state.MaxConcurrentCalls, state.ResetTimeout, state.FailureThreshold);
            });
    }

    /// <summary>
    /// Handles state query requests by returning the current effective state of a circuit breaker.
    /// </summary>
    /// <param name="msg">The state query message containing the operation key.</param>
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

    /// <summary>
    /// Determines the effective state of a circuit breaker by checking if an open circuit has elapsed.
    /// </summary>
    /// <param name="state">The current stored state of the circuit breaker.</param>
    /// <param name="resetTimeout">The duration before an open circuit transitions to half-open.</param>
    /// <returns>The effective circuit state based on elapsed time.</returns>
    private CircuitState GetEffectiveState(BreakerState state, TimeSpan resetTimeout)
    {
        if (state.State == CircuitState.Open && state.OpenedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - state.OpenedAt.Value;
            if (elapsed >= resetTimeout)
            {
                return CircuitState.HalfOpen;
            }
        }

        return state.State;
    }

    /// <summary>
    /// Logs a message to the console when the configured log level includes debug output.
    /// </summary>
    /// <param name="message">The message to write to the console.</param>
    private void Log(string message)
    {
        if (_options.LogLevel > LogLevel.Debug)
            return;

        Console.WriteLine($"[CircuitBreaker] {message}");
    }
}
