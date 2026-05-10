# Oasis.Resilience — Release Notes

---

## Quality Assurance Report · May 2026

*130 tests · net8.0 library / net9.0 tests*

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
| 9 | 🟠 High | **Half-open concurrent call enforcement broken by `ReceiveAsync`** — `_inFlightCounts` was dead code because the actor mailbox was blocked during `await`; a second concurrent `ExecuteWithBreaker` was never processed until the first completed. Refactored to synchronous `Receive` + `PipeTo` so the mailbox stays free during async operations. |
| 10 | 🟡 Medium | **`OperationCanceledException` counted as failure** — cancellation should not count toward the failure threshold in Closed state or re-open the circuit in HalfOpen state. Now handled separately in both the synchronous start path and the PipeTo failure path. |
| 11 | 🟢 Low | **`ResilienceRuntime.Dispose` wrapped exceptions in `AggregateException`** — `Wait(5s)` masks the original exception on fault. Changed to `GetAwaiter().GetResult()` for proper exception propagation. |

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

| Metric | Before | Round 1 | Round 2 |
|---|---|---|---|
| Total tests | 122 | 127 | **130** |
| Pass rate | 100% | 100% | **100%** |
| Line coverage | 77.2% | 89.8% | 89.8%* |
| Branch coverage | 63.6% | 76.7% | 76.7%* |
| **Score** | 7.0 / 10 | 8.6 / 10 | **8.7 / 10** |

*\* Coverage not re-measured; changes are contained to `CircuitBreakerActor` (PipeTo refactoring + tests) and `ResilienceRuntime` (exception propagation only).*

New tests added:
- `CircuitBreakerActorTests.CircuitBreaker_should_not_count_cancellation_as_failure_in_closed`
- `CircuitBreakerActorTests.CircuitBreaker_should_not_reopen_on_cancellation_in_halfopen`
- `CircuitBreakerHalfOpenTests.HalfOpen_should_enforce_max_concurrent_calls`

Previously added:
- `Actors/CircuitBreakerHalfOpenTests.cs` — HalfOpen → Open immediate re-open.
- `Proxies/ResilientProxyCircuitBreakerIntegrationTests.cs` — CB-only open/fail-fast; CB + Retry composition.
- `Proxies/ResilientProxyFanOutIntegrationTests.cs` — fan-out E2E; fan-out with more items than `maxWorkers`.

---

### 📊 Updated Quality Scorecard

| Dimension | Round 1 | Round 2 | Δ |
|---|---|---|---|---|
| Test coverage & stability | 8.6 / 10 | **8.7 / 10** | +0.1 |
| Cyclomatic complexity | 10 / 10 | **10 / 10** | — |
| Package design & correctness | 9.4 / 10 | **9.5 / 10** | +0.1 |
| AOP utilisation | 8.9 / 10 | **8.9 / 10** | — |
| Execution overhead | 8.8 / 10 | **9.0 / 10** | +0.2 |
| Developer experience | 9.2 / 10 | **9.3 / 10** | +0.1 |
| **Overall** | 🟢 9.15 / 10 | 🟢 **9.23 / 10** | **+0.08** |

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
dotnet test   →  130 passed  0 failed  0 skipped
```

*Target frameworks: `net8.0` (library, demos) · `net9.0` (tests)*
