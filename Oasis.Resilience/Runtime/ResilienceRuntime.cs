namespace Oasis.Resilience;

using Akka.Actor;
using Microsoft.Extensions.Options;
using Oasis.Resilience.Actors;

/// <summary>
/// Provides runtime management for the resilience actor system, including initialization and shutdown.
/// </summary>
/// <remarks>Manages the lifecycle of the underlying actor system and exposes the primary resilience actor for
/// message handling.</remarks>
internal sealed class ResilienceRuntime
{
    private readonly RetryOptions _options;

    /// <summary>
    /// Gets the actor system used for managing actors and message processing.
    /// </summary>
    public ActorSystem System { get; } = ActorSystem.Create("resilience-system");

    /// <summary>
    /// Gets the actor reference associated with this instance.
    /// </summary>
    public IActorRef Actor { get; }

    /// <summary>
    /// Initializes a new instance of the ResilienceRuntime class and creates the resilience actor.
    /// </summary>
    public ResilienceRuntime(IOptions<RetryOptions> options)
    {
        _options = options.Value;
        Actor = System.ActorOf(Props.Create(() => new RetryActor(_options)), "resilience");
    }

    /// <summary>
    /// Shuts down the system and waits for termination to complete.
    /// </summary>
    public void Shutdown()
    {
        System.Terminate().Wait();
    }
}