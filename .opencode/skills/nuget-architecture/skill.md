---
name: nuget-architecture
description: 'Guidelines for building NuGet packages with Akka.NET resilience patterns, including proxy-based resilience, actor systems, and proper test coverage. Use when creating NuGet packages, working with Akka.NET actors, or implementing resilience patterns.'
---

# NuGet Architecture Skill

Guidelines for building NuGet packages following the Oasis.Resilience architecture pattern.

## When to Use This Skill

- When creating a new NuGet package
- When implementing Akka.NET actor-based resilience
- When building proxy-based interception patterns
- When asked to "follow the NuGet architecture" or "use the resilience pattern"
- When implementing retry, circuit breaker, supervision, or fan-out patterns

## Architecture Overview

This architecture uses a **proxy pattern with Akka.NET actors** to add resilience to method calls:

```
User Code → DispatchProxy → ResilientProxy → Akka Actors (Retry/CircuitBreaker/Supervision) → Decorated Instance
```

### Core Components

1. **Attributes** (`/Attributes/`)
   - `RetryAttribute` - Marks methods for retry logic
   - `CircuitBreakerAttribute` - Marks methods for circuit breaker
   - `SupervisionAttribute` - Marks methods for actor supervision
   - `FanOutAttribute` - Marks methods for fan-out parallelism

2. **Proxies** (`/Proxies/`)
   - `ResilientProxy<T>` - Main proxy using `DispatchProxy` base class
   - Intercepts method calls and applies resilience attributes

3. **Actors** (`/Actors/`)
   - `RetryActor` - Handles retry logic with exponential backoff
   - `CircuitBreakerActor` - Implements circuit breaker pattern
   - Worker actors - For fan-out parallelism

4. **Options** (`/*.cs`)
   - `RetryOptions` - Configuration for retry behavior
   - `CircuitBreakerOptions` - Configuration for circuit breaker
   - `SupervisionOptions` - Configuration for supervision
   - `FanOutOptions` - Configuration for fan-out

5. **Extensions** (`/Extensions/`)
   - `ResilienceRegistration` - ServiceCollection extensions for DI

## Project Structure

```
MyNuGetPackage/
├── Attributes/              # Attribute definitions
│   ├── RetryAttribute.cs
│   ├── CircuitBreakerAttribute.cs
│   ├── SupervisionAttribute.cs
│   └── FanOutAttribute.cs
├── Proxies/                # Proxy implementations
│   └── ResilientProxy.cs
├── Actors/                  # Akka.NET actors
│   ├── RetryActor.cs
│   └── CircuitBreakerActor.cs
├── Extensions/              # DI registration
│   └── ResilienceRegistration.cs
├── *.csproj                 # Package project
└── README.md

MyNuGetPackage.Test.Unit/   # Unit tests
├── Proxies/                 # Proxy tests
│   ├── ProxyTestBase.cs    # Base class with suppressed ActorSystem logging
│   └── *Tests.cs
├── Actors/                  # Actor tests
│   └── *Tests.cs
└── *.csproj
```

## Key Implementation Patterns

### 1. Proxy Pattern with DispatchProxy

```csharp
public class ResilientProxy<T> : DispatchProxy
{
    public T DecoratedInstance { get; set; }
    public ActorSystem ActorSystem { get; set; }
    public IActorRef ResilienceActorRef { get; set; }
    public IActorRef CircuitBreakerActorRef { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        // Check for resilience attributes
        var retryAttr = method.GetCustomAttribute<RetryAttribute>();
        var breakerAttr = method.GetCustomAttribute<CircuitBreakerAttribute>();

        if (retryAttr is null && breakerAttr is null)
            return targetMethod.Invoke(DecoratedInstance, args);

        return InvokeResilient(method, args, retryAttr, breakerAttr);
    }
}
```

### 2. Suppress CoordinatedShutdown in Tests

Always use `ProxyTestBase` for test classes that create ActorSystems:

```csharp
public abstract class ProxyTestBase : IDisposable
{
    private readonly List<ActorSystem> _actorSystems = new();

    protected ActorSystem CreateActorSystem(string name)
    {
        var config = ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
            akka.coordinated-shutdown.log-level = ERROR
            akka.log-config-on-start = off
        ");
        var system = ActorSystem.Create(name, config);
        _actorSystems.Add(system);
        return system;
    }

    public void Dispose()
    {
        foreach (var system in _actorSystems)
            system.Terminate().Wait(TimeSpan.FromSeconds(5));
    }
}

// In test class:
public class MyTests : ProxyTestBase
{
    [Fact]
    public void My_test()
    {
        var system = CreateActorSystem($"test-{Guid.NewGuid()}");
        // ... test code
    }
}
```

### 3. For TestKit-based Tests

When using `Akka.TestKit.Xunit2`:

```csharp
public class MyActorTests : TestKit
{
    public MyActorTests() : base(GetConfig()) { }

    private static Config GetConfig()
    {
        return ConfigurationFactory.ParseString(@"
            akka.loglevel = ERROR
            akka.stdout-loglevel = ERROR
            akka.coordinated-shutdown.log-level = ERROR
        ");
    }
}
```

### 4. XML Documentation

Add XML documentation to all public types and members:

```csharp
/// <summary>
/// Provides a dynamic proxy that adds resilience features to method invocations.
/// </summary>
/// <typeparam name="T">The interface type to proxy.</typeparam>
public class ResilientProxy<T> : DispatchProxy
{
    /// <summary>
    /// Gets or sets the instance being decorated.
    /// </summary>
    public T DecoratedInstance { get; set; }
}
```

## Build Requirements

- **0 build warnings**
- **0 build errors**
- **XML documentation enabled** in .csproj:
  ```xml
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  ```

## Test Requirements

- **0 CoordinatedShutdown messages** during `dotnet test`
- **All tests pass** (100% pass rate)
- **Coverage > 70%** (target: 80%+)
- Use `ProxyTestBase` for all test classes creating ActorSystems
- Use `xunit` with `Akka.TestKit.Xunit2` for actor tests

## NuGet Package Requirements

- **PackageLicenseExpression** or **PackageLicenseFile**
- **PackageReadmeFile** pointing to README.md
- **GeneratePackageOnBuild** disabled (build separately)
- XML documentation file included in package

## GitHub Action Workflow

Create `.github/workflows/nuget.yml`:

```yaml
name: NuGet CI

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build-test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'
    
    - name: Restore
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --collect:"XPlat Code Coverage"
    
    - name: Upload coverage
      uses: codecov/codecov-action@v4
```

## Example User Prompts

- "Create a new NuGet package following the resilience architecture"
- "Add circuit breaker support to the proxy"
- "Fix CoordinatedShutdown messages in tests"
- "Add XML documentation to all classes"
- "Create a worker actor for fan-out pattern"
