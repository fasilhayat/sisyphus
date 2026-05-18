namespace Oasis.Resilience;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

/// <summary>
/// Vendor-neutral metrics for Oasis.Resilience, emitted via <see cref="System.Diagnostics.Metrics"/>.
/// Consumers subscribe by calling <c>AddMeter(<see cref="ResilienceObservability.MeterName"/>)</c>
/// on their OpenTelemetry <c>MeterProviderBuilder</c> and attaching any exporter they choose.
/// </summary>
internal static class ResilienceMeter
{
    private static readonly Meter Meter = new(ResilienceObservability.MeterName, "1.0");

    // Tracks the last-known state per operation key so the observable gauge
    // can report current values on every Prometheus scrape.
    // Values: 0 = Closed, 1 = HalfOpen, 2 = Open
    private static readonly ConcurrentDictionary<string, int> CircuitStates = new();

    internal static readonly Counter<long> RetryAttempts =
        Meter.CreateCounter<long>(
            "resilience_retry_attempts_total",
            description: "Total number of operation attempts, including the initial call and every retry.");

    internal static readonly Counter<long> RetryFailures =
        Meter.CreateCounter<long>(
            "resilience_retry_failures_total",
            description: "Total number of failed attempts (each retry that throws counts as one failure).");

    internal static readonly Counter<long> CircuitTransitions =
        Meter.CreateCounter<long>(
            "resilience_circuit_transitions_total",
            description: "Number of circuit breaker state transitions, labelled by operation and to_state.");

    static ResilienceMeter()
    {
        // The observable gauge is polled on every metrics scrape.
        // It reports one Measurement per tracked operation key.
        Meter.CreateObservableGauge(
            "resilience_circuit_state",
            observeValues: () => CircuitStates.Select(kv =>
                new Measurement<int>(
                    kv.Value,
                    new KeyValuePair<string, object?>("operation", kv.Key))),
            description: "Current circuit breaker state per operation: 0=Closed, 1=HalfOpen, 2=Open.");
    }

    /// <summary>Updates the tracked circuit state for <paramref name="operationKey"/> and increments the transitions counter.</summary>
    /// <param name="operationKey">The operation key identifying the circuit.</param>
    /// <param name="state">Numeric state value: 0=Closed, 1=HalfOpen, 2=Open.</param>
    /// <param name="stateName">Human-readable label used on the transitions counter (e.g. "Open", "HalfOpen", "Closed").</param>
    internal static void RecordCircuitTransition(string operationKey, int state, string stateName)
    {
        CircuitStates[operationKey] = state;
        CircuitTransitions.Add(1,
            new KeyValuePair<string, object?>("operation", operationKey),
            new KeyValuePair<string, object?>("to_state", stateName));
    }
}
