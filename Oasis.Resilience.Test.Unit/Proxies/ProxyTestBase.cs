namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Akka.Configuration;

/// <summary>
/// Base class for proxy tests that manages <see cref="ActorSystem"/> lifecycle.
/// </summary>
public abstract class ProxyTestBase : IAsyncDisposable
{
    /// <summary>
    /// Tracks all created actor systems for cleanup during disposal.
    /// </summary>
    private readonly List<ActorSystem> _actorSystems = new();

    /// <summary>
    /// Creates an <see cref="ActorSystem"/> with the specified name and test configuration.
    /// </summary>
    /// <param name="name">The name of the actor system.</param>
    /// <returns>The created <see cref="ActorSystem"/>.</returns>
    protected ActorSystem CreateActorSystem(string name)
    {
        var config = ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
            akka.suppress-json-serializer-warning = on
            akka.log-config-on-start = off
            akka.coordinated-shutdown.terminate-actor-system = on
            akka.coordinated-shutdown.exit-clr = off
            akka.coordinated-shutdown.log-level = ERROR
        ");

        var system = ActorSystem.Create(name, config);
        _actorSystems.Add(system);
        return system;
    }

    /// <summary>
    /// Creates an <see cref="ActorSystem"/> with a unique generated name.
    /// </summary>
    /// <returns>The created <see cref="ActorSystem"/>.</returns>
    protected ActorSystem CreateActorSystem()
    {
        return CreateActorSystem($"test-system-{Guid.NewGuid()}");
    }

    /// <summary>
    /// Terminates all actor systems and releases resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var system in _actorSystems)
        {
            await system.Terminate();
        }
        _actorSystems.Clear();
    }
}
