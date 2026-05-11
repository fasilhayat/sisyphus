namespace Demo.Inventory;

using Oasis.Resilience.Attributes;

/// <summary>
/// Demonstrates the <see cref="CircuitBreakerAttribute"/> combined with <see cref="RetryAttribute"/>.
/// Retry handles transient blips; the circuit breaker opens after 3 exhausted retry sequences and
/// rejects all further calls until the 5-second reset timeout elapses (transitioning to half-open).
/// </summary>
public class InventoryService : IInventoryService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("http://localhost:5080")
    };

    /// <summary>
    /// Retrieves the full inventory list.
    /// Circuit opens after 3 failures; resets after 5 seconds.
    /// </summary>
    [CircuitBreaker(failureThreshold: 3, resetTimeout: 5000, maxConcurrentCalls: 1)]
    [Retry(maxAttempts: 2, initialDelay: 300)]
    public async Task<string> GetInventoryAsync()
    {
        var response = await Client.GetAsync("/inventory");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Updates a specific inventory item.</summary>
    [CircuitBreaker(failureThreshold: 2, resetTimeout: 5000, maxConcurrentCalls: 1)]
    [Retry(maxAttempts: 2, initialDelay: 300)]
    public async Task<string> UpdateInventoryAsync(string itemId, int quantity)
    {
        var content = new StringContent(
            $"{{\"quantity\":{quantity}}}",
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await Client.PostAsync($"/inventory/{itemId}", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Fetches stock alerts for low-stock items.</summary>
    [CircuitBreaker(failureThreshold: 5, resetTimeout: 5000, maxConcurrentCalls: 1)]
    [Retry(maxAttempts: 2, initialDelay: 300)]
    public async Task<string> GetStockAlertsAsync()
    {
        var response = await Client.GetAsync("/inventory/alerts");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
