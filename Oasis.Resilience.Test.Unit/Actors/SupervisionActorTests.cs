namespace Oasis.Resilience.Test.Unit.Actors;

using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Oasis.Resilience.Actors;
using Oasis.Resilience.Attributes;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SupervisionActor"/> covering all supervision strategies.
/// </summary>
public class SupervisionActorTests : TestKit
{
    public SupervisionActorTests() : base(GetConfig()) { }

    private static Config GetConfig() => ConfigurationFactory.ParseString("""
        akka.loglevel = ERROR
        akka.stdout-loglevel = ERROR
        akka.coordinated-shutdown.log-level = ERROR
        akka.log-config-on-start = off
        """);

    private const int DefaultMaxRetries = 3;
    private const int DefaultBackoffMin = 500;
    private const int DefaultBackoffMax = 5000;
    private const double DefaultRandomFactor = 0.2;

    [Fact]
    public void CreateSupervisorProps_with_RestartWithBackoff_returns_backoff_supervisor()
    {
        var props = SupervisionActor.CreateSupervisorProps(
            SupervisionStrategy.RestartWithBackoff,
            DefaultMaxRetries, DefaultBackoffMin, DefaultBackoffMax, DefaultRandomFactor);

        var actor = Sys.ActorOf(props, "backoff-supervisor");
        Assert.NotNull(actor);
        Assert.Equal("backoff-supervisor", actor.Path.Name);
    }

    [Fact]
    public void CreateSupervisorProps_with_Restart_returns_supervised_wrapper()
    {
        var props = SupervisionActor.CreateSupervisorProps(
            SupervisionStrategy.Restart,
            DefaultMaxRetries, DefaultBackoffMin, DefaultBackoffMax, DefaultRandomFactor);

        var actor = Sys.ActorOf(props, "restart-supervisor");
        Assert.NotNull(actor);
    }

    [Fact]
    public void CreateSupervisorProps_with_Stop_returns_supervised_wrapper()
    {
        var props = SupervisionActor.CreateSupervisorProps(
            SupervisionStrategy.Stop,
            DefaultMaxRetries, DefaultBackoffMin, DefaultBackoffMax, DefaultRandomFactor);

        var actor = Sys.ActorOf(props, "stop-supervisor");
        Assert.NotNull(actor);
    }

    [Fact]
    public void CreateSupervisorProps_with_Escalate_returns_supervised_wrapper()
    {
        var props = SupervisionActor.CreateSupervisorProps(
            SupervisionStrategy.Escalate,
            DefaultMaxRetries, DefaultBackoffMin, DefaultBackoffMax, DefaultRandomFactor);

        var actor = Sys.ActorOf(props, "escalate-supervisor");
        Assert.NotNull(actor);
    }

    [Fact]
    public void CreateSupervisorProps_with_Resume_returns_supervised_wrapper()
    {
        var props = SupervisionActor.CreateSupervisorProps(
            SupervisionStrategy.Resume,
            DefaultMaxRetries, DefaultBackoffMin, DefaultBackoffMax, DefaultRandomFactor);

        var actor = Sys.ActorOf(props, "resume-supervisor");
        Assert.NotNull(actor);
    }
}
