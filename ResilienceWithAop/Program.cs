using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;
using ResilienceWithAop;

var services = new ServiceCollection();
services.AddResilience()
    .AddResilientService<ITiwazService, TiwazService>();

using var serviceProvider = services.BuildServiceProvider();
var service = serviceProvider.GetRequiredService<ITiwazService>();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine( "Calling service using AOP resilience...");
Console.WriteLine("If the endpoint is unavailable, retries will be shown below.");
Console.ResetColor();

try
{
    var result = await service.GetBondsAsync();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Method call succeeded. Response:");
    Console.ResetColor();
    Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Method call failed after retries: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine("Press ENTER to terminate...");
Console.ReadLine();

services.Configure<RetryOptions>("ResilienceOptions", options =>
{
    options.LogLevel = LogLevel.Debug;
});
