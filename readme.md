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

