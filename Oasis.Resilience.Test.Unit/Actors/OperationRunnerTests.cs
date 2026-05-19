namespace Oasis.Resilience.Test.Unit.Actors;

using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Oasis.Resilience.Actors;
using Xunit;

/// <summary>
/// Unit tests for <see cref="OperationRunner"/> covering success and failure paths.
/// </summary>
public class OperationRunnerTests : TestKit
{
    public OperationRunnerTests() : base(GetConfig()) { }

    private static Config GetConfig() => ConfigurationFactory.ParseString("""
        akka.loglevel = ERROR
        akka.stdout-loglevel = ERROR
        akka.coordinated-shutdown.log-level = ERROR
        akka.log-config-on-start = off
        """);

    [Fact]
    public async Task HandleExecute_should_return_result_on_success()
    {
        var runner = Sys.ActorOf(Props.Create(() => new OperationRunner()), "runner-success");

        runner.Tell(new RunOperation(() => Task.FromResult<object>("hello")));

        var result = await ExpectMsgAsync<object>();
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task HandleExecute_should_return_failure_on_exception()
    {
        var runner = Sys.ActorOf(Props.Create(() => new OperationRunner()), "runner-failure");

        runner.Tell(new RunOperation(() => throw new InvalidOperationException("boom")));

        var failure = await ExpectMsgAsync<Status.Failure>();
        Assert.IsType<InvalidOperationException>(failure.Cause);
        Assert.Contains("boom", failure.Cause.Message);
    }
}
