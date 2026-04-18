namespace Oasis.Resilience;

using Akka.Actor;
using Oasis.Resilience.Actors;

internal sealed class ResilienceRuntime
{
    public ActorSystem System { get; } = ActorSystem.Create("resilience-system");

    public IActorRef Actor { get; }

    public ResilienceRuntime()
    {
        Actor = System.ActorOf(Props.Create<ResilienceActor>(), "resilience");
    }

    public void Shutdown()
    {
        System.Terminate().Wait();
    }
}