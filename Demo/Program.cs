using Demo.Bonds;
using Demo.Calendar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;

var services = new ServiceCollection();

services.AddResilience(options => options.LogLevel = LogLevel.Debug).AddResilientService<ICalendarService, CalendarService>();
services.AddResilience().AddResilientService<ITiwazService, TiwazService>();

using var serviceProvider = services.BuildServiceProvider();

var calendar = serviceProvider.GetRequiredService<ICalendarService>();
var tiwaz = serviceProvider.GetRequiredService<ITiwazService>();

try
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Calling service using AOP resilience...");
    Console.WriteLine("If the endpoint is unavailable, retries will be shown below.");
    Console.ResetColor();

    var danishTask = calendar.GetDanishHolidaysAsync();
    var norwegianTask = calendar.GetNorwegianHolidaysAsync();
    var bondTask = tiwaz.GetBondsAsync();

    try
    {
        await Task.WhenAll(danishTask, norwegianTask, bondTask);
    }
    catch(Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"Task.WhenAll observed one or more failures and threw error: '{ex.Message}'.\nContinuing so each task can be inspected separately.");
        Console.ResetColor();
    }

    PrintTaskResult("Danish", danishTask);
    PrintTaskResult("Norwegian", norwegianTask);
    PrintTaskResult("Bonds", bondTask);
}
catch (Exception ex)
{
    Console.WriteLine($"Call failed after retries: {ex.Message}");
}

Console.WriteLine("Press ENTER to terminate...");
Console.ReadLine();

static void PrintTaskResult(string name, Task<string> task)
{
    if (task.IsCompletedSuccessfully)
    {
        Console.WriteLine($"{name} succeeded:");
        Console.WriteLine(task.Result);
        return;
    }

    if (task.IsFaulted)
    {
        Console.WriteLine($"{name} failed: {task.Exception?.GetBaseException().Message}");
        return;
    }

    if (task.IsCanceled)
    {
        Console.WriteLine($"{name} was cancelled.");
        return;
    }

    Console.WriteLine($"{name} ended in unexpected state.");
}