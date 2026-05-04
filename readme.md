# Aspect-Oriented Resilience with Akka.NET

## Overview

This project demonstrates an Aspect-Oriented Programming (AOP) approach to building resilient, self-healing distributed systems using Akka.NET. The central idea is to separate **resilience concerns** (retries, delays, failure handling, recovery logic) from **business logic**, allowing developers to focus on domain behavior while the infrastructure automatically handles instability.

In traditional systems, resilience logic is scattered across services, controllers, and clients. This leads to duplication, inconsistent retry policies, and fragile error handling. Here, we invert that model: resilience is applied declaratively using attributes and enforced transparently at runtime.

---

## Core Concept: AOP in This System

Aspect-Oriented Programming is used here to inject cross-cutting concerns—specifically resilience—around method execution without polluting business logic.

Instead of writing:

```csharp
try
{
    // call service
}
catch (Exception)
{
    // retry logic
}
```

You declare intent:

```csharp
[Retry(maxAttempts: 2, initialDelay: 2000)]
public async Task<string> GetDataAsync()
```

The AOP layer (backed by Akka.NET actors and supervision strategies) handles:

- Execution retries
- Backoff timing
- Failure tracking
- Recovery orchestration
- Optional fallback routing

---

## Role of Akka.NET

Akka.NET provides the runtime backbone for resilience:

### 1. Actor Isolation
Each resilient operation can be executed inside an actor boundary. Failures are isolated and do not propagate.

### 2. Supervision Strategy
We use Akka supervision (including backoff strategies) to define recovery behavior:

- Restart actors on failure
- Apply exponential backoff
- Introduce jitter (randomization)
- Limit retry attempts

### 3. BackoffSupervisor
The system uses `BackoffSupervisor` to automatically restart failing actors with controlled delays:

- Prevents tight failure loops
- Reduces load spikes during outages
- Allows external systems time to recover

---

## The Retry Attribute

The `[Retry]` attribute is the main AOP entry point.

### Purpose

It declares that a method must be executed with:

- Retry policy
- Delay strategy
- Maximum attempt threshold
- Optional logging / telemetry hooks

### Example

```csharp
[Retry(maxAttempts: 3, initialDelay: 2000)]
public async Task<string> CallExternalApi()
```

### What happens at runtime

When the method is invoked:

1. The call is intercepted by the AOP pipeline.
2. A message is sent to a retry Akka actor.
3. The actor executes the method.
4. If it fails:
   - It is retried based on configuration
   - Backoff delay is applied
   - Retry count is incremented
5. If max attempts are reached:
   - Failure is propagated or handled via fallback logic

---

## The Circuit Breaker Pattern

The `[CircuitBreaker]` attribute implements the circuit breaker pattern, which prevents cascading failures by stopping calls to a failing service after a configurable threshold of consecutive failures.

### Purpose

It protects your system from:

- Cascading failures when downstream services are unhealthy
- Resource exhaustion from waiting on unresponsive services
- Thundering herd problems when a service recovers

### States

The circuit breaker operates in three states:

1. **Closed** - Normal operation. Requests flow through. Failures are counted.
2. **Open** - Failure threshold exceeded. Requests fail immediately without executing the operation.
3. **Half-Open** - Reset timeout elapsed. A limited number of test requests are allowed through to probe if the service has recovered.

### Example

```csharp
[CircuitBreaker(failureThreshold: 3, resetTimeout: 10000, maxConcurrentCalls: 2)]
[Retry(maxAttempts: 4, initialDelay: 1000)]
public async Task<string> GetInventoryAsync()
```

### What happens at runtime

When the method is invoked:

1. The call is intercepted by the AOP pipeline.
2. The circuit breaker state is checked for the operation.
3. If **Closed**: the operation executes. Failures increment the counter.
4. If **Open**: the call fails immediately with `CircuitBreakerOpenException`.
5. If **Half-Open**: a limited number of test calls are allowed through.
6. On success in Half-Open state: circuit transitions back to **Closed**.
7. On failure in Half-Open state: circuit transitions back to **Open**.

### Combining Circuit Breaker with Retry

The circuit breaker and retry attributes work together:

