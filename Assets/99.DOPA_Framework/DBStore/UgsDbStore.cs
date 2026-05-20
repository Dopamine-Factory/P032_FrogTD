using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.VisualScripting;
using UnityEngine;

public class UgsDbStore : IDbStore
{
    private readonly UgsManager ugsManager;

    private const int CacheTtlSeconds = 300;
    private const int MaxRetries = 3;
    private const float BaseRetryDelaySeconds = 1f;

    private readonly Dictionary<string, CachedItem> cacheByKey = new Dictionary<string, CachedItem>();
    private readonly Queue<PendingSave> offlineQueue = new Queue<PendingSave>();
    private bool isProcessingQueue;

    public event Action<bool, string> OnLoading;
    public event Action<string> OnError;

    private class CachedItem
    {
        public string Json;
        public DateTime TimestampUtc;
    }

    private class PendingSave
    {
        public Dictionary<string, string> KeyValuePairs;
        public DateTime QueuedAtUtc;
    }

    public UgsDbStore(UgsManager ugsManager)
    {
        this.ugsManager = ugsManager;
    }

    public Task<bool> InitializeAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        if (ugsManager?.IsInitialized != true)
        {
            Debug.LogWarning("[UgsDbStore] UgsManager not initialized");
            tcs.SetResult(false);
            return tcs.Task;
        }

        if (AuthManager.Instance == null)
        {
            Debug.LogError("[UgsDbStore] AuthManager instance is null");
            tcs.SetResult(false);
            return tcs.Task;
        }

        if (AuthManager.Instance.IsLoggedIn)
        {
            RestoreOfflineQueueFromPrefs();
            tcs.SetResult(true);
            return tcs.Task;
        }

        if (SDKManager.Instance == null)
        {
            Debug.LogError("[UgsDbStore] SDKManager instance is null. Cannot run coroutine.");
            tcs.SetResult(false);
            return tcs.Task;
        }

        Debug.Log("[UgsDbStore] Waiting for AuthManager.IsLoggedIn...");

        // Unity Coroutine으로 대기 → Task로 변환
        SDKManager.Instance.RunCoroutine(WaitForAuthCoroutine(tcs));

