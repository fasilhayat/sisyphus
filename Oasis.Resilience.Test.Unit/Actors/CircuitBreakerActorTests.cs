namespace Oasis.Resilience.Test.Unit.Actors;

using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using Oasis.Resilience.Actors;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerActor"/>.
/// </summary>
public class CircuitBreakerActorTests : TestKit
{
    public CircuitBreakerActorTests() : base(GetConfig())
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

    private readonly RetryOptions _options = new() { LogLevel = Microsoft.Extensions.Logging.LogLevel.None };

    [Fact]
    public async Task CircuitBreaker_should_start_in_closed_state()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(_options)));

        // Act
        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();

        // Assert
        Assert.Equal(CircuitBreakerActor.CircuitState.Closed, response.State);
        Assert.Equal(0, response.FailureCount);
    }

    [Fact]
    public async Task CircuitBreaker_should_open_after_threshold_failures()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(_options)));
        var failureThreshold = 5;

        // Act - Send 5 failures
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

        // Wait for state to be processed
        await Task.Delay(100);

        // Assert - Circuit should be open
        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(CircuitBreakerActor.CircuitState.Open, response.State);
        Assert.Equal(failureThreshold, response.FailureCount);
    }

    [Fact]
    public async Task CircuitBreaker_should_return_open_exception_when_circuit_is_open()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(_options)));
        var failureThreshold = 5;

        // Open the circuit
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

        // Wait for state to be processed
        await Task.Delay(100);

        // Act - Try to execute when open
        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("should not execute"),
            failureThreshold,
            TimeSpan.FromSeconds(30),
            1));

        // Assert
        var response = await ExpectMsgAsync<Status.Failure>();
        Assert.IsType<CircuitBreakerActor.CircuitBreakerOpenException>(response.Cause);
    }

    [Fact]
    public async Task CircuitBreaker_should_transition_to_halfopen_after_reset_timeout()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(_options)));
        var resetTimeout = TimeSpan.FromMilliseconds(100);

        // Open the circuit
        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("test failure"),
            1,
            resetTimeout,
            1));
        await ExpectMsgAsync<Status.Failure>();

        // Wait for state to be processed
        await Task.Delay(50);

        // Act - Wait for reset timeout
        await Task.Delay(resetTimeout + TimeSpan.FromMilliseconds(50));

        // Query state - should be HalfOpen
        // Use polling to handle any timing issues
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

        // Assert
        Assert.Equal(CircuitBreakerActor.CircuitState.HalfOpen, response.State);
    }

    [Fact]
    public async Task CircuitBreaker_should_close_after_success_in_halfopen()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(_options)));
        var resetTimeout = TimeSpan.FromMilliseconds(100);

        // Open the circuit
        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("test failure"),
            1,
            resetTimeout,
            1));
        await ExpectMsgAsync<Status.Failure>();

        // Wait for reset timeout
        await Task.Delay(resetTimeout + TimeSpan.FromMilliseconds(50));

        // Act - Send successful execution in half-open state
        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => Task.FromResult<object>("success"),
            1,
            resetTimeout,
            1));
        var successResponse = await ExpectMsgAsync<object>();

        // Wait for state to be processed
        await Task.Delay(100);

        // Assert - Circuit should be closed
        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var stateResponse = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(CircuitBreakerActor.CircuitState.Closed, stateResponse.State);
        Assert.Equal("success", successResponse);
    }

    [Fact]
    public async Task CircuitBreaker_should_execute_successful_operations()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(_options)));

        // Act
        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => Task.FromResult<object>("success-result"),
            5,
            TimeSpan.FromSeconds(30),
            1));

        // Assert
        var response = await ExpectMsgAsync<object>();
        Assert.Equal("success-result", response);
    }

    [Fact]
    public async Task CircuitBreaker_should_reset_failure_count_on_success()
    {
        // Arrange
        var actor = Sys.ActorOf(Props.Create(() => new CircuitBreakerActor(_options)));

        // Send one failure
        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => throw new Exception("test failure"),
            5,
            TimeSpan.FromSeconds(30),
            1));
        await ExpectMsgAsync<Status.Failure>();

        // Wait for state to be processed
        await Task.Delay(100);

        // Verify failure count
        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        var response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(1, response.FailureCount);

        // Act - Send success
        actor.Tell(new CircuitBreakerActor.ExecuteWithBreaker(
            "test-op",
            () => Task.FromResult<object>("success"),
            5,
            TimeSpan.FromSeconds(30),
            1));
        await ExpectMsgAsync<object>();

        // Wait for state to be processed
        await Task.Delay(100);

        // Assert - Failure count should be reset
        actor.Tell(new CircuitBreakerActor.GetState("test-op"));
        response = await ExpectMsgAsync<CircuitBreakerActor.StateResponse>();
        Assert.Equal(0, response.FailureCount);
        Assert.Equal(CircuitBreakerActor.CircuitState.Closed, response.State);
    }
}
