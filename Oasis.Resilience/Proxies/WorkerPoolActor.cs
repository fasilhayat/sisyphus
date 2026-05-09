namespace Oasis.Resilience.Proxies;

using Akka.Actor;

/// <summary>
/// Configuration props for a worker pool, including worker props and supervision strategy.
/// </summary>
/// <param name="WorkerProps">Props for creating worker actors.</param>
/// <param name="Strategy">The supervision strategy for the pool.</param>
internal sealed record WorkerPoolProps(Props WorkerProps, SupervisorStrategy Strategy);

/// <summary>
/// An Akka.NET actor that manages a single worker child with a configurable supervision strategy,
/// forwarding all messages to the worker.
/// </summary>
internal sealed class WorkerPoolActor : ReceiveActor
{
    private readonly IActorRef _worker;
    private readonly SupervisorStrategy _strategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerPoolActor"/> class.
    /// </summary>
    /// <param name="props">The worker pool configuration props.</param>
    public WorkerPoolActor(WorkerPoolProps props)
    {
        _strategy = props.Strategy;
        _worker = Context.ActorOf(props.WorkerProps, "worker");
        Receive<object>(msg => _worker.Forward(msg));
    }

    /// <summary>
    /// Returns the custom supervision strategy for the worker pool.
    /// </summary>
    protected override SupervisorStrategy SupervisorStrategy() => _strategy;
}
