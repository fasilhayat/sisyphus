# Oasis.Resilience

An Akka.NET-based resilience framework that uses AOP (DispatchProxy) to add retry, circuit breaker, supervision, and fan-out patterns to your service interfaces via declarative attributes.

## Installation

```shell
dotnet add package Oasis.Resilience
```

## Quick Start

### 1. Define a service interface

```csharp
public interface IMyService
{
    [Retry(maxAttempts: 3, initialDelay: 1000)]
    Task<string> GetDataAsync();
}
```

### 2. Implement the service

```csharp
public class MyService : IMyService
{
    public async Task<string> GetDataAsync() { ... }
}
```

### 3. Register with DI

```csharp
services.AddResilience();
services.AddResilientService<IMyService, MyService>();
```

## Attributes

| Attribute | Description |
|-----------|-------------|
| `[Retry]` | Retries the method with exponential backoff on failure |
| `[CircuitBreaker]` | Prevents calls when a failure threshold is exceeded |
| `[Supervision]` | Wraps execution in an Akka.NET supervised actor |
| `[FanOut]` | Distributes work across multiple worker actors in parallel |

## Features

- **Retry** — configurable attempts, exponential backoff, cancellation token support
- **Circuit Breaker** — closed/open/half-open states, configurable threshold and reset timeout, concurrent test calls
- **Supervision** — Restart, Stop, Escalate, Resume, RestartWithBackoff strategies
- **Fan-Out** — parallel processing with worker actors, custom message factory and result aggregator
- **DI Integration** — `IServiceCollection` extensions for registration
- **Actor System** — built on Akka.NET for isolation, scheduling, and lifecycle management
