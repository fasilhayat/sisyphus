namespace Oasis.Resilience;

/// <summary>
/// Constants and helpers for wiring Oasis.Resilience instrumentation into an observability pipeline.
/// </summary>
/// <example>
/// Register with OpenTelemetry in your application host:
/// <code>
/// using var meterProvider = Sdk.CreateMeterProviderBuilder()
///     .AddMeter(ResilienceObservability.MeterName)
///     .AddPrometheusHttpListener(o => o.UriPrefixes = ["http://localhost:9464/"])
///     .Build();
/// </code>
/// </example>
public static class ResilienceObservability
{
    /// <summary>
    /// The meter name used by Oasis.Resilience for all <see cref="System.Diagnostics.Metrics"/> instrumentation.
    /// Pass this to <c>MeterProviderBuilder.AddMeter()</c> to subscribe.
    /// </summary>
    public const string MeterName = "Oasis.Resilience";
}