        return tcs.Task;
    }
    private const float AuthWaitTimeoutSeconds = 2f;
    private const float AuthPollIntervalSeconds = 0.1f;
    private IEnumerator WaitForAuthCoroutine(TaskCompletionSource<bool> tcs)
    {
        float timeoutAt = Time.realtimeSinceStartup + AuthWaitTimeoutSeconds;

        while (AuthManager.Instance != null &&
               !AuthManager.Instance.IsLoggedIn &&
               Time.realtimeSinceStartup < timeoutAt)
        {
            yield return new WaitForSeconds(AuthPollIntervalSeconds);
        }

        if (AuthManager.Instance == null)
        {
            Debug.LogError("[UgsDbStore] AuthManager became null while waiting.");
            tcs.TrySetResult(false);
            yield break;
        }

        if (!AuthManager.Instance.IsLoggedIn)
        {
            Debug.LogError("[UgsDbStore] AuthManager login timeout.");
            tcs.TrySetResult(false);
            yield break;
        }

        Debug.Log("[UgsDbStore] AuthManager login confirmed: " + AuthManager.Instance.UserId);

        // PlayerPrefs access should be on main thread (coroutine is main thread)
        RestoreOfflineQueueFromPrefs();

        tcs.TrySetResult(true);
    }

    public Task UpdateFieldAsync(string userId, string field, object value)
    {
        OnError?.Invoke("[UgsDbStore] UpdateFieldAsync not supported. Use DbStoreManager.SaveDataAsync<T>().");
        return Task.CompletedTask;
    }

    public Task UpdateInventoryItemAsync(string userId, string field, string itemKey, int quantity)
    {
        OnError?.Invoke("[UgsDbStore] UpdateInventoryItemAsync not supported. Use DbStoreManager.SaveDataAsync<InventoryData>().");
        return Task.CompletedTask;
    }

    public Task IncrementInventoryItemAsync(string userId, string field, string itemKey, int incrementValue)
    {
        OnError?.Invoke("[UgsDbStore] IncrementInventoryItemAsync not supported. Use DbStoreManager.SaveDataAsync<InventoryData>().");
        return Task.CompletedTask;
    }

    private async Task<T> LoadJsonAsAsync<T>(string userId, string key) where T : new()
    {
        var raw = await LoadRawAsync(userId, new HashSet<string> { key });
        raw.TryGetValue(key, out var json);

        if (string.IsNullOrEmpty(json))
        {
            return new T();
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            OnError?.Invoke($"[UgsDbStore] LoadJsonAsAsync parse failed. key={key}, error={e.Message}");
            return new T();
        }
    }

    public async Task<byte[]> LoadDataAsync(string key)
    {
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                return null;

            // UGS Cloud Save는 기본적으로 AuthenticationService.Instance.PlayerId를 기준으로 동작한다.
            // userId는 받긴 하지만 실제 Cloud Save 키에는 포함되지 않는다.
            var keys = new HashSet<string> { key };
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            if (result.TryGetValue(key, out var item))
            {
                return item.Value.GetAs<byte[]>();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UgsDbStore] LoadDataAsync error: {e}");
        }

        return null;
    }


    private async Task SaveAsync<T>(string userId, string key, T data)
    {
        string json = JsonUtility.ToJson(data);
        var dict = new Dictionary<string, string> { { key, json } };
        await SaveRawAsync(userId, dict);
    }

    public async Task<Dictionary<string, string>> LoadRawAsync(string userId, HashSet<string> keys)
    {
        if (keys == null || keys.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>(keys.Count);
        var keysToFetch = new HashSet<string>();

        var nowUtc = DateTime.UtcNow;

        foreach (var key in keys)
        {
            if (cacheByKey.TryGetValue(key, out var cached))
            {
                double ageSeconds = (nowUtc - cached.TimestampUtc).TotalSeconds;
                if (ageSeconds <= CacheTtlSeconds)
                {
                    result[key] = cached.Json;
                    continue;
                }
            }

            keysToFetch.Add(key);
        }

        if (keysToFetch.Count == 0)
        {
            return result;
        }

        try
        {
            var cloudResult = await CloudSaveService.Instance.Data.Player.LoadAsync(keysToFetch);

            foreach (var key in keysToFetch)
            {
                if (cloudResult.TryGetValue(key, out var item))
                {
                    string json = item.Value.GetAsString();
                    if (string.IsNullOrEmpty(json))
                    {
                        json = "{}";
                    }

                    result[key] = json;
                    cacheByKey[key] = new CachedItem
                    {
                        Json = json,
                        TimestampUtc = nowUtc
                    };
                }
                else
                {
                    result[key] = "{}";
                }
            }

            return result;
        }
        catch (Exception e)
        {
            OnError?.Invoke($"[UgsDbStore] LoadRawAsync failed: {e.Message}");

            foreach (var key in keysToFetch)
            {
                if (!result.ContainsKey(key))
                {
                    result[key] = "{}";
                }
            }

            return result;
        }
    }

    public async Task SaveRawAsync(string userId, Dictionary<string, string> keyValuePairs)
    {
        Debug.Log("[UgsDbStore] SaveRawAsync ENTER"
            + ", userId=" + (string.IsNullOrEmpty(userId) ? "NULL_OR_EMPTY" : userId)
            + ", kvpCount=" + (keyValuePairs == null ? -1 : keyValuePairs.Count)
            + ", isSignedIn=" + Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn);

        if (keyValuePairs == null)
        {
            Debug.LogWarning("[UgsDbStore] SaveRawAsync EARLY RETURN: keyValuePairs is null");
            return;
        }

        if (keyValuePairs.Count == 0)
        {
            Debug.LogWarning("[UgsDbStore] SaveRawAsync EARLY RETURN: keyValuePairs.Count == 0");
            return;
        }

        OnLoading?.Invoke(true, "Saving...");

        var payload = new Dictionary<string, object>(keyValuePairs.Count);
        var nowUtc = DateTime.UtcNow;

        foreach (var kvp in keyValuePairs)
        {
            string key = kvp.Key;
            string json = kvp.Value;

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[UgsDbStore] SaveRawAsync SKIP: empty key");
                continue;
            }

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[UgsDbStore] SaveRawAsync JSON EMPTY -> {}. key=" + key);
                json = "{}";
            }

            payload[key] = json;

            cacheByKey[key] = new CachedItem
            {
                Json = json,
                TimestampUtc = nowUtc
            };
        }

        Debug.Log("[UgsDbStore] SaveRawAsync payloadCount=" + payload.Count
            + ", keys=" + string.Join(", ", payload.Keys));

        if (payload.Count == 0)
        {
            Debug.LogWarning("[UgsDbStore] SaveRawAsync EARLY RETURN: payload.Count == 0 (all keys invalid)");
            OnLoading?.Invoke(false, "Saved");
            return;
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            Debug.Log("[UgsDbStore] SaveRawAsync TRY attempt=" + (attempt + 1) + "/" + MaxRetries);

            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(payload);

                Debug.Log("[UgsDbStore] SaveRawAsync SUCCESS. Keys: " + string.Join(", ", payload.Keys));
                OnLoading?.Invoke(false, "Saved");
                return;
            }
            catch (Exception e) when (attempt < MaxRetries - 1 && IsRetryable(e))
            {
                float delaySeconds = BaseRetryDelaySeconds * Mathf.Pow(2f, attempt);

                Debug.LogWarning("[UgsDbStore] SaveRawAsync RETRYABLE FAIL attempt="
                    + (attempt + 1) + "/" + MaxRetries
                    + ", delaySeconds=" + delaySeconds
                    + ", error=" + e);

                await Task.Delay((int)(delaySeconds * 1000f));
            }
            catch (Exception e)
            {
                Debug.LogError("[UgsDbStore] SaveRawAsync FAIL attempt="
                    + (attempt + 1) + "/" + MaxRetries
                    + ", error=" + e);
                break;
            }
        }

        Debug.LogWarning("[UgsDbStore] SaveRawAsync FALLBACK: enqueue offline");
        EnqueueOfflineSave(keyValuePairs);
        OnLoading?.Invoke(false, "Offline queued");
    }


    public void ProcessOfflineQueue()
    {
        if (isProcessingQueue)
        {
            return;
        }

        if (offlineQueue.Count == 0)
        {
            return;
        }

        _ = ProcessOfflineQueueInternalAsync();
    }

    private async Task ProcessOfflineQueueInternalAsync()
    {
        isProcessingQueue = true;

        try
        {
            while (offlineQueue.Count > 0)
            {
                var pending = offlineQueue.Peek();

                if (pending.QueuedAtUtc < DateTime.UtcNow.AddHours(-24))
                {
                    offlineQueue.Dequeue();
                    continue;
                }

                try
                {
                    await SaveRawAsync(AuthManager.Instance.UserId, pending.KeyValuePairs);
                    offlineQueue.Dequeue();
                }
                catch
                {
                    break;
                }
            }

            PersistOfflineQueueToPrefs();
        }
        finally
        {
            isProcessingQueue = false;
        }
    }

    public void ClearExpiredCache()
    {
        var nowUtc = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in cacheByKey)
        {
            double ageSeconds = (nowUtc - kvp.Value.TimestampUtc).TotalSeconds;
            if (ageSeconds > CacheTtlSeconds)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            cacheByKey.Remove(key);
        }
    }

    private bool IsRetryable(Exception e)
    {
        string msg = e.Message ?? string.Empty;
        msg = msg.ToLowerInvariant();

        return msg.Contains("network") || msg.Contains("timeout") || msg.Contains("unavailable");
    }

    private void EnqueueOfflineSave(Dictionary<string, string> keyValuePairs)
    {
        offlineQueue.Enqueue(new PendingSave
        {
            KeyValuePairs = new Dictionary<string, string>(keyValuePairs),
            QueuedAtUtc = DateTime.UtcNow
        });

        PersistOfflineQueueToPrefs();
    }

    private void PersistOfflineQueueToPrefs()
    {
        var wrapper = new OfflineQueueWrapper();

        foreach (var queued in offlineQueue)
        {
            var item = new OfflineQueueItem();
            item.QueuedAtUtcTicks = queued.QueuedAtUtc.Ticks;

            foreach (var kvp in queued.KeyValuePairs)
            {
                item.KeyValuePairs.Add(new SerializableKvp
                {
                    Key = kvp.Key,
                    Value = kvp.Value
                });
            }

            wrapper.Items.Add(item);
        }

        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString("UgsDbStore_OfflineQueue", json);
        PlayerPrefs.Save();
    }


    private void RestoreOfflineQueueFromPrefs()
    {
        if (!PlayerPrefs.HasKey("UgsDbStore_OfflineQueue"))
        {
            return;
        }

        string json = PlayerPrefs.GetString("UgsDbStore_OfflineQueue");
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        var wrapper = JsonUtility.FromJson<OfflineQueueWrapper>(json);
        if (wrapper == null || wrapper.Items == null)
        {
            return;
        }

        offlineQueue.Clear();

        foreach (var item in wrapper.Items)
        {
            var dict = new Dictionary<string, string>();
            if (item.KeyValuePairs != null)
            {
                foreach (var kvp in item.KeyValuePairs)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        dict[kvp.Key] = string.IsNullOrEmpty(kvp.Value) ? "{}" : kvp.Value;
                    }
                }
            }

            offlineQueue.Enqueue(new PendingSave
            {
                KeyValuePairs = dict,
                QueuedAtUtc = new DateTime(item.QueuedAtUtcTicks, DateTimeKind.Utc)
            });
        }
    }

    [Serializable]
    private class OfflineQueueWrapper
    {
        public List<OfflineQueueItem> Items = new List<OfflineQueueItem>();
    }

    [Serializable]
    private class OfflineQueueItem
    {
        public long QueuedAtUtcTicks;
        public List<SerializableKvp> KeyValuePairs = new List<SerializableKvp>();
    }

    [Serializable]
    private class SerializableKvp
    {
        public string Key;
        public string Value;
    }
}