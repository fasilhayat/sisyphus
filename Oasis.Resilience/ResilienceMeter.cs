namespace Oasis.Resilience;

using Prometheus;

/// <summary>
/// Prometheus metrics for Oasis.Resilience, emitted via prometheus-net.
/// Metrics are available at the <c>/metrics</c> endpoint exposed by the consuming application.
/// </summary>
internal static class ResilienceMeter
{
    /// <summary>
    /// Retry attempts counter, labelled by operation. Increments on every attempt, including the initial call and retries.
    /// </summary>
    /// <remarks>Each increment corresponds to an attempt to execute the operation, whether it's the first try or a retry. This allows tracking the total number of attempts made for each operation.</remarks>
    internal static readonly Counter RetryAttempts = Metrics.CreateCounter(
        "resilience_retry_attempts_total", 
        "Total number of operation attempts, including the initial call and every retry.", 
        new CounterConfiguration { LabelNames = ["operation"] });

    /// <summary>
    /// Represents a counter that tracks the total number of failed retry attempts for operations.
    /// </summary>
    /// <remarks>Each increment corresponds to a retry attempt that resulted in an exception.
    /// The counter is labeled by operation name, allowing failures to be tracked per operation.</remarks>
    internal static readonly Counter RetryFailures = Metrics.CreateCounter(
        "resilience_retry_failures_total", 
        "Total number of failed attempts (each retry that throws counts as one failure).", 
        new CounterConfiguration { LabelNames = ["operation"] });

    /// <summary>
    /// Counter for tracking the number of circuit breaker state transitions, labeled by operation and the new state.
    /// </summary>
    /// <remarks>This counter increments each time a circuit breaker changes state (e.g., from Closed to Open).
    /// The labels allow for analysis of transitions by operation and the resulting state.</remarks>
    internal static readonly Counter CircuitTransitions = Metrics.CreateCounter(
        "resilience_circuit_transitions_total", 
        "Number of circuit breaker state transitions, labelled by operation and to_state.", 
        new CounterConfiguration { LabelNames = ["operation", "to_state"] });

    /// <summary>
    /// Represents a gauge metric that tracks the current state of the circuit breaker for each operation.
    /// </summary>
    /// <remarks>The gauge uses the following values to indicate circuit breaker state: 0 for Closed, 1 for
    /// HalfOpen, and 2 for Open. The metric is labeled by operation name, allowing monitoring of circuit state per
    /// operation.</remarks>
    internal static readonly Gauge CircuitState = Metrics.CreateGauge(
        "resilience_circuit_state",
        "Current circuit breaker state per operation: 0=Closed, 1=HalfOpen, 2=Open.",
        new GaugeConfiguration { LabelNames = ["operation"] });

    /// <summary>
    /// Represents a counter that tracks the total number of fan-out worker tasks dispatched.
    /// </summary>
    /// <remarks>Use this counter to monitor how many fan-out operations have been initiated. The counter
    /// includes a label for the operation name, allowing metrics to be segmented by operation type.</remarks>
    internal static readonly Counter FanOutDispatched = Metrics.CreateCounter(
        "resilience_fanout_dispatched_total",
        "Total number of fan-out worker tasks dispatched.",
        new CounterConfiguration { LabelNames = ["operation"] });

    /// <summary>
    /// Represents a counter that tracks the total number of fan-out worker tasks that have thrown exceptions.
    /// </summary>
    /// <remarks>Use this counter to monitor and analyze the frequency of failures in fan-out operations. The
    /// counter is labeled by operation, allowing for granular tracking of failures across different
    /// operations.</remarks>
    internal static readonly Counter FanOutFailures = Metrics.CreateCounter(
        "resilience_fanout_failures_total",
        "Total number of fan-out worker tasks that threw an exception.",
        new CounterConfiguration { LabelNames = ["operation"] });

    /// <summary>Updates the circuit state gauge and increments the transitions counter.</summary>
    /// <param name="operationKey">The operation key identifying the circuit.</param>
    /// <param name="state">Numeric state: 0=Closed, 1=HalfOpen, 2=Open.</param>
    /// <param name="stateName">Human-readable label for the transitions counter.</param>
    internal static void RecordCircuitTransition(string operationKey, int state, string stateName)
    {
        CircuitState.WithLabels(operationKey).Set(state);
        CircuitTransitions.WithLabels(operationKey, stateName).Inc();
    }
}