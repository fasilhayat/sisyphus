namespace Oasis.Resilience.Test.Unit.Actors;

using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="RetryActor"/> exception filter (the <c>RetryOn</c> argument
/// of <see cref="RetryActor.Execute"/>).
/// </summary>
public class RetryActorExceptionFilterTests : TestKit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryActorExceptionFilterTests"/> class.
    /// </summary>
    public RetryActorExceptionFilterTests()
        : base(ConfigurationFactory.ParseString("akka.loglevel = ERROR\nakka.stdout-loglevel = ERROR"))
    {
    }

    private readonly RetryOptions _options = new() { LogLevel = LogLevel.None };

    /// <summary>
    /// Verifies the retry actor does not retry an exception type outside the supplied filter.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_not_retry_exception_outside_filter()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));
        var attempts = 0;

        actor.Tell(new RetryActor.Execute(
            () =>
            {
                attempts++;
                throw new InvalidOperationException("not retryable");
            },
            MaxAttempts: 3,
            InitialDelay: TimeSpan.FromMilliseconds(10),
            RetryOn: [typeof(HttpRequestException)]));

        var response = await ExpectMsgAsync<Status.Failure>();
        Assert.IsType<InvalidOperationException>(response.Cause);
        Assert.Equal(1, attempts);
    }

    /// <summary>
    /// Verifies the retry actor retries an exception type that matches the supplied filter.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_retry_exception_inside_filter()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));
        var attempts = 0;

        actor.Tell(new RetryActor.Execute(
            () =>
            {
                attempts++;
                if (attempts < 2) throw new HttpRequestException("transient");
                return Task.FromResult<object>("ok");
            },
            MaxAttempts: 3,
            InitialDelay: TimeSpan.FromMilliseconds(10),
            RetryOn: [typeof(HttpRequestException)]));

        var response = await ExpectMsgAsync<object>(TimeSpan.FromSeconds(5));
        Assert.Equal("ok", response);
        Assert.Equal(2, attempts);
    }

    /// <summary>
    /// Verifies the retry actor retries any exception when the filter is null.
    /// </summary>
    [Fact]
    public async Task RetryActor_should_retry_all_when_filter_is_null()
    {
        var actor = Sys.ActorOf(Props.Create(() => new RetryActor(_options, null)));
        var attempts = 0;

        actor.Tell(new RetryActor.Execute(
            () =>
            {
                attempts++;
                if (attempts < 2) throw new InvalidOperationException("anything");
                return Task.FromResult<object>("done");
            },
            MaxAttempts: 3,
            InitialDelay: TimeSpan.FromMilliseconds(10),
            RetryOn: null));

        var response = await ExpectMsgAsync<object>(TimeSpan.FromSeconds(5));
        Assert.Equal("done", response);
        Assert.Equal(2, attempts);
    }
}
