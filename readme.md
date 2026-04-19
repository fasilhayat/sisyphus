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
[Resilient(maxAttempts: 2, initialDelaySeconds: 2)]
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

## The Resilient Attribute

The `[Resilient]` attribute is the main AOP entry point.

### Purpose

It declares that a method must be executed with:

- Retry policy
- Delay strategy
- Maximum attempt threshold
- Optional logging / telemetry hooks

### Example

```csharp
[Resilient(maxAttempts: 3, initialDelaySeconds: 2)]
public async Task<string> CallExternalApi()
```

### What happens at runtime

When the method is invoked:

1. The call is intercepted by the AOP pipeline.
2. A message is sent to a resilient Akka actor.
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

- Resilience is opt-in via attributes
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