```csharp
[CircuitBreaker(failureThreshold: 3, resetTimeout: 10000)]
[Retry(maxAttempts: 4, initialDelay: 1000)]
public async Task<string> GetDataAsync()
```

- **Retry** handles transient failures (temporary network blips, brief timeouts)
- **Circuit Breaker** prevents repeated attempts when a service is clearly down
- The circuit breaker wraps the retry, so if the circuit is open, retries are never attempted

---

## The Supervision Attribute

The `[Supervision]` attribute integrates Akka.NET's supervision strategies with the AOP layer, allowing declarative configuration of actor failure handling.

### Purpose

It enables:
- Actor restart with configurable backoff
- Different supervision strategies (Restart, Stop, Escalate, Resume, RestartWithBackoff)
- Integration with Akka.NET's supervision hierarchy
- Declarative failure handling without manual actor management

### Supervision Strategies

| Strategy | Description |
|----------|-------------|
| `Restart` | Restart the actor when it fails |
| `Stop` | Stop the actor when it fails |
| `Escalate` | Escalate the failure to the parent actor |
| `Resume` | Resume the actor without restarting when it fails |
| `RestartWithBackoff` | Restart the actor with exponential backoff when it fails |

### Example

```csharp
[Supervision(strategy: SupervisionStrategy.RestartWithBackoff, maxRetries: 5, backoffMinMs: 2000, backoffMaxMs: 30000)]
[Retry(maxAttempts: 3, initialDelay: 2000)]
public async Task<string> GetDataWithSupervisionAsync()
```

### What happens at runtime

When a method with `[Supervision]` is invoked:

1. The AOP layer creates or reuses a supervised actor
2. If `RestartWithBackoff` is specified, a `BackoffSupervisor` manages the actor
3. On failure, the configured strategy is applied
4. The actor is restarted/stopped/escalated based on the strategy
5. Backoff delays prevent tight failure loops

---

## The Fan-Out Attribute

The `[FanOut]` attribute enables parallel processing by distributing work across multiple actor workers, with built-in supervision.

### Purpose

It allows:
- Automatic fan-out of work to multiple actors
- Parallel processing of collections/arrays
- Aggregation of results from multiple workers
- Transparent actor management with supervision

### Example

```csharp
[FanOut(
    workerActorType: typeof(HolidayWorkerActor),
    splitParameterName: "years",
    maxWorkers: 5
)]
[Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
public async Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country)
```

### How it works

1. The `splitParameterName` identifies which parameter contains the data to split
2. The method creates a supervisor for the specified `workerActorType`
3. Work is distributed across up to `maxWorkers` actor instances
4. Results are aggregated back into the expected return type
5. Supervision is applied to each worker actor

### Configuration

| Parameter | Type | Description |
|-----------|------|-------------|
| `workerActorType` | `Type` | The type of worker actor to spawn |
| `splitParameterName` | `string` | The name of the parameter to split for distribution |
| `maxWorkers` | `int` | Maximum number of worker actors (default: 5) |

### Message Factory and Result Aggregator

Since the AOP layer needs to create messages and aggregate results generically, you must register a message factory and result aggregator:

```csharp
// Register message factory for your worker actor
ResilientProxy<IHolidayService>.RegisterMessageFactory((workerType, splitValue, parameters, otherArgs) =>
{
    if (workerType == typeof(HolidayWorkerActor))
    {
        var year = (int)splitValue;
        var country = (string)otherArgs[0];
        return new HolidayWorkerActor.ProcessYear(year, country);
    }
    throw new InvalidOperationException($"Unknown worker type: {workerType.Name}");
});

// Register result aggregator
ResilientProxy<IHolidayService>.RegisterResultAggregator((results, workerType, returnType) =>
{
    if (returnType == typeof(Dictionary<int, string>) && workerType == typeof(HolidayWorkerActor))
    {
        var dict = new Dictionary<int, string>();
        foreach (var result in results)
        {
            if (result is HolidayWorkerActor.YearProcessed processed)
            {
                dict[processed.Year] = processed.Content;
            }
        }
        return dict;
    }
    throw new InvalidOperationException($"Unsupported return type {returnType.Name} for worker {workerType.Name}");
});
```

