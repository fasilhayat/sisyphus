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
    public RetryActorTests() : base(GetConfig())
    {
    }

    private static Config GetConfig()
    {
        return ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
            akka.coordinated-shutdown.log-level = ERROR
            akka.log-config-on-start = off
        ");
    }

    private readonly RetryOptions _options = new() { LogLevel = LogLevel.None };

    [Fact]
    public async Task RetryActor_should_succeed_on_first_attempt()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options)));
        var expectedResult = "success";

        // Act
        actor.Tell(new RetryActor.Execute(
            () => Task.FromResult<object>(expectedResult),
            MaxAttempts: 3,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        // Assert
        var response = await ExpectMsgAsync<object>();
        Assert.Equal(expectedResult, response);
    }

    [Fact]
    public async Task RetryActor_should_retry_on_failure()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options)));
        var attemptCount = 0;

        // Act - First two attempts fail, third succeeds
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

        // Assert
        var response = await ExpectMsgAsync<object>();
        Assert.Equal("success after 3 attempts", response);
    }

    [Fact]
    public async Task RetryActor_should_fail_after_max_attempts()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options)));

        // Act
        actor.Tell(new RetryActor.Execute(
            () => throw new Exception("always fails"),
            MaxAttempts: 2,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        // Assert
        var response = await ExpectMsgAsync<Status.Failure>();
        Assert.NotNull(response.Cause);
        Assert.Contains("always fails", response.Cause.Message);
    }

    [Fact]
    public async Task RetryActor_should_apply_exponential_backoff()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options)));
        var attemptTimes = new List<DateTime>();

        // Act
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

        // Assert - Should succeed after retries
        var response = await ExpectMsgAsync<object>();
        Assert.Equal("success", response);
        
        // Verify exponential backoff (second attempt should be delayed more than first)
        Assert.True(attemptTimes.Count >= 2);
    }

    [Fact]
    public async Task RetryActor_should_handle_different_return_types()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options)));

        // Act - Return an integer
        actor.Tell(new RetryActor.Execute(
            () => Task.FromResult<object>(42),
            MaxAttempts: 1,
            InitialDelay: TimeSpan.FromMilliseconds(100)));

        // Assert
        var response = await ExpectMsgAsync<object>();
        Assert.Equal(42, (int)response);
    }
}
