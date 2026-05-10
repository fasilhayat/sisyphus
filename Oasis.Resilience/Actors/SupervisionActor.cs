namespace Oasis.Resilience.Actors;

using Akka.Actor;
using Akka.Pattern;
using Attributes;

/// <summary>
/// Factory for creating supervisor actor props based on the configured supervision strategy.
/// The wrapped child is a stateless <see cref="OperationRunner"/> that executes <see cref="RunOperation"/> messages,
/// allowing the supervisor to be cached and reused across invocations.
/// </summary>
public static class SupervisionActor
{
    /// <summary>
    /// Creates actor props for a supervisor that wraps an <see cref="OperationRunner"/> with the given
    /// supervision strategy and resolved timing parameters.
    /// </summary>
    /// <param name="strategy">The supervision strategy to apply.</param>
    /// <param name="maxRetries">The maximum number of retries to apply to the supervision strategy.</param>
    /// <param name="backoffMinMs">The minimum backoff in milliseconds.</param>
    /// <param name="backoffMaxMs">The maximum backoff in milliseconds.</param>
    /// <param name="randomFactor">The random factor for jitter.</param>
    /// <returns>Props for the supervisor actor.</returns>
    public static Props CreateSupervisorProps(
        SupervisionStrategy strategy,
        int maxRetries,
        int backoffMinMs,
        int backoffMaxMs,
        double randomFactor)
    {
        var childProps = Props.Create(() => new OperationRunner());

        return strategy switch
        {
            SupervisionStrategy.RestartWithBackoff => CreateBackoffProps(childProps, backoffMinMs, backoffMaxMs, randomFactor),
            SupervisionStrategy.Restart => CreateSupervisedWrapperProps(childProps, Directive.Restart, maxRetries),
            SupervisionStrategy.Stop => CreateSupervisedWrapperProps(childProps, Directive.Stop, maxRetries),
            SupervisionStrategy.Escalate => CreateSupervisedWrapperProps(childProps, Directive.Escalate, maxRetries),
            SupervisionStrategy.Resume => CreateSupervisedWrapperProps(childProps, Directive.Resume, maxRetries),
            _ => childProps
        };
    }

    /// <summary>Creates props for a backoff supervisor with the configured timing.</summary>
    private static Props CreateBackoffProps(Props childProps, int minMs, int maxMs, double factor)
    {
        return BackoffSupervisor.Props(
            childProps, "runner",
            TimeSpan.FromMilliseconds(minMs),
            TimeSpan.FromMilliseconds(maxMs),
            factor);
    }

    /// <summary>Creates props for a supervised wrapper with a one-for-one strategy using the given directive.</summary>
    private static Props CreateSupervisedWrapperProps(Props childProps, Directive directive, int maxRetries)
    {
        var strategy = new OneForOneStrategy(maxRetries, TimeSpan.FromMinutes(1), _ => directive);
        return Props.Create(() => new SupervisedWrapper(childProps, strategy));
    }
}