---

## Parameter Reference

### Retry Attribute Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxAttempts` | `int` | 5 | The maximum number of times the operation will be attempted. This includes the initial attempt, so a value of 3 means one original call plus two retries. Must be at least 1. |
| `initialDelay` | `int` (milliseconds) | 2000 | The base delay before the first retry. The delay between subsequent retries grows exponentially using the formula: `initialDelay * 2^(attempt - 1)`. For example, with `initialDelay: 1000`, delays would be: 1s, 2s, 4s, 8s... Must be zero or greater. |

**Example delays with `initialDelay: 1000`:**
- Attempt 1: immediate
- Attempt 2: 1 second delay
- Attempt 3: 2 seconds delay
- Attempt 4: 4 seconds delay
- Attempt 5: 8 seconds delay

### Circuit Breaker Attribute Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `failureThreshold` | `int` | 5 | The number of consecutive failures required before the circuit transitions to **Open** state. Once open, all subsequent calls fail immediately without executing the underlying operation. Must be at least 1. A lower value makes the circuit more sensitive to failures. |
| `resetTimeout` | `int` (milliseconds) | 30000 | The duration the circuit remains in **Open** state before transitioning to **Half-Open**. During this time, no calls are executed. After this period, test calls are allowed through to check if the service has recovered. Must be zero or greater. |
| `maxConcurrentCalls` | `int` | 1 | The maximum number of concurrent calls allowed when the circuit is in **Half-Open** state. These test calls determine whether the service has recovered. A value of 1 means only one test call is allowed; if it succeeds, the circuit closes. If it fails, the circuit reopens. Must be at least 1. |

**State transition example with `failureThreshold: 3, resetTimeout: 10000`:**
1. Three consecutive failures occur → circuit opens
2. For the next 10 seconds, all calls fail immediately
3. After 10 seconds, circuit transitions to half-open
4. One test call (maxConcurrentCalls: 1) is allowed through
5. If it succeeds → circuit closes, normal operation resumes
6. If it fails → circuit reopens, another 10 second wait begins

### RetryOptions Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LogLevel` | `LogLevel` | `Debug` | Controls the verbosity of diagnostic logging. Set to `LogLevel.Debug` to see retry attempt messages, or a higher level (e.g., `LogLevel.Information`, `LogLevel.Warning`) to suppress them. |

### CircuitBreakerOptions Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultFailureThreshold` | `int` | 5 | The global default failure threshold applied when a `[CircuitBreaker]` attribute does not specify `failureThreshold`. Can be overridden per-method via the attribute. |
| `DefaultResetTimeout` | `int` (milliseconds) | 30000 | The global default reset timeout applied when a `[CircuitBreaker]` attribute does not specify `resetTimeout`. Can be overridden per-method via the attribute. |
| `DefaultMaxConcurrentCalls` | `int` | 1 | The global default max concurrent calls applied when a `[CircuitBreaker]` attribute does not specify `maxConcurrentCalls`. Can be overridden per-method via the attribute. |

### Supervision Attribute Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `strategy` | `SupervisionStrategy` | `RestartWithBackoff` | The supervision strategy to apply when the actor fails. Options: `Restart`, `Stop`, `Escalate`, `Resume`, `RestartWithBackoff`. |
| `maxRetries` | `int` | 5 | The maximum number of retries before giving up. Must be at least 1. |
| `backoffMinMs` | `int` (milliseconds) | 2000 | The minimum backoff duration. Must be zero or greater. |
| `backoffMaxMs` | `int` (milliseconds) | 30000 | The maximum backoff duration. Must be zero or greater. |
| `randomFactor` | `double` | 0.2 | The random factor to add jitter to backoff. Must be zero or greater. |

### FanOut Attribute Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `workerActorType` | `Type` | (required) | The type of worker actor to spawn for processing. Must be a subclass of `ReceiveActor`. |
| `splitParameterName` | `string` | (required) | The name of the parameter to split for fan-out distribution. |
| `maxWorkers` | `int` | 5 | The maximum number of worker actors to spawn. Must be at least 1. |

---

## What We Are Abstracting Away

This system removes several recurring concerns from application code:

### 1. Retry Logic
No manual loops, no `try/catch` retry blocks.

