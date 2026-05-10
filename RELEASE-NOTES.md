# Oasis.Resilience — Release Notes

---

## Quality Assurance Report · May 2026

*127 tests · net8.0 library / net9.0 tests*

---

### 🐛 Bug Fixes

| # | Severity | Description |
|---|---|---|
| 1 | 🔴 Critical | **Fan-out silent data truncation** — `maxWorkers` previously acted as a hard cap, silently dropping input items beyond the limit. Now all items are processed; `maxWorkers` is a parallelism concurrency cap enforced via `SemaphoreSlim`. |
| 2 | 🔴 Critical | **Actor leak from `GetOrAdd` race** — under concurrent first-call traffic the supervisor factory could run multiple times, orphaning extra Akka actors inside the system. Fixed by wrapping with `Lazy<IActorRef>(ExecutionAndPublication)`. |
| 3 | 🔴 Critical | **Circuit breaker hardcoded defaults** — `HandleSuccess` / `HandleFailure` used `AddOrUpdate` with a factory that silently applied hardcoded thresholds and timeouts when the entry was missing. Replaced with `TryGetValue` + `TryUpdate`; stale messages for missing keys are now ignored. |
| 4 | 🟠 High | **`MakeGenericMethod` called on every proxied invocation** — the constructed generic `MethodInfo` is now cached in `InvokeGenericMethodCache` keyed by the implemented method; costs once per interface method, not per call. |
| 5 | 🟠 High | **`ResolveImplementedMethod` reflection lookup on every call** — mapping from interface `MethodInfo` to implementation `MethodInfo` is now cached in `ImplementedMethodCache`. |
| 6 | 🟡 Medium | **Missing `volatile` on static factory fields** — `_globalMessageFactory` and `_globalResultAggregator` lacked a cross-thread memory fence. Both are now `volatile`. |
| 7 | 🟡 Medium | **Cached supervisor actors never stopped** — `ResilientProxy<T>` now implements both `IDisposable` and `IAsyncDisposable`. All materialised supervisor actors in the per-instance caches are gracefully stopped on disposal, compatible with both sync and async DI scopes. |
| 8 | 🟡 Medium | **`AddResilience()` / `AddResilientService()` not idempotent** — calling either twice registered a second `ResilienceRuntime`, leaking a second Akka actor system. Both now use `TryAddSingleton`. |

---

### ✅ Previously Fixed (same release cycle)

- **Circuit breaker HalfOpen → Open** immediate re-open on failed trial call.
- **`TargetInvocationException` unwrapping** — synchronously-throwing methods now surface their original exception type to callers.
- **CB + Retry composition** — was silently broken (Akka `Ask` auto-faults; the `is Status.Failure` check never fired). Fixed with a `try/catch` that re-throws `CircuitBreakerOpenException` directly and falls through to retry for all other exceptions.
- **`Sender` captured before every `await`** in `OperationRunner` and `CircuitBreakerActor.ExecuteOperation`.
- **Retry backoff hardened** — exponential backoff capped by `MaxDelayMs`, jitter via `JitterFactor`, exponent overflow guard (capped at 30).
- **`FanOutParameters.cs` dead code removed.**
- **`AskTimeout` configurable** via `RetryOptions.AskTimeout`; previously hardcoded to 30 s.

---

### ✨ New Features

| Feature | Detail |
|---|---|
| `RetryOptions.MaxDelayMs` | Upper bound for exponential backoff delay (default 30 000 ms). |
| `RetryOptions.JitterFactor` | Symmetric multiplicative jitter applied to each backoff (default 0.2 = ±20%). |
| `RetryOptions.AskTimeout` | Timeout for internal Akka `Ask` calls (default 30 s). Useful to lower in test environments. |
| `IDisposable` / `IAsyncDisposable` on proxy | Supervised actors created per method / per worker type are stopped on DI scope disposal. |
| Idempotent registration | `AddResilience()` and `AddResilientService()` are safe to call multiple times. |
| Fan-out processes all items | All split values are dispatched regardless of `maxWorkers`; concurrency is throttled, not truncated. |

---

### 🧪 Test Coverage & Stability

| Metric | Before | After |
|---|---|---|
| Total tests | 122 | **127** |
| Pass rate | 100% | **100%** |
| Line coverage | 77.2% | **89.8%** |
| Branch coverage | 63.6% | **76.7%** |
| **Score** | 7.0 / 10 | **8.6 / 10** |

New test files added:
- `Actors/CircuitBreakerHalfOpenTests.cs` — HalfOpen → Open immediate re-open.
- `Proxies/ResilientProxyCircuitBreakerIntegrationTests.cs` — CB-only open/fail-fast; CB + Retry composition.
- `Proxies/ResilientProxyFanOutIntegrationTests.cs` — fan-out E2E; fan-out with more items than `maxWorkers`.

---

### 📊 Updated Quality Scorecard

| Dimension | Previous | Now | Δ |
|---|---|---|---|
| Test coverage & stability | 8.5 / 10 | **8.6 / 10** | +0.1 |
| Cyclomatic complexity | 10 / 10 | **10 / 10** | — |
| Package design & correctness | 8.7 / 10 | **9.4 / 10** | +0.7 |
| AOP utilisation | 8.4 / 10 | **8.9 / 10** | +0.5 |
| Execution overhead | 8.0 / 10 | **8.8 / 10** | +0.8 |
| Developer experience | 9.0 / 10 | **9.2 / 10** | +0.2 |
| **Overall** | 8.8 / 10 | 🟢 **9.15 / 10** | **+0.35** |

---

### ⚠️ Known Deferred Items

| Item | Reason deferred |
|---|---|
| Per-service actor isolation | One shared `RetryActor` + `CircuitBreakerActor` means concurrent calls across all registered services queue behind each other. Splitting per `typeof(T)` is a meaningful architectural change warranting its own design discussion. |
| Remaining branch coverage gap (~23%) | Actor crash-recovery and hosted-service teardown paths require forced faults against a live Akka system — better suited to a dedicated integration test layer. |
| Fan-out implicit supervision | When no `[Supervision]` attribute is present, global defaults (`RestartWithBackoff`) still apply to fan-out workers. This is an intentional conservative default and is documented in the README. |

---

### 📦 Build

```
dotnet build  →  0 Warning(s)  0 Error(s)
dotnet test   →  127 passed  0 failed  0 skipped
```

*Target frameworks: `net8.0` (library, demos) · `net9.0` (tests)*
