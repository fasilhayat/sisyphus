namespace DemoWithNuGet.Inventory;

/// <summary>
/// Defines the contract for inventory management operations.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Retrieves the full inventory list asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation containing the inventory data as a string.</returns>
    Task<string> GetInventoryAsync();

    /// <summary>
    /// Updates the quantity of a specific inventory item asynchronously.
    /// </summary>
    /// <param name="itemId">The unique identifier of the inventory item to update.</param>
    /// <param name="quantity">The new quantity to set for the item.</param>
    /// <returns>A task representing the asynchronous operation containing the confirmation as a string.</returns>
    Task<string> UpdateInventoryAsync(string itemId, int quantity);

    /// <summary>
    /// Fetches stock alerts for items that are low or out of stock asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation containing the stock alert data as a string.</returns>
    Task<string> GetStockAlertsAsync();
}