### 2. Backoff Calculations
No exponential delay calculations in business code.

### 3. Failure Recovery Strategy
No duplicated decision logic for “what happens when it fails”.

### 4. Concurrency Handling
No manual thread or task coordination for retry timing.

### 5. Infrastructure Awareness
Business logic is unaware of:
- Actor system
- Supervision trees
- Retry orchestration
- External service instability

---

## Self-Healing Behavior

The system is designed to recover automatically from transient failures:

### Failure Scenarios Handled

- Temporary network outages
- Downstream service restarts
- Message broker unavailability
- API rate limiting

### Recovery Mechanism

- Actor restarts isolate failure state
- Backoff delays reduce system pressure
- Retry limits prevent infinite loops
- Randomized jitter prevents thundering herd effects

This results in a system that stabilizes itself without human intervention.

---

## Design Intent

The goal is not just resilience, but **resilience by default**.

Developers should not “remember” to make things robust. Instead:

- Resilience (retry) is opt-in via attributes
- Defaults enforce safe behavior
- Infrastructure enforces consistency

---

## Benefits

### For Developers
- Cleaner service code
- No duplicated retry logic
- Predictable failure behavior

### For Architecture
- Centralized resilience policy
- Observable failure patterns
- Controlled recovery behavior

### For Operations
- Reduced incident noise
- Fewer cascading failures
- Improved system stability under load

---

## Summary

This AOP + Akka.NET approach transforms resilience from an application concern into an infrastructure capability.

You are not building retry logic into systems.

You are building systems that automatically retry themselves correctly, consistently, and safely.

---

Below is a practical feature map for an **AOP abstraction on top of Akka.NET** for client-side REST calls. The main idea is: Polly handles the retry policy itself, while Akka.NET adds runtime behavior around it—supervision, scheduling, circuit breaking, DI, and message-driven plumbing that makes the abstraction easier for other developers to consume [1][2][3][4].

## Feature list

### 1. Declarative method-level policies
Use attributes on client methods to describe retry behavior, timeout, breaker settings, and fallback behavior. A programmer should be able to write one decoration and get the full pipeline without manual wiring [5][6].

### 2. Retry with scheduler-backed delays
Use Akka.NET’s retry support or scheduler to delay attempts instead of hand-rolling `Task.Delay`. Akka.NET’s `RetrySupport` can retry async work with fixed delay or backoff using a scheduler, which fits actor-driven execution nicely [1][7].

### 3. Circuit breaker per endpoint
Add a breaker for each REST operation or upstream service so repeated failures stop traffic temporarily. Akka.NET’s circuit breaker supports open, half-open, and closed behavior plus callbacks like `OnOpen`, `OnClose`, and `OnHalfOpen` [2].

### 4. Supervision-style failure classification
Classify failures into retryable, recoverable, fatal, or escalate. Akka.NET’s supervision model is built for this style of parent-managed failure handling, and it gives a more lifecycle-aware model than a plain retry loop [4].

### 5. Ask-pattern invocation
Wrap client calls as request/response messages using the ask pattern so the implementation stays asynchronous and timeout-aware. Akka.NET’s ask pattern is designed for send-and-receive futures and works naturally with pipe-to and task composition [8][9].

### 6. Backoff and restart semantics
Support delayed recovery, exponential backoff, and actor restart after repeated failures. Akka.NET has backoff supervision patterns specifically for restarting work after increasing intervals [4][9].

### 7. Dependency injection support
Let the actor/proxy resolve HTTP clients, serializers, auth providers, and logging from DI. Akka.NET supports passing an `IServiceProvider` into the actor system and resolving scoped/transient dependencies in actors [3][10].

### 8. Context propagation
Carry correlation id, tenant id, user context, trace id, and cancellation token through the pipeline. This is not Akka-specific by itself, but Akka’s message envelope style makes it easy to standardize [8][11].

### 9. Fallback paths
Allow an attribute to specify a fallback method or degraded response behavior when retries fail. This is useful for “best effort” queries where stale data is acceptable [2][6].

