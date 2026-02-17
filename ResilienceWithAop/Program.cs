using Akka.Actor;
using Oasis.Resilience;
using ResilienceWithAop;
using System.Reflection;


// ===== Actor system =====
using var system = ActorSystem.Create("tiwaz-system");

// ===== Create proxy for your service =====
ITiwazService service = DispatchProxy.Create<ITiwazService, ResilientProxy<ITiwazService>>();
((ResilientProxy<ITiwazService>)service).DecoratedInstance = new TiwazService();
((ResilientProxy<ITiwazService>)service).ProxyActorSystem = system;

// ===== Call the decorated method =====
try
{
    var result = await service.GetBondsAsync();
    Console.WriteLine("Method call succeeded. Response:");
    Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.WriteLine($"Method call failed after retries: {ex.Message}");
}

Console.WriteLine("Press ENTER to terminate...");
Console.ReadLine();

await system.Terminate();
