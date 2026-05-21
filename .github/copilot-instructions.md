# Copilot Instructions — Oasis.Resilience

## Build & Test

```bash
# Restore, build, and test the solution
dotnet restore
dotnet build --no-restore
dotnet test --no-build --collect:"XPlat Code Coverage"

# Run a single test class
dotnet test --filter "FullyQualifiedName~RetryActorTests"

# Run tests by name substring
dotnet test --filter "DisplayName~should retry"
```

## Architecture

`Oasis.Resilience` is an Akka.NET-backed resilience library that uses `System.Reflection.DispatchProxy` to intercept method calls and apply resilience policies **declared as attributes** on interface methods — keeping all resilience logic out of business code.

### Request Flow

```
Consumer calls interface method
  → ResilientProxy<T>.Invoke() intercepts
    → reads [Retry], [CircuitBreaker], [Supervision], [FanOut] attributes
    → resolves sentinel (-1) values against global options
    → dispatches to the appropriate Akka.NET actor
      → RetryActor      — exponential backoff, exception filtering
      → CircuitBreakerActor — Closed / Open / Half-Open state machine
      → SupervisionActor — BackoffSupervisor wrapping
      → (FanOut) — spawns worker actors, fans out array param, aggregates
  → result returned to consumer
```

### Project Layout

| Project | Target | Purpose |
|---|---|---|
| `Oasis.Resilience` | net8.0 | Library / NuGet package |
| `Oasis.Resilience.Test.Unit` | net9.0 | xUnit unit & integration tests |
| `Demo`, `DemoWithNuGet` | — | Usage examples (not shipped) |
| `ResilienceWithAop`, `ResilienceWithAkka` | — | Prototype/demo apps |

### Key Types

- **`ResilienceRuntime`** (internal singleton) — owns the `ActorSystem` and creates the two shared actor refs (`RetryActor`, `CircuitBreakerActor`). Disposed by DI.
- **`ResilientProxy<T>`** — the `DispatchProxy` subclass; caches attribute lookups and supervisor actors in `ConcurrentDictionary` fields. Static caches are per `T`, instance caches are per method.
- **`ResilienceRegistration`** — DI entry points: `AddResilience(…)` configures options; `AddResilientService<TInterface, TImpl>()` wires the proxy.
- **`OperationRunner`** / **`SupervisedWrapper`** — internal actors used by `SupervisionActor` to run work inside a supervised hierarchy.

### Actor message convention
All actor message types are `sealed record` types defined as nested types inside the actor class. Follow the same pattern when adding new messages.

## Key Conventions

### Attribute sentinel values
All numeric attribute parameters default to `AttributeDefaults.UnsetInt` (`-1`) or `AttributeDefaults.UnsetDouble` (`-1.0`). At runtime `OptionsResolver` substitutes the global option value. **Never pass `0` to mean "use default"** — only `-1` means unset.

```csharp
// Falls back to RetryOptions.DefaultMaxAttempts / DefaultInitialDelayMs
[Retry]

// Explicit override
[Retry(maxAttempts: 3, initialDelay: 2000)]
```

### Attribute stacking order matters
`[CircuitBreaker]` must be placed **above** `[Retry]` — the circuit breaker wraps the retry so an open circuit prevents retries from being attempted at all.

```csharp
[CircuitBreaker(failureThreshold: 3, resetTimeout: 10000)]
[Retry(maxAttempts: 4, initialDelay: 1000)]
public Task<string> GetDataAsync();
```

### FanOut — no factories needed (v2.5+)
The `[FanOut]` attribute auto-detects the single array parameter to split. The **method body itself** is called once per item (with a single-element array). No `RegisterMessageFactory` / `RegisterResultAggregator` calls are required.

When the method has **more than one array parameter**, specify which to split with `splitOn`:

```csharp
[FanOut(splitOn: "items", maxWorkers: 5)]
public Task<List<Result>> ProcessAsync(int[] items, string[] categories) { ... }
```

Auto-merge is supported for `Dictionary<K,V>` (entries merged), `T[]` (concatenated), and `List<T>` (concatenated).

### `InternalsVisibleTo`
`Oasis.Resilience` exposes internals to `Oasis.Resilience.Test.Unit` via an assembly-level attribute in the csproj. Keep test project names in sync if renaming.

### XML documentation
`GenerateDocumentationFile = true` is set on the library. All public and internal types/members must have `<summary>` XML docs.

### Test base class
Proxy tests that need an `ActorSystem` inherit from `ProxyTestBase` (in `Oasis.Resilience.Test.Unit/Proxies/`), which registers created systems for async teardown. Akka log level is hardcoded to `ERROR` to suppress actor noise.

Test stack: **xUnit + FluentAssertions + NSubstitute + Akka.TestKit.Xunit2**.

### NuGet sources
`nuget.config` includes a local feed at `http://nuget.hayatnet.local/v3/index.json` (insecure). This must be reachable when restoring on a developer machine; CI uses the public `nuget.org` feed as fallback.

### CI
GitHub Actions (`.github/workflows/nuget.yml`) runs restore → build → test → Codecov upload on every push/PR to `main`.
