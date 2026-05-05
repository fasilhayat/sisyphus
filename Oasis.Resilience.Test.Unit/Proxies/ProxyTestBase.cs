namespace Oasis.Resilience.Test.Unit.Proxies;

using Akka.Actor;
using Akka.Configuration;

/// <summary>
/// Base class for proxy tests that provides ActorSystem creation with suppressed logging.
/// </summary>
public abstract class ProxyTestBase : IDisposable
{
    private readonly List<ActorSystem> _actorSystems = new();

    /// <summary>
    /// Creates an ActorSystem with logging suppressed to prevent CoordinatedShutdown messages.
    /// </summary>
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
    /// Creates an ActorSystem with logging suppressed, using a unique name.
    /// </summary>
    protected ActorSystem CreateActorSystem()
    {
        return CreateActorSystem($"test-system-{Guid.NewGuid()}");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var system in _actorSystems)
        {
            system.Terminate().Wait(TimeSpan.FromSeconds(5));
        }
        _actorSystems.Clear();
    }
}
