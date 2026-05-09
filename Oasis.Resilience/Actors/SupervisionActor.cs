namespace Oasis.Resilience.Actors;

using Akka.Actor;
using Akka.Pattern;
using Attributes;

/// <summary>
/// Factory for creating supervisor actor props based on the configured supervision strategy.
/// </summary>
public static class SupervisionActor
{
    /// <summary>
    /// Creates actor props for a supervisor that wraps an operation with the given supervision strategy.
    /// </summary>
    /// <param name="operation">The operation to supervise.</param>
    /// <param name="supervision">The supervision configuration.</param>
    /// <returns>Props for the supervisor actor.</returns>
    public static Props CreateSupervisorProps(
        Func<Task<object>> operation,
        SupervisionAttribute supervision)
    {
        var childProps = Props.Create(() => new OperationRunner(operation));

        return supervision.Strategy switch
        {
            SupervisionStrategy.RestartWithBackoff => BackoffSupervisor.Props(
                childProps, "runner",
                TimeSpan.FromMilliseconds(supervision.BackoffMinMs),
                TimeSpan.FromMilliseconds(supervision.BackoffMaxMs),
                supervision.RandomFactor),

            SupervisionStrategy.Restart => BackoffSupervisor.Props(
                childProps, "runner",
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(1),
                0),

            SupervisionStrategy.Stop => CreateSupervisedWrapperProps(childProps, Directive.Stop),
            SupervisionStrategy.Escalate => CreateSupervisedWrapperProps(childProps, Directive.Escalate),
            SupervisionStrategy.Resume => CreateSupervisedWrapperProps(childProps, Directive.Resume),
            _ => childProps
        };
    }

    /// <summary>Creates props for a supervised wrapper with a one-for-one strategy using the given directive.</summary>
    private static Props CreateSupervisedWrapperProps(Props childProps, Directive directive)
    {
        var strategy = new OneForOneStrategy(0, TimeSpan.FromMinutes(1), _ => directive);
        return Props.Create(() => new SupervisedWrapper(childProps, strategy));
    }
}
