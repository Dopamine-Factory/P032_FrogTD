using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Linq;
using UnityEngine.UI;

public class AddressablesAssetManager : MonoBehaviour
{
    #region Singleton
    public static AddressablesAssetManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Configuration
    [Header("Core Settings")]
    [SerializeField] private int maxCacheSize = 100;
    [SerializeField] private float cacheTimeoutSeconds = 300f;
    [SerializeField] private bool useResourcesFallback = true;
    
    [Header("Retry & Fallback")]
    [SerializeField] private int maxRetryCount = 3;
    [SerializeField] private float baseRetryDelay = 0.5f;
    
    [Header("Prewarming")]
    [SerializeField] private PrewarmConfig[] prewarmAssets;
    #endregion

    #region Data Structures
    [System.Serializable]
    public class PrewarmConfig
    {
        public AssetCategory category;
        public string[] keys;
        public bool keepInMemory;
    }

    public enum AssetCategory
    {
        Popup, AudioClip, Prefab, Sprite, Texture
    }

    [System.Serializable]
    public class CachedAsset
    {
        public UnityEngine.Object asset;
        public AsyncOperationHandle handle;
        public int refCount;
        public float lastUsedTime;
        public bool keepInMemory;
        [NonSerialized] public HashSet<Action<UnityEngine.Object>> waitingCallbacks = new();
    }

    [System.Serializable]
    public class CacheStats
    {
        public int totalLoads, cacheHits, cacheMisses, loadFailures;
        public float hitRate => totalLoads > 0 ? (float)cacheHits / totalLoads : 0f;
        [NonSerialized] public long totalMemoryBytes;
    }
    #endregion

    #region Fields
    private Dictionary<string, CachedAsset> cache = new();
    private LinkedList<string> lruList = new();
    private HashSet<string> lruSet = new();
    private Dictionary<string, UnityEngine.Object> resourcesCache = new();
    [SerializeField] private Canvas popupCanvas;
    private bool isReady = false;
    public CacheStats Stats { get; private set; } = new CacheStats();
    #endregion

    #region Properties
    public bool IsReady => isReady;
    public int CacheCount => Cache.Count;
    public string CacheStat => $"{Cache.Count}/{maxCacheSize} (Hit:{Stats.hitRate:P0})";

    public Dictionary<string, CachedAsset> Cache { get => cache; set => cache = value; }
    #endregion

    private void Initialize()
    {
        CreatePopupCanvas();
        StartCoroutine(PrewarmCoroutine());
        InvokeRepeating(nameof(CleanupCache), cacheTimeoutSeconds, cacheTimeoutSeconds);
        isReady = true;
    }

    private void CreatePopupCanvas()
    {
        // popupCanvas = FindObjectOfType<Canvas>();
        if (popupCanvas == null)
        {
            var go = new GameObject("AddressablesCanvas");
            popupCanvas = go.AddComponent<Canvas>();
            popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            popupCanvas.sortingOrder = 1000;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(go);
        }
    }

    #region
    // public static string GetAddress(AssetCategory category, string key) => $"{category}/{key}";
    public static string GetAddress(AssetCategory category, string key) => $"{key}";
    #endregion

    #region
    public void LoadAssetAsync<T>(AssetCategory category, string key, Action<T> onComplete) 
        where T : UnityEngine.Object
    {
        var address = GetAddress(category, key);
        StartCoroutine(LoadAssetCoroutine(address, onComplete));
    }

    public void InstantiatePopupAsync<T>(string popupKey, Action<T> onComplete) where T : PopupBase
    {
        var address = GetAddress(AssetCategory.Popup, popupKey);
        StartCoroutine(InstantiatePopupCoroutine(address, onComplete));
    }
    #endregion

    #region
    private IEnumerator LoadAssetCoroutine<T>(string address, Action<T> onComplete) 
        where T : UnityEngine.Object
    {
        Stats.totalLoads++;

        if (TryGetFromCache(address, out T cachedAsset))
        {
            Stats.cacheHits++;
            onComplete?.Invoke(cachedAsset);
            yield break;
        }

        Stats.cacheMisses++;

        if (Cache.TryGetValue(address, out var waiting) && waiting.waitingCallbacks.Count > 0)
        {
            waiting.waitingCallbacks.Add(obj => onComplete?.Invoke((T)obj));
            yield break;
        }

        AudioClip clip = null;
        for (int retry = 0; retry < maxRetryCount; retry++)
        {
            var handle = Addressables.LoadAssetAsync<T>(address);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                clip = (AudioClip)(UnityEngine.Object)handle.Result;
                AddToCache(address, handle.Result, handle);
                Debug.Log($"[Loaded] {address} (Retry:{retry})");
                onComplete?.Invoke(handle.Result);
                yield break;
            }

            if (retry < maxRetryCount - 1)
                yield return new WaitForSeconds(baseRetryDelay * Mathf.Pow(2f, retry));
        }

