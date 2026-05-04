# AOP + Akka.NET Supervision Enhancement Suggestions

## Vision

The true value of combining AOP with Akka.NET supervision is not just about retries, but about **orchestrating actor systems declaratively**. This allows developers to use simple attributes to spawn multiple actors for specific jobs while hiding the complexity of Akka.NET internals.

## Use Cases

1. **Simple**: Single supervised actor with retry and circuit-breaker
2. **Advanced**: Fan-out to multiple actors with supervision (e.g., actors for getting Norwegian holidays, Swedish holidays, Danish holidays for different year ranges)
3. **Data Processing**: Actors sharing a common task of getting large data and splitting retrieval jobs

---

## 1. New Attributes (Oasis.Resilience/Attributes/)

### SupervisionAttribute.cs

```csharp
using Akka.Actor;

namespace Oasis.Resilience.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SupervisionAttribute : Attribute
{
    public SupervisionStrategy Strategy { get; }
    public int MaxRetries { get; }
    public int BackoffMinMs { get; }
    public int BackoffMaxMs { get; }
    public double RandomFactor { get; }
    
    public SupervisionAttribute(
        SupervisionStrategy strategy = SupervisionStrategy.RestartWithBackoff,
        int maxRetries = 5,
        int backoffMinMs = 2000,
        int backoffMaxMs = 30000,
        double randomFactor = 0.2)
    {
        Strategy = strategy;
        MaxRetries = maxRetries;
        BackoffMinMs = backoffMinMs;
        BackoffMaxMs = backoffMaxMs;
        RandomFactor = randomFactor;
    }
}

public enum SupervisionStrategy
{
    Restart,
    Stop,
    Escalate,
    Resume,
    RestartWithBackoff
}
```

### FanOutAttribute.cs

```csharp
namespace Oasis.Resilience.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class FanOutAttribute : Attribute
{
    public Type WorkerActorType { get; }
    public string SplitParameterName { get; }
    public int MaxWorkers { get; }
    
    public FanOutAttribute(
        Type workerActorType,
        string splitParameterName,
        int maxWorkers = 5)
    {
        WorkerActorType = workerActorType;
        SplitParameterName = splitParameterName;
        MaxWorkers = maxWorkers;
    }
}
```

---

## 2. Demo Service Interface (Demo/Holidays/IHolidayService.cs)

```csharp
using Oasis.Resilience.Attributes;

namespace Demo.Holidays;

public interface IHolidayService
{
    // Simple: Single supervised actor with retry
    [Retry(maxAttempts: 3, initialDelay: 2000)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    Task<string> GetNorwegianHolidaysAsync(int year);
    
    // Simple: Single supervised actor with retry
    [Retry(maxAttempts: 3, initialDelay: 2000)]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    Task<string> GetDanishHolidaysAsync(int year);
    
    // Advanced: Fan-out to multiple actors with supervision
    [FanOut(
        workerActorType: typeof(HolidayWorkerActor),
        splitParameterName: "years",
        maxWorkers: 5
    )]
    [Supervision(strategy: SupervisionStrategy.RestartWithBackoff)]
    Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country);
}
```

---

## 3. Demo Service Implementation (Demo/Holidays/HolidayService.cs)

```csharp
namespace Demo.Holidays;

using Oasis.Resilience.Attributes;

public class HolidayService : IHolidayService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("https://holidays.api")
    };
    
    public async Task<string> GetNorwegianHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/norway/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<string> GetDanishHolidaysAsync(int year)
    {
        var response = await Client.GetAsync($"/denmark/{year}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    
    // AOP interceptor won't call this directly for FanOut
    // Instead, it uses this as a template for worker actors
    public async Task<Dictionary<int, string>> GetHolidaysForYearsAsync(int[] years, string country)
    {
        var result = new Dictionary<int, string>();
        foreach (var year in years)
        {
            var response = await Client.GetAsync($"/{country}/{year}");
            response.EnsureSuccessStatusCode();
            result[year] = await response.Content.ReadAsStringAsync();
        }
        return result;
    }
}
```

---

## 4. Worker Actor (Demo/Holidays/Actors/HolidayWorkerActor.cs)

