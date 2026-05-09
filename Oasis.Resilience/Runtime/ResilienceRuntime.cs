namespace Oasis.Resilience;

using Akka.Actor;
using Akka.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oasis.Resilience.Actors;

/// <summary>
/// Manages the Akka.NET actor system and creates the core resilience actors (retry, circuit breaker).
/// </summary>
internal sealed class ResilienceRuntime : IDisposable, IAsyncDisposable
{
    private static readonly Config DefaultConfig = ConfigurationFactory.ParseString(@"
        akka.loglevel = ERROR
        akka.stdout-loglevel = ERROR
        akka.suppress-json-serializer-warning = on
        akka.log-config-on-start = off
        akka.coordinated-shutdown.log-level = ERROR
    ");

    private readonly RetryOptions _retryOptions;

    /// <summary>
    /// Gets the Akka.NET actor system instance.
    /// </summary>
    public ActorSystem System { get; }

    /// <summary>
    /// Gets the actor ref for the retry actor.
    /// </summary>
    public IActorRef RetryActor { get; }

    /// <summary>
    /// Gets the actor ref for the circuit breaker actor.
    /// </summary>
    public IActorRef CircuitBreakerActor { get; }

    /// <summary>
    /// Gets the supervision options configured for this runtime.
    /// </summary>
    public SupervisionOptions SupervisionOptions { get; }

    /// <summary>
    /// Gets the fan-out options configured for this runtime.
    /// </summary>
    public FanOutOptions FanOutOptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilienceRuntime"/> class.
    /// </summary>
    /// <param name="retryOptions">Retry configuration options.</param>
    /// <param name="breakerOptions">Circuit breaker configuration options.</param>
    /// <param name="supervisionOptions">Supervision configuration options.</param>
    /// <param name="fanOutOptions">Fan-out configuration options.</param>
    /// <param name="loggerFactory">Optional logger factory for creating actor loggers.</param>
    /// <param name="config">Optional Akka.NET configuration. Uses a default with ERROR log level if not specified.</param>
    public ResilienceRuntime(
        IOptions<RetryOptions> retryOptions,
        IOptions<CircuitBreakerOptions> breakerOptions,
        IOptions<SupervisionOptions> supervisionOptions,
        IOptions<FanOutOptions> fanOutOptions,
        ILoggerFactory? loggerFactory = null,
        Config? config = null)
    {
        _retryOptions = retryOptions.Value;
        _ = breakerOptions.Value;
        SupervisionOptions = supervisionOptions.Value;
        FanOutOptions = fanOutOptions.Value;

        System = ActorSystem.Create("resilience-system", config ?? DefaultConfig);

        var retryLogger = loggerFactory?.CreateLogger<RetryActor>();
        var breakerLogger = loggerFactory?.CreateLogger<CircuitBreakerActor>();

        RetryActor = System.ActorOf(Props.Create(() => new RetryActor(_retryOptions, retryLogger)), "resilience");
        CircuitBreakerActor = System.ActorOf(
            Props.Create(() => new CircuitBreakerActor(_retryOptions.LogLevel, breakerLogger)), "circuit-breaker");
    }

    /// <summary>
    /// Gracefully shuts down the actor system.
    /// </summary>
    public async Task ShutdownAsync()
    {
        await System.Terminate();
    }

    /// <summary>
    /// Disposes the actor system resources synchronously.
    /// </summary>
    public void Dispose()
    {
        System.Terminate().Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Disposes the actor system resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await System.Terminate();
    }
}