        if (useResourcesFallback)
        {
            var resourcesPath = ExtractResourcesPath(address);
            var fallback = Resources.Load<T>(resourcesPath);
            
            if (fallback != null)
            {
                resourcesCache[address] = fallback;
                Debug.LogWarning($"[Fallback] {address} → Resources");
                Stats.cacheHits++;
                onComplete?.Invoke(fallback);
                yield break;
            }
        }

        Stats.loadFailures++;
        Debug.LogError($"[LoadFailed] {address} after {maxRetryCount} retries");
    }

    private IEnumerator InstantiatePopupCoroutine<T>(string address, Action<T> onComplete) where T : PopupBase
    {
        var instantiateHandle = Addressables.InstantiateAsync(address, popupCanvas.transform, false);
        yield return instantiateHandle;

        if (instantiateHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var popup = instantiateHandle.Result.GetComponent<T>();
            if (popup != null)
            {
                AddToCache(address, instantiateHandle.Result, instantiateHandle);
                Debug.Log($"[PopupInstantiated] {address}");
                onComplete?.Invoke(popup);
            }
        }
    }
    #endregion

    #region
    private bool TryGetFromCache<T>(string address, out T asset) where T : UnityEngine.Object
    {
        asset = null;
        if (Cache.TryGetValue(address, out var cached) && cached.asset is T tAsset)
        {
            cached.refCount++;
            cached.lastUsedTime = Time.time;
            
            if (lruSet.Contains(address))
            {
                lruList.Remove(address);
                lruSet.Remove(address);
            }
            lruList.AddLast(address);
            lruSet.Add(address);
            
            asset = tAsset;
            return true;
        }
        return false;
    }

    private void AddToCache(string address, UnityEngine.Object asset, AsyncOperationHandle handle)
    {
        var cached = new CachedAsset
        {
            asset = asset,
            handle = handle,
            refCount = 1,
            lastUsedTime = Time.time
        };
        Cache[address] = cached;
        
        lruList.AddLast(address);
        lruSet.Add(address);

        while (Cache.Count > maxCacheSize)
            EvictLRU();
    }

    private void EvictLRU()
    {
        var node = lruList.First;
        while (node != null && lruList.Count > 0)
        {
            string key = node.Value;
            if (Cache.TryGetValue(key, out var cached) && cached.refCount <= 0)
            {
                lruList.RemoveFirst();
                lruSet.Remove(key);
                Addressables.Release(cached.handle);
                Cache.Remove(key);
                return;
            }
            node = node.Next;
        }
    }

    public void ReleaseAsset(string address)
    {
        if (Cache.TryGetValue(address, out var cached))
        {
            cached.refCount--;
            cached.waitingCallbacks.Clear();
            
            if (cached.refCount <= 0)
            {
                if (lruSet.Contains(address))
                {
                    lruList.Remove(address);
                    lruSet.Remove(address);
                }
                
                Addressables.Release(cached.handle);
                Cache.Remove(address);
            }
        }
        else if (resourcesCache.ContainsKey(address))
        {
            resourcesCache.Remove(address); // 🔥 Resources도 정리
        }
    }
    #endregion

    #region Cleanup
    private IEnumerator PrewarmCoroutine()
    {
        if (prewarmAssets == null) yield break;

        int success = 0, failed = 0;
        foreach (var config in prewarmAssets)
        {
            foreach (var key in config.keys)
            {
                var address = GetAddress(config.category, key);
                var handle = StartCoroutine(LoadAssetCoroutine<UnityEngine.Object>(address, null));
                yield return handle;
                if (Cache.ContainsKey(address)) success++;
                else failed++;
            }
        }
        Debug.Log($"[Prewarm] {success}/{prewarmAssets.Sum(c => c.keys.Length)} Success");
    }

    private void CleanupCache()
    {
        var expired = Cache.Where(kvp => kvp.Value.refCount <= 0 &&
                                       Time.time - kvp.Value.lastUsedTime > cacheTimeoutSeconds)
                          .ToArray();
        foreach (var kvp in expired)
            ReleaseAsset(kvp.Key);
    }

    private string ExtractResourcesPath(string address)
    {
        return address.Replace("Popup_", "Popups/")
                      .Replace("AudioClip_", "Audio/")
                      .Replace("Prefab_", "Prefabs/");
    }
    #endregion

    private void OnDestroy()
    {
        foreach (var cached in Cache.Values)
        {
            if (cached.handle.Status != AsyncOperationStatus.None)
                Addressables.Release(cached.handle);
        }
        Cache.Clear();
        resourcesCache.Clear();
    }

    #region Debug
    [ContextMenu("Print Stats")]
    public void PrintCacheStats()
    {
        Debug.Log($"[Addressables] {CacheStat} | " +
                 $"Hits:{Stats.cacheHits} Miss:{Stats.cacheMisses} Fail:{Stats.loadFailures} " +
                 $"Rate:{Stats.hitRate:P1}");
    }

    [ContextMenu("Clear All")]
    public void ClearCache()
    {
        foreach (var kvp in Cache.ToArray())
            ReleaseAsset(kvp.Key);
        Debug.Log("[Addressables] Cache cleared");
    }
    #endregion
}