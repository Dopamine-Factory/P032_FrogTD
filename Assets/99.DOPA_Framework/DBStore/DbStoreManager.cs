using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public class DbStoreManager : BaseSystemManager<DbStoreManager>
{
    [Header("DB Provider 설정")]
    [SerializeField] private MainProviderType providerType = MainProviderType.Ugs;

    [Header("Data Keys")]
    [SerializeField] private DataKeysConfig dataKeysConfig; // ScriptableObject 연결

    [Header("참조 매니저들")]
    [SerializeField] private FirebaseManager firebaseManager;
    [SerializeField] private UgsManager ugsManager;

    private IDbStore activeStore;

    private static readonly Dictionary<string, object> _globalCache = new();
    private static readonly Dictionary<string, DateTime> _cacheTimestamps = new();
    private const int CacheTTLSeconds = 300; // 5분

    public bool HasActiveStore => activeStore != null;

    public override void Initialize()
    {
        Debug.Log("[DbStoreManager] Initialize called. Dependencies ready: " + AreDependenciesInitialized);

        base.Initialize();
    }

    public override void ExecutePostInitialization()
    {
        if (!AreDependenciesInitialized)
        {
            Debug.LogWarning("[DbStoreManager] Dependencies not ready. Retrying later...");
            return;
        }

        base.ExecutePostInitialization();

        StartCoroutine(InitializeFlow(null));
    }

    public IEnumerator InitializeFlow(Action<bool> onComplete)
    {
        Debug.Log("[DbStoreManager] Initialize start");

        if (!IsEnabled)
        {
            Debug.Log("[DbStoreManager] Disabled by IsEnabled flag");
            activeStore = null;
            onComplete?.Invoke(true);
            yield break;
        }

        yield return SetupActiveStoreCoroutine(onComplete);
    }

    private IEnumerator SetupActiveStoreCoroutine(Action<bool> onComplete)
    {
        switch (providerType)
        {
            case MainProviderType.Firebase:
                if (firebaseManager == null || !firebaseManager.IsEnabled || !firebaseManager.IsInitialized)
                {
                    Debug.LogWarning("[DbStoreManager] Firebase selected but FirebaseManager not ready.");
                    activeStore = null;
                    onComplete?.Invoke(false);
                    yield break;
                }
                activeStore = new FirebaseDbStore(firebaseManager);
                break;

            case MainProviderType.Ugs:
                if (ugsManager == null || !ugsManager.IsEnabled || !ugsManager.IsInitialized)
                {
                    Debug.LogWarning("[DbStoreManager] UGS selected but UgsManager not ready.");
                    activeStore = null;
                    onComplete?.Invoke(false);
                    yield break;
                }
                activeStore = new UgsDbStore(ugsManager);
                break;

            default:
                Debug.LogWarning("[DbStoreManager] DbProviderType.None. DB disabled.");
                activeStore = null;
                onComplete?.Invoke(true);
                yield break;
        }

        if (activeStore == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        bool isDone = false;
        bool result = false;

        Task<bool> initTask = activeStore.InitializeAsync();
        initTask.ContinueWith(t =>
        {
            result = !t.IsFaulted && !t.IsCanceled && t.Result;
            isDone = true;
        });

        while (!isDone) yield return null;

        onComplete?.Invoke(result);
        Debug.Log($"[DbStoreManager] Initialize done. Success={result}, Provider={providerType}");
    }

    #region 제너릭 API

    public async Task LoadAllInitialDataAsync(string userId)
    {
        var keys = dataKeysConfig.GetInitialKeySet();  // IsRequired=true 키만! 스크립터블 오브젝트 세팅 후 사용
        var rawDict = await activeStore.LoadRawAsync(userId, keys);

        int cachedCount = 0;
        foreach (var kvp in rawDict)
        {
            if (kvp.Value is string json && !string.IsNullOrEmpty(json))
            {
                _globalCache[kvp.Key] = json;
                _cacheTimestamps[kvp.Key] = DateTime.UtcNow;
                cachedCount++;
            }
        }

        foreach (var requiredKey in dataKeysConfig.GetInitialKeySet())
        {
            if (!_globalCache.ContainsKey(requiredKey) ||
                string.IsNullOrEmpty(_globalCache[requiredKey] as string))
            {
                Debug.LogError($"[DataLoader] REQUIRED '{requiredKey}' failed to load!");
            }
        }

        Debug.Log($"[DataLoader] Initial load: {cachedCount}/{keys.Count} success");
    }


    public async Task<byte[]> LoadDataAsync(string key)
    {
        if (activeStore == null)
        {
            Debug.LogWarning("[DbStoreManager] LoadPlayerDataAsync called but no active DB store.");
            return null;
        }

        return await activeStore.LoadDataAsync(key);
    }

    public async Task<T> LoadDataAsync<T>(string userId, bool forceRefresh = false) where T : new()
    {
        var key = GetDataKey<T>();

        if (!forceRefresh)
        {
            if (_globalCache.TryGetValue(key, out var cachedJson) &&
                (DateTime.UtcNow - _cacheTimestamps[key]).TotalSeconds < CacheTTLSeconds)
            {
                return JsonUtility.FromJson<T>((string)cachedJson);
            }
        }

        var rawDict = await activeStore.LoadRawAsync(userId, new HashSet<string> { key });
        if (rawDict.TryGetValue(key, out var json) && !string.IsNullOrEmpty(json))
        {
            _globalCache[key] = json;
            _cacheTimestamps[key] = DateTime.UtcNow;
            return JsonUtility.FromJson<T>(json);
        }

        var empty = new T();
        var emptyJson = JsonUtility.ToJson(empty);
        _globalCache[key] = emptyJson;
        _cacheTimestamps[key] = DateTime.UtcNow;

        return empty;
    }

    public async Task SaveDataAsync<T>(string userId, T data)
    {
        Debug.Log($"[DbStoreManager] SaveDataAsync ENTER"
    + $", userId={(string.IsNullOrEmpty(userId) ? "NULL_OR_EMPTY" : userId)}"
    + $", type={typeof(T).Name}"
    + $", activeStore={(activeStore != null ? activeStore.GetType().Name : "NULL")}"
    + $", dataJson={JsonUtility.ToJson(data).Substring(0, Math.Min(100, JsonUtility.ToJson(data).Length))}");

        if (activeStore == null)
        {
            Debug.LogError("[DbStoreManager] SaveDataAsync ABORT: activeStore is null");
            return;
        }

        var key = GetDataKey<T>();
        var json = JsonUtility.ToJson(data);
        Debug.Log($"[DbStoreManager] SaveDataAsync CALLING STORE. key={key}, userId={userId}");

        var dict = new Dictionary<string, string> { { key, json } };
        await activeStore.SaveRawAsync(userId, dict);

        Debug.Log("[DbStoreManager] SaveDataAsync STORE CALL COMPLETED");
        _globalCache[key] = json;
        _cacheTimestamps[key] = DateTime.UtcNow;
    }

    private string GetDataKey<T>()
    {
        var attr = typeof(T).GetCustomAttribute<DataKeyAttribute>();
        return attr?.Key ?? typeof(T).Name.ToLower();
    }

    #endregion
}