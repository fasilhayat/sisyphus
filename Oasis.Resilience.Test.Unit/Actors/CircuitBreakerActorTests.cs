namespace Oasis.Resilience.Test.Unit.Actors;

using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Microsoft.Extensions.Logging;
using Oasis.Resilience.Actors;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerActor"/>.
/// </summary>
public class CircuitBreakerActorTests : TestKit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerActorTests"/> class.
    /// </summary>
    public CircuitBreakerActorTests() : base(GetConfig())
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
    /// Verifies the circuit breaker starts in the Closed state with zero failure count.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_should_start_in_closed_state()
    {
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)));

        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();

        Assert.Equal(CircuitBreakerActor.CircuitState.Closed, response.State);
        Assert.Equal(0, response.FailureCount);
    }

    /// <summary>
    /// Verifies the circuit breaker transitions to Open after the configured failure threshold is reached.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_should_open_after_threshold_failures()
    {
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)));
        var failureThreshold = 5;

        for (int i = 0; i < failureThreshold; i++)
        {
            actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
                "test-op",
                () => throw new Exception("test failure"),
                failureThreshold,
                TimeSpan.FromSeconds(30),
                1));
            await ExpectMsgAsync<Status.Failure>();
        }

        await Task.Delay(100);

        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(CircuitBreakerActor.CircuitState.Open, response.State);
        Assert.Equal(failureThreshold, response.FailureCount);
    }

    /// <summary>
    /// Verifies a <see cref="CircuitBreakerActor.CircuitBreakerOpenException"/> is thrown when the circuit is Open.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_should_return_open_exception_when_circuit_is_open()
    {
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)));
        var failureThreshold = 5;

        for (int i = 0; i < failureThreshold; i++)
        {
            actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
                "test-op",
                () => throw new Exception("test failure"),
                failureThreshold,
                TimeSpan.FromSeconds(30),
                1));
            await ExpectMsgAsync<Status.Failure>();
        }

        await Task.Delay(100);

        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("should not execute"),
            failureThreshold,
            TimeSpan.FromSeconds(30),
            1));

        var response = await ExpectMsgAsync<Status.Failure>();
        Assert.IsType<CircuitBreakerActor.CircuitBreakerOpenException>(response.Cause);
    }

    /// <summary>
    /// Verifies the circuit breaker transitions to HalfOpen after the reset timeout elapses.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_should_transition_to_halfopen_after_reset_timeout()
    {
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)));
        var resetTimeout = TimeSpan.FromMilliseconds(100);

        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("test failure"),
            1,
            resetTimeout,
            1));
        await ExpectMsgAsync<Status.Failure>();

        await Task.Delay(resetTimeout + TimeSpan.FromMilliseconds(50));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        CircuitBreakerActor.StateResponse response = null!;
        do
        {
            actor.Tell(new CircuitBreakerActor.GetState("test-op"));
            response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
            if (response.State == CircuitBreakerActor.CircuitState.HalfOpen)
                break;
            await Task.Delay(10);
        } while (DateTime.UtcNow < deadline);

        Assert.Equal(CircuitBreakerActor.CircuitState.HalfOpen, response.State);
    }

    /// <summary>
    /// Verifies the circuit breaker returns to Closed after a successful operation in HalfOpen state.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_should_close_after_success_in_halfopen()
    {
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)));
        var resetTimeout = TimeSpan.FromMilliseconds(100);

        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("test failure"),
            1,
            resetTimeout,
            1));
        await ExpectMsgAsync<Status.Failure>();

        await Task.Delay(resetTimeout + TimeSpan.FromMilliseconds(50));

        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => Task.FromResult<object>("success"),
            1,
            resetTimeout,
            1));
        var successResponse = await ExpectMsgAsync<object>();

        await Task.Delay(100);

        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var stateResponse = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(CircuitBreakerActor.CircuitState.Closed, stateResponse.State);
        Assert.Equal("success", successResponse);
    }

    /// <summary>
    /// Verifies the circuit breaker successfully executes operations that do not throw.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_should_execute_successful_operations()
    {
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)));

        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => Task.FromResult<object>("success-result"),
            5,
            TimeSpan.FromSeconds(30),
            1));

        var response = await ExpectMsgAsync<object>();
        Assert.Equal("success-result", response);
    }

    /// <summary>
    /// Verifies the failure count resets to zero after a successful operation.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_should_reset_failure_count_on_success()
    {
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(LogLevel.None, null)));

        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("test failure"),
            5,
            TimeSpan.FromSeconds(30),
            1));
        await ExpectMsgAsync<Status.Failure>();

        await Task.Delay(100);

        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(1, response.FailureCount);

        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => Task.FromResult<object>("success"),
            5,
            TimeSpan.FromSeconds(30),
            1));
        await ExpectMsgAsync<object>();

        await Task.Delay(100);

        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(0, response.FailureCount);
        Assert.Equal(CircuitBreakerActor.CircuitState.Closed, response.State);
    }
}
