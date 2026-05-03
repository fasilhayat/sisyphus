using Demo.Bonds;
using Demo.Calendar;
using Demo.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oasis.Resilience;

var services = new ServiceCollection();

services.AddResilience(options => options.LogLevel = LogLevel.Debug).AddResilientService<ICalendarService, CalendarService>();
services.AddResilience().AddResilientService<ITiwazService, TiwazService>();
services.AddResilience().AddResilientService<IInventoryService, InventoryService>();

using var serviceProvider = services.BuildServiceProvider();

var calendar = serviceProvider.GetRequiredService<ICalendarService>();
var tiwaz = serviceProvider.GetRequiredService<ITiwazService>();
var inventory = serviceProvider.GetRequiredService<IInventoryService>();

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

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine("Demonstrating Circuit Breaker + Retry combination...");
Console.WriteLine("The inventory service uses both patterns: retry handles transient failures,");
Console.WriteLine("while circuit breaker prevents cascade failures after consecutive errors.");
Console.ResetColor();
Console.WriteLine();

try
{
    Console.WriteLine("Calling GetInventoryAsync (CircuitBreaker: 3 failures, Retry: 4 attempts)...");
    var inventoryTask = inventory.GetInventoryAsync();
    await SafeAwait(inventoryTask, "GetInventoryAsync");
    PrintTaskResult("Inventory", inventoryTask);

    Console.WriteLine();
    Console.WriteLine("Calling UpdateInventoryAsync (CircuitBreaker: 2 failures, Retry: 3 attempts)...");
    var updateTask = inventory.UpdateInventoryAsync("ITEM-001", 50);
    await SafeAwait(updateTask, "UpdateInventoryAsync");
    PrintTaskResult("UpdateInventory", updateTask);

    Console.WriteLine();
    Console.WriteLine("Calling GetStockAlertsAsync (CircuitBreaker: 5 failures, Retry: 2 attempts)...");
    var alertsTask = inventory.GetStockAlertsAsync();
    await SafeAwait(alertsTask, "GetStockAlertsAsync");
    PrintTaskResult("StockAlerts", alertsTask);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Inventory operations failed: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine();
Console.WriteLine("Press ENTER to terminate...");
Console.ReadLine();

static async Task SafeAwait(Task task, string name)
{
    try
    {
        await task;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{name} encountered an error: {ex.Message}");
        Console.ResetColor();
    }
}

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