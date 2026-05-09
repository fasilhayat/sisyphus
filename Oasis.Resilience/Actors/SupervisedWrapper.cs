namespace Oasis.Resilience.Actors;

using Akka.Actor;

/// <summary>
/// An Akka.NET actor that wraps a child actor with a custom supervision strategy, forwarding all messages to the child.
/// </summary>
public sealed class SupervisedWrapper : ReceiveActor
{
    private readonly IActorRef _child;
    private readonly SupervisorStrategy _strategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisedWrapper"/> class.
    /// </summary>
    /// <param name="childProps">The props for the child actor.</param>
    /// <param name="strategy">The supervision strategy to apply to the child.</param>
    public SupervisedWrapper(Props childProps, SupervisorStrategy strategy)
    {
        _strategy = strategy;
        _child = Context.ActorOf(childProps, "child");
        Receive<object>(msg => _child.Forward(msg));
    }

    /// <summary>
    /// Returns the custom supervision strategy for this actor.
    /// </summary>
    protected override SupervisorStrategy SupervisorStrategy() => _strategy;
}
