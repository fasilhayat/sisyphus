---
name: nuget-architecture
description: 'Guidelines for building NuGet packages with proper project structure, testing patterns, documentation, and CI/CD. Use when creating NuGet packages, structuring library projects, or setting up test projects.'
---

# NuGet Architecture Skill

Generic guidelines for building well-structured NuGet packages.

## When to Use This Skill

- When creating a new NuGet package
- When structuring a .NET library project
- When setting up unit test projects
- When asked to "follow the NuGet architecture" or "structure the project properly"
- When configuring CI/CD for a library

## Project Structure

### Solution Layout

```
MySolution.sln
├── src/
│   └── MyLibrary/
│       ├── Models/              # Data models and DTOs
│       ├── Services/            # Core business logic
│       ├── Interfaces/         # Public contracts
│       ├── Extensions/         # Extension methods and DI registration
│       ├── Helpers/            # Internal utility classes
│       ├── MyLibrary.csproj
│       └── README.md
│
├── tests/
│   └── MyLibrary.Test.Unit/
│       ├── Unit/
│       ├── Helpers/
│       └── MyLibrary.Test.Unit.csproj
│
└── samples/                    # Optional: sample usage projects
    └── MyLibrary.Sample/
```

### Main Library Project (src/MyLibrary/)

```
MyLibrary/
├── Models/
│   └── MyModel.cs
├── Services/
│   └── MyService.cs
├── Interfaces/
│   └── IMyService.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
├── Helpers/
│   └── MyHelper.cs
├── MyLibrary.csproj
└── README.md
```

### Test Project (tests/MyLibrary.Test.Unit/)

```
MyLibrary.Test.Unit/
├── Unit/
│   └── MyServiceTests.cs
├── Helpers/
│   └── TestHelper.cs
├── MyLibrary.Test.Unit.csproj
└── Usings.cs
```

## Main Library .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <PropertyGroup>
    <PackageId>MyLibrary</PackageId>
    <Version>1.0.0</Version>
    <Authors>YourName</Authors>
    <Description>A brief description of the library.</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

</Project>
```

## Test Project .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MyLibrary\MyLibrary.csproj" />
  </ItemGroup>

</Project>
```

## Code Patterns

### 1. Interface Definition

```csharp
/// <summary>
/// Defines operations for MyService.
/// </summary>
public interface IMyService
{
    /// <summary>
    /// Gets the value for the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value associated with the key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found.</exception>
    string GetValue(string key);
}
```

### 2. Service Implementation

```csharp
/// <summary>
/// Implements the <see cref="IMyService"/> interface.
/// </summary>
public class MyService : IMyService
{
    /// <summary>
    /// Gets the value for the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value associated with the key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found.</exception>
    public string GetValue(string key)
    {
        // Implementation
    }
}
```

### 3. DI Extensions

```csharp
/// <summary>
/// Extensions for <see cref="IServiceCollection"/> to register library services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MyLibrary services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMyLibrary(this IServiceCollection services)
    {
        services.AddTransient<IMyService, MyService>();
        return services;
    }
}
```

### 4. Test Base Class (Optional)

```csharp
/// <summary>
/// Base class for tests providing shared setup.
/// </summary>
public abstract class TestBase : IDisposable
{
    private readonly List<IDisposable> _resources = new();

    /// <summary>
    /// Registers a resource for cleanup.
    /// </summary>
    protected void Register(IDisposable resource) => _resources.Add(resource);

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var resource in _resources)
            resource.Dispose();
    }
}
```

## Build Requirements

- **0 build warnings** (TreatWarningsAsErrors enabled)
- **0 build errors**
- **XML documentation** enabled
- **GeneratePackageOnBuild** disabled (build packages separately)

## Test Requirements

- **All tests pass** (100% pass rate)
- **No unwanted output** during test runs
- **Coverage > 70%** (target: 80%+)
- Use base test classes for shared setup
- Use `xunit` or preferred test framework
- Mock external dependencies

## README.md Requirements

Include in the package README.md:

```markdown
# MyLibrary

A brief description of what the library does.

## Installation

```bash
dotnet add package MyLibrary
```

## Usage

```csharp
using MyLibrary;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddMyLibrary();
var provider = services.BuildServiceProvider();

var service = provider.GetRequiredService<IMyService>();
var result = service.GetValue("key");
```

## Features

- Feature 1
- Feature 2

## Requirements

- .NET 8.0 or later
```

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
      with:
        files: ./**/TestResults/**/coverage.cobertura.xml
        fail_ci_if_error: false
```

## .gitignore Additions

Add to `.gitignore`:

```
# Coverage results
TestResults/
*.cobertura.xml

# NuGet packages
*.nupkg
*.snupkg

# Build outputs
**/bin/
**/obj/
```

## Example User Prompts

- "Create a new NuGet package"
- "Structure the project properly"
- "Add XML documentation to all classes"
- "Set up the test project"
- "Configure GitHub Actions for CI"
- "Create a README.md for the package"
- "Follow the NuGet architecture"

## Notes

- Keep the library focused on a single responsibility
- Use interfaces for all public-facing types
- Provide sensible defaults in configuration classes
- Write tests for both success and failure paths
- Include a comprehensive README.md with usage examples
- Use semantic versioning for package versions
