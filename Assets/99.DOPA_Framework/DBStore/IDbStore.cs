using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IDbStore
{
    /// <summary>
    /// Initialize DB backend. Should be called after its SDK manager is initialized.
    /// </summary>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Load multiple raw JSON values by keys for a specific user.
    /// Returns: key -> json string (or empty if not found).
    /// </summary>
    Task<Dictionary<string, string>> LoadRawAsync(string userId, HashSet<string> keys);

    /// <summary>
    /// Save multiple raw JSON values by keys for a specific user.
    /// input: key -> json string.
    /// </summary>
    Task SaveRawAsync(string userId, Dictionary<string, string> keyValuePairs);

    /// <summary>
    /// Update a single field.
    /// </summary>
    Task UpdateFieldAsync(string userId, string field, object value);

    /// <summary>
    /// Update nested inventory item like inventory.itemKey
    /// </summary>
    Task UpdateInventoryItemAsync(string userId, string field, string itemKey, int quantity);
    
    /// <summary>
    /// Increment nested inventory item value
    /// </summary>
    Task IncrementInventoryItemAsync(string userId, string field, string itemKey, int incrementValue);

    Task<byte[]> LoadDataAsync(string key);
}