```csharp
using Akka.Actor;

namespace Demo.Holidays.Actors;

public sealed class HolidayWorkerActor : ReceiveActor
{
    private readonly HttpClient _client = new() 
    { 
        BaseAddress = new Uri("https://holidays.api") 
    };
    
    public HolidayWorkerActor()
    {
        ReceiveAsync<ProcessYear>(async msg =>
        {
            try
            {
                var response = await _client.GetAsync($"/{msg.Country}/{msg.Year}");
                response.EnsureSuccessStatusCode();
                Sender.Tell(new YearProcessed(msg.Year, await response.Content.ReadAsStringAsync()));
            }
            catch (Exception ex)
            {
                Sender.Tell(new Status.Failure(ex));
            }
        });
    }
    
    public sealed record ProcessYear(int Year, string Country);
    public sealed record YearProcessed(int Year, string Content);
}
```

---

## 5. AOP Interceptor Concept (Oasis.Resilience/Proxies/ResilientProxy.cs)

```csharp
using Akka.Actor;
using Akka.Pattern;

namespace Oasis.Resilience.Proxies;

public partial class ResilientProxy
{
    private async Task<object?> HandleFanOut(MethodInfo method, object[] args, FanOutAttribute fanOut)
    {
        // Create BackoffSupervisor for worker actors
        var supervisorProps = BackoffSupervisor.Props(
            childProps: Props.Create(() => (ActorBase)Activator.CreateInstance(fanOut.WorkerActorType)!),
            childNamePrefix: fanOut.WorkerActorType.Name,
            minBackoff: TimeSpan.FromMilliseconds(fanOut.BackoffMinMs),
            maxBackoff: TimeSpan.FromMilliseconds(fanOut.BackoffMaxMs),
            randomFactor: fanOut.RandomFactor
        );
        
        var supervisor = _actorSystem.ActorOf(supervisorProps, $"{fanOut.WorkerActorType.Name}-supervisor");
        
        // Split work based on splitParameterName
        var splitParamIndex = Array.FindIndex(method.GetParameters(), 
            p => p.Name == fanOut.SplitParameterName);
        var years = (int[])args[splitParamIndex];
        
        var country = (string)args[1]; // Assuming country is second parameter
        
        // Fan-out: send to multiple workers
        var tasks = years.Select(year =>
            supervisor.Ask<HolidayWorkerActor.YearProcessed>(
                new HolidayWorkerActor.ProcessYear(year, country), 
                TimeSpan.FromSeconds(30))
        ).ToArray();
        
        // Fan-in: collect results
        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.Year, r => r.Content);
    }
    
    private void HandleSupervisionAttribute(MethodInfo method, SupervisionAttribute supervision)
    {
        // Apply supervision strategy to actor creation
        // This configures how the actor system handles failures
        switch (supervision.Strategy)
        {
            case SupervisionStrategy.RestartWithBackoff:
                // Use BackoffSupervisor props
                break;
            case SupervisionStrategy.Restart:
                // Use standard supervision with Restart directive
                break;
            case SupervisionStrategy.Stop:
                // Stop actor on failure
                break;
            case SupervisionStrategy.Escalate:
                // Escalate to parent
                break;
            case SupervisionStrategy.Resume:
                // Resume without restarting
                break;
        }
    }
}
```

---

## Implementation Steps

1. **Add new attributes** to `Oasis.Resilience/Attributes/`
2. **Create Demo/Holidays/** folder with service interface and implementation
3. **Create Demo/Holidays/Actors/** folder with worker actor
4. **Update ResilientProxy.cs** to handle `FanOutAttribute` and `SupervisionAttribute`
5. **Update DependencyInjection** to register holiday service with resilience
6. **Update Demo/Program.cs** to demonstrate fan-out scenario

---

## Benefits

- **Declarative actor orchestration** - developers just add attributes
- **Hidden complexity** - AOP layer handles spawning, supervision, fan-out/fan-in
- **Proper Akka.NET usage** - leverages `BackoffSupervisor`, supervision strategies
- **Scalable** - easy to add more worker types and fan-out scenarios