### 10. Bulkhead / concurrency control
Limit parallel requests per endpoint so one bad dependency does not consume all client resources. This is a common resilience addition to a richer AOP layer, even if the final retry engine remains Polly-based [6][2].

### 11. Observability hooks
Expose structured logs, metrics, tracing, and attempt counters per method. Akka-style workflows make it easy to emit events for each attempt, breaker transition, and final failure [2][4].

### 12. Stream-safe retry
For `IAsyncEnumerable<T>`, retry page fetches or segments, not the whole stream, unless the stream is restart-safe. This avoids duplicate records and gives a clean streaming model [12][1].

## Recommended layering

A clean architecture is:

- **Attribute layer**: declares intent.
- **Interceptor/proxy layer**: reads attributes and builds execution policy.
- **Akka.NET runtime layer**: handles message flow, scheduling, supervision, breaker state, and DI.
- **Polly policy layer**: executes retries, backoff, and policy combinations [6][1][3][4].

That gives you richer plumbing than Polly alone while keeping the retry semantics explicit and testable.

## Pseudocode model

```text
Client method
  -> attribute metadata
  -> AOP interceptor reads metadata
  -> build execution envelope
  -> send request to Akka actor
  -> actor applies circuit breaker
  -> actor executes REST call
  -> on transient failure, retry with delay/backoff
  -> on repeated failure, emit event / fallback / escalate
  -> return result to caller
```

That flow mirrors the things Akka is especially good at: message boundaries, supervised execution, delayed recovery, and explicit failure handling [8][4][2].

## Example attributes

```csharp
[Retry(maxAttempts = 4, initialDelay = 2000)]
[CircuitBreaker(failureThreshold = 5, reset = 5000)]
[Timeout(milliseconds = 3000)]
public interface IMemberClient
{
    Task<IReadOnlyList<Member>> GetMembersAsync();
}
```

You can extend this with one combined attribute if you want fewer decorations:

```csharp
[Resilience(
    maxAttempts = 4,
    initialDelays = 2000,
    timeout = 3000,
    failureThreshold = 5,
    reset = 5000)]
```

That makes usage easier for other programmers, because they only need to learn one shape and one set of defaults.

## C# implementation sketch

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class RetryAttribute : Attribute
{
    public int MaxAttempts { get; }
    public int InitialDelayM { get; }
    public RetryAttribute(int maxAttempts = 3, int initialDelay = 2)
    {
        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class CircuitBreakerAttribute : Attribute
{
    public int FailureThreshold { get; }
    public int Reset { get; }
    public CircuitBreakerAttribute(int failureThreshold = 5, int reset = 4)
    {
        FailureThreshold = failureThreshold;
        Reset = resets;
    }
}

public interface IMemberClient
{
    [Retry(4, 250)]
    [CircuitBreaker(5, 4)]
    Task<IReadOnlyList<Member>> GetMembersAsync();
}
```

Interceptor concept:

```csharp
public sealed class AopInterceptor<T> : DispatchProxy
{
    private T _inner;

    public void SetInner(T inner) => _inner = inner;

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        var retry = targetMethod.GetCustomAttribute<RetryAttribute>();
        var breaker = targetMethod.GetCustomAttribute<CircuitBreakerAttribute>();

        return ExecuteAsync(targetMethod, args, retry, breaker);
    }

    private async Task<object> ExecuteAsync(MethodInfo method, object[] args, RetryAttribute retry, CircuitBreakerAttribute breaker)
    {
        // 1. Build Akka message envelope
        // 2. Ask actor for execution
        // 3. Apply breaker state
        // 4. Retry using scheduler/backoff
        // 5. Return result
        return await Task.FromResult(method.Invoke(_inner, args));
    }
}
```

This is only the shape; the real implementation would route the call through an actor and use Akka.NET’s retry and circuit breaker facilities rather than directly invoking the method [1][2][8].

## What to give developers

To reduce plumbing for other programmers, provide:

- Default attributes with sane values.
- One combined resilience attribute for common cases.
- Centralized actor/proxy registration in DI.
- Standard log and metric fields.
- A consistent exception mapping policy.
- A helper for `IAsyncEnumerable<T>` page-level retries.

That way, most users only decorate methods and register the client once, while the framework handles the rest [3][4][2].

