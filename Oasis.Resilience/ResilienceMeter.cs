namespace Oasis.Resilience;

using Prometheus;

/// <summary>
/// Prometheus metrics for Oasis.Resilience, emitted via prometheus-net.
/// Metrics are available at the <c>/metrics</c> endpoint exposed by the consuming application.
/// </summary>
internal static class ResilienceMeter
{
    internal static readonly Counter RetryAttempts = Metrics.CreateCounter(
        "resilience_retry_attempts_total",
        "Total number of operation attempts, including the initial call and every retry.",
        new CounterConfiguration { LabelNames = ["operation"] });

    internal static readonly Counter RetryFailures = Metrics.CreateCounter(
        "resilience_retry_failures_total",
        "Total number of failed attempts (each retry that throws counts as one failure).",
        new CounterConfiguration { LabelNames = ["operation"] });

    internal static readonly Counter CircuitTransitions = Metrics.CreateCounter(
        "resilience_circuit_transitions_total",
        "Number of circuit breaker state transitions, labelled by operation and to_state.",
        new CounterConfiguration { LabelNames = ["operation", "to_state"] });

    internal static readonly Gauge CircuitState = Metrics.CreateGauge(
        "resilience_circuit_state",
        "Current circuit breaker state per operation: 0=Closed, 1=HalfOpen, 2=Open.",
        new GaugeConfiguration { LabelNames = ["operation"] });

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
