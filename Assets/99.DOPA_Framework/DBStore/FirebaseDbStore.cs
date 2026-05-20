using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

public class FirebaseDbStore : IDbStore
{
    private readonly FirebaseManager firebaseManager;
    private FirebaseFirestore firestore;

    public FirebaseDbStore(FirebaseManager firebaseManager)
    {
        this.firebaseManager = firebaseManager;
    }

    public Task<bool> InitializeAsync()
    {
        try
        {
            firestore = FirebaseFirestore.DefaultInstance;

            firestore.Settings.PersistenceEnabled = true;
            firestore.Settings.CacheSizeBytes = FirebaseFirestoreSettings.CacheSizeUnlimited;

            Debug.Log("[FirebaseDbStore] InitializeAsync success");
            return Task.FromResult(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] InitializeAsync error: {e}");
            return Task.FromResult(false);
        }
    }

    public async Task<Dictionary<string, string>> LoadRawAsync(string userId, HashSet<string> keys)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[FirebaseDbStore] LoadRawAsync failed: userId is null or empty");
            foreach (var key in keys)
            {
                result[key] = "{}";
            }
            return result;
        }

        if (keys == null || keys.Count == 0)
        {
            return result;
        }

        try
        {
            DocumentReference docRef = GetPlayerDoc(userId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            foreach (var key in keys)
            {
                result[key] = "{}";
            }

            if (!snapshot.Exists)
            {
                return result;
            }

            foreach (var key in keys)
            {
                if (snapshot.TryGetValue<string>(key, out var json) && !string.IsNullOrEmpty(json))
                {
                    result[key] = json;
                }
            }

            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] LoadRawAsync error: {e}");

            foreach (var key in keys)
            {
                if (!result.ContainsKey(key))
                {
                    result[key] = "{}";
                }
            }

            return result;
        }
    }

    public async Task<byte[]> LoadDataAsync(string key)
    {
        try
        {
            DocumentReference docRef = firestore.Collection("players").Document(key);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                return System.Text.Encoding.UTF8.GetBytes(snapshot.ToString());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] LoadPlayerDataAsync error: {e}");
        }
        return null;
    }
    
    public async Task SaveRawAsync(string userId, Dictionary<string, string> keyValuePairs)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[FirebaseDbStore] SaveRawAsync failed: userId is null or empty");
            return;
        }

        if (keyValuePairs == null || keyValuePairs.Count == 0)
        {
            return;
        }

        try
        {
            DocumentReference docRef = GetPlayerDoc(userId);

            var update = new Dictionary<string, object>(keyValuePairs.Count);
            foreach (var kvp in keyValuePairs)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                {
                    continue;
                }

                string json = string.IsNullOrEmpty(kvp.Value) ? "{}" : kvp.Value;
                update[kvp.Key] = json;
            }

            if (update.Count == 0)
            {
                return;
            }

            await docRef.SetAsync(update, SetOptions.MergeAll);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] SaveRawAsync error: {e}");
        }
    }

    public async Task UpdateFieldAsync(string userId, string field, object value)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[FirebaseDbStore] UpdateFieldAsync failed: userId is null or empty");
            return;
        }

        if (string.IsNullOrEmpty(field))
        {
            Debug.LogError("[FirebaseDbStore] UpdateFieldAsync failed: field is null or empty");
            return;
        }

        try
        {
            DocumentReference docRef = GetPlayerDoc(userId);

            var updates = new Dictionary<string, object>(1);
            updates[field] = value;

            await docRef.UpdateAsync(updates);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] UpdateFieldAsync error: {e}");
        }
    }

    public async Task UpdateInventoryItemAsync(string userId, string field, string itemKey, int quantity)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[FirebaseDbStore] UpdateInventoryItemAsync failed: userId is null or empty");
            return;
        }

        if (string.IsNullOrEmpty(field))
        {
            Debug.LogError("[FirebaseDbStore] UpdateInventoryItemAsync failed: field is null or empty");
            return;
        }

        if (string.IsNullOrEmpty(itemKey))
        {
            Debug.LogError("[FirebaseDbStore] UpdateInventoryItemAsync failed: itemKey is null or empty");
            return;
        }

        try
        {
            DocumentReference docRef = GetPlayerDoc(userId);

            var updates = new Dictionary<string, object>(1);
            updates[$"{field}.{itemKey}"] = quantity;

            await docRef.UpdateAsync(updates);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] UpdateInventoryItemAsync error: {e}");
        }
    }

    public async Task IncrementInventoryItemAsync(string userId, string field, string itemKey, int incrementValue)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[FirebaseDbStore] IncrementInventoryItemAsync failed: userId is null or empty");
            return;
        }

        if (string.IsNullOrEmpty(field))
        {
            Debug.LogError("[FirebaseDbStore] IncrementInventoryItemAsync failed: field is null or empty");
            return;
        }

        if (string.IsNullOrEmpty(itemKey))
        {
            Debug.LogError("[FirebaseDbStore] IncrementInventoryItemAsync failed: itemKey is null or empty");
            return;
        }

        try
        {
            DocumentReference docRef = GetPlayerDoc(userId);

            var updates = new Dictionary<string, object>(1);
            updates[$"{field}.{itemKey}"] = FieldValue.Increment(incrementValue);

            await docRef.UpdateAsync(updates);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] IncrementInventoryItemAsync error: {e}");
        }
    }

    public async Task<bool> CheckDeviceValidationAsync(string userId, string localDeviceId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[FirebaseDbStore] CheckDeviceValidationAsync failed: userId is null or empty");
            return false;
        }

        if (string.IsNullOrEmpty(localDeviceId))
        {
            Debug.LogError("[FirebaseDbStore] CheckDeviceValidationAsync failed: localDeviceId is null or empty");
            return false;
        }

        try
        {
            DocumentReference docRef = GetPlayerDoc(userId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                Debug.LogError("[FirebaseDbStore] CheckDeviceValidationAsync failed: player document not found");
                return false;
            }

            if (snapshot.TryGetValue<string>("deviceId", out var firestoreDeviceId))
            {
                return firestoreDeviceId == localDeviceId;
            }

            var updates = new Dictionary<string, object>(1);
            updates["deviceId"] = localDeviceId;

            await docRef.SetAsync(updates, SetOptions.MergeAll);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseDbStore] CheckDeviceValidationAsync error: {e}");
            return false;
        }
    }

    private DocumentReference GetPlayerDoc(string userId)
    {
        return firestore.Collection("players").Document(userId);
    }
}