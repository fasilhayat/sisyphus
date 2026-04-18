using Demo;
using Microsoft.Extensions.DependencyInjection;
using Oasis.Resilience;

var services = new ServiceCollection();

services.AddResilience().AddResilientService<ICalendarService, CalendarService>();
using var serviceProvider = services.BuildServiceProvider();
var calendar = serviceProvider.GetRequiredService<ICalendarService>();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("Calling Calendar API using AOP resilience...");
Console.WriteLine("Stop/start backend while retries are running to test recovery.");
Console.ResetColor();

try
{
    var result = await calendar.GetDanishHolidaysAsync();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Call succeeded. Response:");
    Console.ResetColor();
    Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Call failed after retries: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine("Press ENTER to terminate...");
Console.ReadLine();