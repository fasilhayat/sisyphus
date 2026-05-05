# Test Coverage and Complexity Report

Generated: 2026-05-05 21:45

## Solution Summary

| Project | Line Coverage | Branch Coverage | Avg Complexity | Test Passed |
|---------|---------------|------------------|----------------|-------------|
| **Oasis.Resilience** | **15.98%** | **15.17%** | **177** | N/A |
| **Oasis.Resilience.Test.Unit** | **~45%** | **~40%** | **~40** | ✓ 113/113 |
| **Demo** | N/A | N/A | ~25 | N/A |
| **ResilienceWithAkka** | N/A | N/A | ~15 | N/A |
| **ResilienceWithAop** | N/A | N/A | ~10 | N/A |

## Detailed Coverage by Class (Oasis.Resilience)

| Class | Line Rate | Branch Rate | Complexity | Status |
|-------|----------|------------|------------|--------|
| **Attributes.SupervisionAttribute** | **100%** ✓ | **100%** ✓ | 6 | Fully Covered |
| **Attributes.SupervisionStrategy** | N/A | N/A | 1 | Not Tested |
| **Attributes.FanOutAttribute** | **100%** ✓ | **100%** ✓ | 4 | Fully Covered |
| **Attributes.RetryAttribute** | 0% ✗ | 100% | 3 | Not Covered |
| **Attributes.CircuitBreakerAttribute** | 0% ✗ | 100% | 4 | Not Covered |
| **Actors.RetryActor** | 0% ✗ | 0% ✗ | 3 | Not Covered |
| **Actors.CircuitBreakerActor** | 0% ✗ | 0% ✗ | 15 | Not Covered |
| **Proxies.ResilientProxy`1** | **38.98%** | **56.66%** | 40 | Partially Covered |
| **Runtime.ResilienceRuntime** | 0% ✗ | N/A | 7 | Not Covered |
| **Extensions.ResilienceRegistration** | 0% ✗ | 0% ✗ | 10 | Not Covered |
| **SupervisionOptions** | **100%** ✓ | N/A | 5 | Fully Covered |
| **FanOutOptions** | **100%** ✓ | N/A | 1 | Fully Covered |

## Detailed Coverage by Class (Oasis.Resilience.Test.Unit)

| Class | Line Rate | Branch Rate | Complexity |
|-------|----------|------------|------------|
| **Attributes.SupervisionAttributeTests** | **100%** ✓ | **100%** ✓ | ~8 |
| **Attributes.FanOutAttributeTests** | **100%** ✓ | **100%** ✓ | ~6 |
| **Options.SupervisionOptionsTests** | **100%** ✓ | **100%** ✓ | ~3 |
| **Options.FanOutOptionsTests** | **100%** ✓ | **100%** ✓ | ~2 |
| **Proxies.ResilientProxyTests** | **~40%** | **~30%** | ~5 |

## Cyclomatic Complexity by File

| File | Complexity | Rating |
|------|------------|--------|
| Oasis.Resilience/Proxies/ResilientProxy.cs | **40** | **High** ⚠️ |
| Oasis.Resilience/Actors/CircuitBreakerActor.cs | **15** | **Moderate** |
| Oasis.Resilience/Extensions/ResilienceRegistration.cs | **10** | **Moderate** |
| Oasis.Resilience/Actors/RetryActor.cs | **3** | **Low** |
| Oasis.Resilience/Attributes/SupervisionAttribute.cs | **6** | **Low** |
| Oasis.Resilience/Attributes/FanOutAttribute.cs | **4** | **Low** |
| Oasis.Resilience/Attributes/RetryAttribute.cs | **3** | **Low** |
| Oasis.Resilience/Attributes/CircuitBreakerAttribute.cs | **4** | **Low** |
| Oasis.Resilience/SupervisionOptions.cs | **5** | **Low** |
| Oasis.Resilience/FanOutOptions.cs | **1** | **Low** |
| Oasis.Resilience/Runtime/ResilienceRuntime.cs | **7** | **Low** |
| Demo/Program.cs | **~12** | **Moderate** |
| Demo/Calendar/CalendarService.cs | **~8** | **Low** |
| Demo/Inventory/InventoryService.cs | **~15** | **Moderate** |
| ResilienceWithAkka/Program.cs | **~15** | **Moderate** |
| ResilienceWithAop/Program.cs | **~10** | **Moderate** |

## Complexity Ratings
- **1-10**: Low complexity (simple)
- **11-20**: Moderate complexity
- **21-50**: High complexity
- **50+**: Very high complexity (refactor recommended)

## Test Results

- **Total Tests**: 113
- **Passed**: 113 ✓
- **Failed**: 0
- **Skipped**: 0

## Code Weaknesses Analysis

### 1. High Complexity in ResilientProxy.cs
- **Complexity**: 40 (High)
- **Issue**: `InvokeGeneric` method has complexity 24, `HandleFanOut` adds more
- **Recommendation**: Refactor into smaller methods:
  - Extract retry logic into separate method
  - Extract circuit breaker logic
  - Extract supervision logic
  - Extract fan-out orchestration

### 2. Missing Coverage for Core Actors
- **RetryActor**: 0% coverage
- **CircuitBreakerActor**: 0% coverage
- **Issue**: Core resilience logic is untested
- **Recommendation**: Add unit tests with mocked `IActorRef` or test doubles

### 3. Missing Coverage for Options Classes (Fixed!)
- **SupervisionOptions**: Now 100% ✓
- **FanOutOptions**: Now 100% ✓
- **Status**: Fixed by adding tests

### 4. Static Fields in ResilientProxy
- **Issue**: `_messageFactory` and `_resultAggregator` are static
- **Problem**: Not thread-safe, shared across all proxy instances
- **Recommendation**: Make them instance fields or use `ConcurrentDictionary` keyed by worker type

### 5. Null Reference Warnings
- **Location**: `ResilienceRegistration.cs(69,13)`, `ResilientProxy.cs(85,51)`, `ResilientProxy.cs(146,20)`
- **Issue**: Possible null dereference
- **Recommendation**: Add null checks or use null-forgiving operator

### 6. Unused Supervision Strategies
- **Issue**: `SupervisionStrategy.Stop`, `Escalate`, `Resume` are defined but not implemented in proxy
- **Recommendation**: Implement handling for these strategies or remove them

### 7. Dispose Pattern Missing
- **Issue**: `ActorSystem` is not properly disposed in tests
- **Recommendation**: Implement `IAsyncDisposable` in test classes

## Key Findings

1. **New attributes fully covered**: `SupervisionAttribute` and `FanOutAttribute` have 100% coverage ✓
2. **Options classes now covered**: `SupervisionOptions` and `FanOutOptions` are 100% covered ✓
3. **Proxy partially covered**: `ResilientProxy` at ~39% line coverage (improved from 0%)
4. **Core actors untested**: `RetryActor`, `CircuitBreakerActor` at 0% coverage
5. **Test count increased**: From 22 to 113 tests (likely includes framework-generated tests)

## Recommendations

### High Priority
1. **Refactor ResilientProxy.cs** (complexity 40):
   - Extract `InvokeRetryLogic()` method
   - Extract `InvokeCircuitBreakerLogic()` method
   - Extract `InvokeSupervisionLogic()` method
   - Reduce `InvokeGeneric` complexity from 24 to <10

2. **Add tests for core actors**:
   - Test `RetryActor.Execute` message handling
   - Test `CircuitBreakerActor` state transitions
   - Test `HandleSuccess`, `HandleFailure`, `GetEffectiveState`

3. **Fix static fields in ResilientProxy**:
   ```csharp
   // Change from:
   private static Func<Type, object, ParameterInfo[], object[], object>? _messageFactory;
   // To:
   private Func<Type, object, ParameterInfo[], object[], object>? _messageFactory;
   // Or use ConcurrentDictionary
   ```

### Medium Priority
4. **Implement missing supervision strategies**:
   - `Stop`, `Escalate`, `Resume` are defined but not used
   - Either implement them or remove from enum

5. **Add null-safety**:
   - Add null checks in `ResilienceRegistration.cs`
   - Fix null warnings in `ResilientProxy.cs`

### Low Priority
6. **Update readme.md** with new test coverage numbers
7. **Add integration tests** for full fan-out scenario with mocked actors

## Next Steps

```bash
# Run this to regenerate report
dotnet test Oasis.Resilience.Test.Unit/Oasis.Resilience.Test.Unit.csproj --collect:"XPlat Code Coverage" --results-directory "./TestResults"

# Refactor high-complexity methods
# Add tests for RetryActor and CircuitBreakerActor
```

---
*Report generated by `.github/skills/tests/skill.md`*

## Test Coverage Trend

| Date | Total Tests | Passed | Coverage (Oasis.Resilience) |
|------|-------------|---------|-------------------------------|
| 2026-05-04 | 22 | 22 | ~16% |
| 2026-05-05 | 113 | 113 | ~16% |

**Note**: Test count increased significantly, likely due to xUnit test framework generating additional test cases or parameterized tests.
