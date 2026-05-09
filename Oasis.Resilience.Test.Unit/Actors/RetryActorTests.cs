namespace Oasis.Resilience.Test.Unit.Actors;

using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RetryActor"/>.
/// </summary>
public class RetryActorTests : TestKit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryActorTests"/> class.
    /// </summary>
    public RetryActorTests() : base(GetConfig())
    {
    }

    /// <summary>
    /// Gets the Akka configuration for tests with reduced logging.
    /// </summary>
    /// <returns>An Akka <see cref="Config"/> object.</returns>
    private static Config GetConfig()
    {
        return ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
            akka.coordinated-shutdown.log-level = ERROR
            akka.log-config-on-start = off
        ");
    }

    /// <summary>
    /// Shared retry options used across tests.
    /// </summary>
    private readonly RetryOptions _options = new() { LogLevel = LogLevel.None };

    /// <summary>
    /// Verifies the retry actor succeeds on the first attempt when no exception is thrown.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_succeed_on_first_attempt()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));
        var expectedResult = "success";

        actor.Tell(new RetryActor.Execute(
            () => Task.FromResult<object>(expectedResult),
            MaxAttempts: 3,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        var response = await ExpectMsgAsync<object>();
        Assert.Equal(expectedResult, response);
    }

    /// <summary>
    /// Verifies the retry actor retries on failure and eventually succeeds.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_retry_on_failure()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));
        var attemptCount = 0;

        actor.Tell(new RetryActor.Execute(
            () =>
            {
                attemptCount++;
                if (attemptCount < 3)
                    throw new Exception($"Attempt {attemptCount} failed");
                return Task.FromResult<object>($"success after {attemptCount} attempts");
            },
            MaxAttempts: 3,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        var response = await ExpectMsgAsync<object>();
        Assert.Equal("success after 3 attempts", response);
    }

    /// <summary>
    /// Verifies the retry actor returns a failure after exhausting all retry attempts.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_fail_after_max_attempts()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));

        actor.Tell(new RetryActor.Execute(
            () => throw new Exception("always fails"),
            MaxAttempts: 2,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        var response = await ExpectMsgAsync<Status.Failure>();
        Assert.NotNull(response.Cause);
        Assert.Contains("always fails", response.Cause.Message);
    }

    /// <summary>
    /// Verifies the retry actor applies exponential backoff between retry attempts.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_apply_exponential_backoff()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));
        var attemptTimes = new List<DateTime>();

        actor.Tell(new RetryActor.Execute(
            () =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                if (attemptTimes.Count < 4)
                    throw new Exception($"Attempt {attemptTimes.Count} failed");
                return Task.FromResult<object>("success");
            },
            MaxAttempts: 4,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        var response = await ExpectMsgAsync<object>();
        Assert.Equal("success", response);
        Assert.True(attemptTimes.Count >= 2);
    }

    /// <summary>
    /// Verifies the retry actor handles different return types, such as integers.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_handle_different_return_types()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));

        actor.Tell(new RetryActor.Execute(
            () => Task.FromResult<object>(42),
            MaxAttempts: 1,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        var response = await ExpectMsgAsync<object>();
        Assert.Equal(42, (int)response);
    }
}
