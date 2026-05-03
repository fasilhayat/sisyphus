namespace Oasis.Resilience;

using Akka.Actor;
using Microsoft.Extensions.Options;
using Oasis.Resilience.Actors;

/// <summary>
/// Provides runtime management for the resilience actor system, including initialization and shutdown.
/// </summary>
/// <remarks>Manages the lifecycle of the underlying actor system and exposes resilience actors for message handling.</remarks>
internal sealed class ResilienceRuntime
{
    /// <summary>
    /// Stores the retry configuration options used for the retry actor.
    /// </summary>
    private readonly RetryOptions _retryOptions;

    /// <summary>
    /// Stores the circuit breaker configuration options (unused but available for future use).
    /// </summary>
    private readonly CircuitBreakerOptions _breakerOptions;

    /// <summary>
    /// Gets the actor system used for managing actors and message processing.
    /// </summary>
    public ActorSystem System { get; } = ActorSystem.Create("resilience-system");

    /// <summary>
    /// Gets the actor reference associated with retry operations.
    /// </summary>
    public IActorRef RetryActor { get; }

    /// <summary>
    /// Gets the actor reference associated with circuit breaker operations.
    /// </summary>
    public IActorRef CircuitBreakerActor { get; }

    /// <summary>
    /// Initializes a new instance of the ResilienceRuntime class and creates the resilience actors.
    /// </summary>
    public ResilienceRuntime(IOptions<RetryOptions> retryOptions, IOptions<CircuitBreakerOptions> breakerOptions)
    {
        _retryOptions = retryOptions.Value;
        _breakerOptions = breakerOptions.Value;

        RetryActor = System.ActorOf(Props.Create(() => new RetryActor(_retryOptions)), "resilience");
        CircuitBreakerActor = System.ActorOf(Props.Create(() => new CircuitBreakerActor(_retryOptions)), "circuit-breaker");
    }

    /// <summary>
    /// Shuts down the system and waits for termination to complete.
    /// </summary>
    public void Shutdown()
    {
        System.Terminate().Wait();
    }
}