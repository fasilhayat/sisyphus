namespace DemoWithNuGet.Inventory;

using Oasis.Resilience.Attributes;

/// <summary>
/// Service responsible for managing inventory operations including retrieval, updates, and stock alerts.
/// Communicates with the external inventory service at https://inventory.hayatnet.local.
/// </summary>
public class InventoryService : IInventoryService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("https://inventory.hayatnet.local")
    };

    /// <summary>
    /// Retrieves the full inventory list from the inventory service.
    /// Protected by circuit breaker (3 failures) and retry (4 attempts, 1s delay).
    /// </summary>
    /// <returns>A string containing the inventory data.</returns>
    [CircuitBreaker(failureThreshold: 3, resetTimeout: 10000, maxConcurrentCalls: 2)]
    [Retry(maxAttempts: 4, initialDelay: 1000)]
    public async Task<string> GetInventoryAsync()
    {
        Console.WriteLine("Calling inventory endpoint...");
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/inventory");
        req.Headers.Add("accept", "*/*");
        req.Headers.Add("X-API-KEY", "Skyw@lker!");

        var response = await Client.SendAsync(req);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Updates the quantity of a specific inventory item.
    /// Protected by circuit breaker (2 failures) and retry (3 attempts, 2s delay).
    /// </summary>
    /// <param name="itemId">The unique identifier of the inventory item to update.</param>
    /// <param name="quantity">The new quantity to set for the item.</param>
    /// <returns>A string confirming the inventory update.</returns>
    [CircuitBreaker(failureThreshold: 2, resetTimeout: 15000, maxConcurrentCalls: 1)]
    [Retry(maxAttempts: 3, initialDelay: 2000)]
    public async Task<string> UpdateInventoryAsync(string itemId, int quantity)
    {
        Console.WriteLine($"Updating inventory: {itemId} with quantity {quantity}");
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/inventory/{itemId}");
        req.Content = new StringContent($"{{\"quantity\": {quantity}}}");
        req.Headers.Add("accept", "*/*");
        req.Headers.Add("X-API-KEY", "Skyw@lker!");

        var response = await Client.SendAsync(req);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Fetches stock alerts for items that are low or out of stock.
    /// Protected by circuit breaker (5 failures) and retry (2 attempts, 500ms delay).
    /// </summary>
    /// <returns>A string containing the stock alert data.</returns>
    [CircuitBreaker(failureThreshold: 5, resetTimeout: 20000, maxConcurrentCalls: 1)]
    [Retry(maxAttempts: 2, initialDelay: 500)]
    public async Task<string> GetStockAlertsAsync()
    {
        Console.WriteLine("Fetching stock alerts...");
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/inventory/alerts");
        req.Headers.Add("accept", "*/*");
        req.Headers.Add("X-API-KEY", "Skyw@lker!");

        var response = await Client.SendAsync(req);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
