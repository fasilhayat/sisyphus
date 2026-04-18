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
    var danishTask = calendar.GetDanishHolidaysAsync();
    var norwegianTask = calendar.GetNorwegianHolidaysAsync();

    try
    {
        await Task.WhenAll(danishTask, norwegianTask);
    }
    catch
    {
        // Do not fail entire demo here.
        // Inspect tasks individually below.
    }

    if (danishTask.IsFaulted)
    {
        Console.WriteLine($"Danish failed: " + danishTask.Exception?.GetBaseException().Message);
    }
    else if (danishTask.IsCompletedSuccessfully)
    {
        Console.WriteLine("Danish succeeded:");
        Console.WriteLine(danishTask.Result);
    }

    if (norwegianTask.IsFaulted)
    {
        Console.WriteLine($"Norwegian failed: " + norwegianTask.Exception?.GetBaseException().Message);
    }
    else if (norwegianTask.IsCompletedSuccessfully)
    {
        Console.WriteLine("Norwegian succeeded:");
        Console.WriteLine(norwegianTask.Result);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Call failed after retries: {ex.Message}");
}

Console.WriteLine("Press ENTER to terminate...");
Console.ReadLine();