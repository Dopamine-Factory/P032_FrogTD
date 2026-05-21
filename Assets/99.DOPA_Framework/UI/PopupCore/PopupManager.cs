using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PopupManager : MonoBehaviour
{
    #region Singleton
    public static PopupManager Instance { get; private set; }

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
    [System.Serializable]
    public class CanvasPool
    {
        [Header("Canvas")]
        public Canvas canvas;
        public PopupPriority priority = PopupPriority.Default;
        [Range(1, 30)] public int maxPopups = 30;

        // [Header("Sorting")]
        // [Range(0, 5000)] public int baseSortingOrder = 1000;

        public bool ignoreRaycast;
    }

    [System.Serializable]
    public class PrewarmConfig
    {
        public string addressKey;
        public PopupPriority priority = PopupPriority.Default;
        [Range(1, 3)] public int prewarmCount = 1;
    }

    public enum PopupPriority
    {
        Background, Default, Notice, Main, System
    }

    [Header("Canvas Pools")]
    [SerializeField] private List<CanvasPool> canvasPools = new List<CanvasPool>();

    [Header("Prewarm Popups")]
    [SerializeField] private PrewarmConfig[] prewarmConfigs = new PrewarmConfig[0];

    [Header("Global")]
    [SerializeField] private int maxActivePopups = 1000;
    [SerializeField] private float warmCacheTimeout = 7200f;
    #endregion

    #region Fields
    private Dictionary<PopupPriority, CanvasPool> canvasPoolDict = new();
    private Dictionary<string, HybridPopupPool> hybridPools = new();
    private readonly List<PopupBase> activePopups = new List<PopupBase>();
    private Coroutine cleanupCoroutine;
    #endregion

    #region Properties
    public bool IsReady { get; private set; }
    public int ActiveCount => activePopups.Count;
    public int PrewarmCount => hybridPools.Values.Sum(p => p.prewarmPool?.Count ?? 0);
    public int WarmCacheCount => hybridPools.Values.Count(p => p.warmCache != null);
    #endregion

    #region Initialization
    private void Initialize()
    {
        Debug.Log("[PopupManager] Initialize Start");

        BuildCanvasPools();
        PrewarmSystemPopups();
        IsReady = true;
    }

    private void BuildCanvasPools()
    {
        canvasPools.RemoveAll(pool => pool.canvas == null);

        canvasPoolDict.Clear();
        foreach (var pool in canvasPools)
        {
            // pool.canvas.sortingOrder = pool.baseSortingOrder;
            pool.canvas.sortingOrder = GetDefaultSortingOrder(pool.priority);

            pool.canvas.overrideSorting = true;
            canvasPoolDict[pool.priority] = pool;
        }
    }


    private void PrewarmSystemPopups()
    {
        foreach (var config in prewarmConfigs)
        {
            StartCoroutine(PrewarmPopup(config));
        }
    }

    public void CleanupAndRefreshCanvasPools()
    {
        canvasPools.RemoveAll(pool => pool.canvas == null);

        canvasPoolDict.Clear();
        foreach (var pool in canvasPools)
        {
            if (pool.canvas != null)
                canvasPoolDict[pool.priority] = pool;
        }

        foreach (var pool in canvasPools)
        {
            if (pool.canvas != null)
            {
                // pool.canvas.sortingOrder = pool.baseSortingOrder;
                pool.canvas.sortingOrder = GetDefaultSortingOrder(pool.priority);

                pool.canvas.overrideSorting = true;
            }
        }
    }

    public void RegisterCanvasPools(List<CanvasPool> pools)
    {
        int addedCount = 0;
        foreach (var newPool in pools)
        {
            if (newPool.canvas == null)
            {
                Debug.LogWarning("PopupManager: RegisterCanvasPools - Canvas is null.");
                continue;
            }

            if (canvasPoolDict.TryGetValue(newPool.priority, out CanvasPool existingPool))
            {
                if (existingPool != null && existingPool.canvas != null)
                {
                    Debug.LogError($"[PopupManager]: Priority '{newPool.priority}' already exists. Existing: '{existingPool.canvas.name}'.");
                    continue;
                }
                else
                {
                    canvasPoolDict.Remove(newPool.priority);
                    canvasPools.RemoveAll(p => p == existingPool);
                    Debug.Log($"PopupManager: Removed invalid pool for priority {newPool.priority}.");
                }
            }

            if (canvasPools.Any(p => p.canvas == newPool.canvas))
            {
                Debug.Log($"PopupManager: Canvas '{newPool.canvas.name}' already registered.");
                continue;
            }

            canvasPools.Add(newPool);

            canvasPoolDict[newPool.priority] = newPool;

            // newPool.canvas.sortingOrder = newPool.baseSortingOrder;
            newPool.canvas.sortingOrder = GetDefaultSortingOrder(newPool.priority);
            newPool.canvas.overrideSorting = true;

            addedCount++;
            Debug.Log($"PopupManager: Canvas '{newPool.canvas.name}' registered. Priority: {newPool.priority}");
        }

        Debug.Log($"[PopupManager] RegisterCanvasPools: +{addedCount} pools. Total: {canvasPools.Count}");
    }

    private PopupPriority GetPriorityFromCanvas(Canvas canvas)
    {
        string name = canvas.name.ToLower();
        if (name.Contains("system") || name.Contains("update")) return PopupPriority.System;
        if (name.Contains("main") || name.Contains("home")) return PopupPriority.Main;
        if (name.Contains("notice") || name.Contains("reward") || name.Contains("result")) return PopupPriority.Notice;
        if (name.Contains("background") || name.Contains("loading")) return PopupPriority.Background;
        return PopupPriority.Default;
    }


    private int GetDefaultSortingOrder(PopupPriority priority)
    {
        return priority switch
        {
            PopupPriority.Background => 5, // 현재는 사용처 없음
            PopupPriority.Default => 10, // 인 게임 기본 - 아이템 획득 등
            PopupPriority.Notice => 20, // 인 게임 최상위 - 게임 결과 등 
            PopupPriority.Main => 30, // 메인 레이어 팝업 - 로비 이벤트, 랭킹 등
            PopupPriority.System => 50, // 강제 업데이트 팝업 등 최상단 시스템 부류
            _ => 10
        };
    }

    private void OnValidate()
    {
        foreach (var pool in canvasPools)
        {
            if (pool.canvas != null)
            {
                pool.canvas.sortingOrder = GetDefaultSortingOrder(pool.priority);
                pool.canvas.overrideSorting = true;
            }
        }
    }

    #endregion

    #region Public API

    public void ShowPopup<TPopup>(string title = null, string content = null,
                            Action<TPopup> onCreated = null,
                            PopupPriority? priority = null, bool forceNew = false)
        where TPopup : PopupBase
    {
        string addressKey = AddressablesAssetManager.GetAddress(AddressablesAssetManager.AssetCategory.Popup, typeof(TPopup).Name);

        var existing = activePopups.FirstOrDefault(p => p.AddressKey == addressKey && p.IsOpened && p.TargetCanvas != null && p.TargetCanvas.gameObject.activeInHierarchy);
        if (existing != null && !forceNew)
        {
            Debug.Log($"[PopupManager] [{addressKey}] is Activ Popup, (forceNew={forceNew})");
            existing.transform.SetAsLastSibling();
            return;
        }

        ShowPopupGeneric<TPopup>(addressKey, title, content, onCreated, priority);
    }

    public void ClosePopupByKey(string addressKey)
    {
        Debug.Log($"[PopupManager] ClosePopupByKey [{addressKey}] Start");

        if (hybridPools.TryGetValue(addressKey, out var pool))
        {
            var targetPopup = activePopups.FirstOrDefault(p => p.AddressKey == addressKey);
            if (targetPopup != null)
            {
                targetPopup.Close();
                Debug.Log($"[PopupManager] {addressKey} ClosePopupByKey!");
            }
        }
    }

    public void ClosePopup<TPopup>()
        where TPopup : PopupBase
    {
        string addressKey = AddressablesAssetManager.GetAddress(
            AddressablesAssetManager.AssetCategory.Popup,
            typeof(TPopup).Name
        );
        ClosePopupByKey(addressKey);
    }

    public void CloseTop()
    {
        if (activePopups.Count <= 0)
        {
            return;
        }

        int lastIndex = activePopups.Count - 1;
        PopupBase topPopup = activePopups[lastIndex];

        if (topPopup == null)
        {
            activePopups.RemoveAt(lastIndex);
            return;
        }

        topPopup.Close();
    }

    public void CloseAll()
    {
        if (activePopups.Count <= 0)
        {
            return;
        }

        PopupBase[] snapshot = activePopups.ToArray();
        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            PopupBase popup = snapshot[i];
            if (popup == null)
            {
                continue;
            }

            popup.Close();
        }
    }

    public Action OnPopupClosedCallback;
    private void HandlePopupClosed(PopupBase popup)
    {
        if (popup == null)
        {
            return;
        }

        popup.OnClosed -= HandlePopupClosed;

        activePopups.Remove(popup);

        if (hybridPools.TryGetValue(popup.AddressKey, out var pool))
        {
            bool hasAnyActive = activePopups.Any(p => p != null && p.AddressKey == popup.AddressKey);
            pool.hasActiveInstance = hasAnyActive;
            pool.lastActivityTime = Time.time;
        }

        OnPopupClosedCallback?.Invoke();
    }

    #endregion

    #region Generic Overload
    private void ShowPopupGeneric<TPopup>(string addressKey, string title, string content,
                                        Action<TPopup> onCreated, PopupPriority? priority)
        where TPopup : PopupBase
    {
        if (!IsReady || ActiveCount >= maxActivePopups)
        {
            Debug.LogWarning($"[PopupManager] ShowPopupGeneric {maxActivePopups} Limit Over Fail");
            return;
        }

        StartCoroutine(LoadAndShowPopupGeneric<TPopup>(addressKey, title, content, onCreated, priority));
    }
    #endregion

    #region Core Loading Pipeline
    private IEnumerator LoadAndShowPopup(string addressKey, string title, string content,
                                       Action<PopupBase> onCreated, PopupPriority? priorityOverride)
    {
        var pool = GetOrCreateHybridPool(addressKey);
        PopupBase popup = AcquirePopup(pool);

        var existingActive = activePopups.FirstOrDefault(p =>
            p.AddressKey == addressKey &&
            p.IsOpened &&
            p.TargetCanvas != null &&
            p.TargetCanvas.gameObject.activeInHierarchy);

        if (existingActive != null)
        {
            Debug.Log($"[PopupManager] LoadAndShowPopup [{addressKey}] reuse existing");
            existingActive.transform.SetAsLastSibling();
            yield break;
        }

        if (popup == null)
        {
            yield return StartCoroutine(CreateNewPopupCoroutine(pool, addressKey));
            popup = pool.warmCache;
            if (popup == null)
            {
                Debug.LogError($"[PopupManager] LoadAndShowPopup {addressKey} Fail!");
                yield break;
            }
        }

        var priority = priorityOverride ?? PopupPriority.Notice;
        var canvasPool = GetCanvasPool(priority);

        popup.InitializeForAddressables(addressKey, pool.warmHandle, true);
        popup.AssignCanvas(canvasPool.canvas);
        popup.RegisterToSafeArea(GetComponent<EnhancedSafeArea>());


        onCreated?.Invoke(popup);

        popup.OnClosed -= HandlePopupClosed;
        popup.OnClosed += HandlePopupClosed;

        activePopups.Add(popup);
        popup.Open(title, content);
    }

    private IEnumerator LoadAndShowPopupGeneric<TPopup>(string addressKey, string title, string content,
                                                      Action<TPopup> onCreated, PopupPriority? priorityOverride)
        where TPopup : PopupBase
    {
        yield return LoadAndShowPopup(addressKey, title, content, popup =>
        {
            if (popup is TPopup typedPopup)
                onCreated?.Invoke(typedPopup);
        }, priorityOverride);
    }
    #endregion

    #region Hybrid Pooling System
    [System.Serializable]
    private class HybridPopupPool
    {
        public string addressKey;
        public Queue<PopupBase> prewarmPool = new();
        public PopupBase warmCache;
        public bool hasActiveInstance;
        public float lastActivityTime;
        public AsyncOperationHandle<GameObject> warmHandle;
    }

    private HybridPopupPool GetOrCreateHybridPool(string addressKey)
    {
        if (!hybridPools.TryGetValue(addressKey, out var pool))
        {
            pool = new HybridPopupPool { addressKey = addressKey };
            hybridPools[addressKey] = pool;
        }

        return pool;
    }

    private PopupBase AcquirePopup(HybridPopupPool pool)
    {
        var existing = activePopups.FirstOrDefault(p =>
            p.AddressKey == pool.addressKey && p.IsOpened && p.TargetCanvas != null && p.TargetCanvas.gameObject.activeInHierarchy);
        if (existing != null)
        {
            existing.transform.SetAsLastSibling();
            return null;
        }

        if (pool.prewarmPool.Count > 0) return pool.prewarmPool.Dequeue();
        if (pool.warmCache != null && !pool.warmCache.IsOpened) return pool.warmCache;
        return null;
    }

    #endregion

    #region Prewarm & Creation
    private IEnumerator PrewarmPopup(PrewarmConfig config)
    {
        var pool = GetOrCreateHybridPool(config.addressKey);

        for (int i = 0; i < config.prewarmCount; i++)
        {
            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(config.addressKey);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                var popup = handle.Result.GetComponent<PopupBase>();
                if (popup != null)
                {
                    popup.InitializeForAddressables(config.addressKey, handle, true);
                    popup.gameObject.SetActive(false);
                    pool.prewarmPool.Enqueue(popup);
                }
            }
        }

        Debug.Log($"[PopupManager] PrewarmPopup Success [{config.addressKey}] {config.prewarmCount}개");
    }

    private IEnumerator CreateNewPopupCoroutine(HybridPopupPool pool, string addressKey)
    {
        Debug.Log($"[PopupManager] CreateNewPopup {addressKey}");

        var handle = Addressables.InstantiateAsync(addressKey);

        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            var popup = handle.Result.GetComponent<PopupBase>();
            if (popup != null)
            {
                popup.InitializeForAddressables(addressKey, handle, false);
                pool.warmCache = popup;
                pool.warmHandle = handle;
                Debug.Log($"[PopupManager] {addressKey} CreateNewPopup Success!");
                yield break;
            }
        }

        Debug.LogError($"[PopupManager] {addressKey} CreateNewPopup Fail!");
    }

    #endregion

    #region Canvas Management
    private CanvasPool GetCanvasPool(PopupPriority priority)
    {
        if (canvasPoolDict.TryGetValue(priority, out var pool))
            return pool;
        if (canvasPoolDict.TryGetValue(PopupPriority.Default, out var normal))
            return normal;
        return canvasPools.FirstOrDefault();
    }
    #endregion

    #region Memory Management
    private IEnumerator CleanupWarmCache()
    {
        while (true)
        {
            yield return new WaitForSeconds(warmCacheTimeout);

            foreach (var kvp in hybridPools.ToArray())
            {
                var pool = kvp.Value;
                if (pool.warmCache != null &&
                    !pool.hasActiveInstance &&
                    Time.time - pool.lastActivityTime > warmCacheTimeout &&
                    pool.warmHandle.IsValid())
                {
                    Addressables.ReleaseInstance(pool.warmHandle);
                    pool.warmCache = null;
                    pool.warmHandle = default;
                    Debug.Log($"[PopupManager] CleanupWarmCache Warm [{kvp.Key}]");
                }
            }
        }
    }
    #endregion

    #region Lifecycle
    private void OnDestroy()
    {
        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
        }
    }
    #endregion

    #region Debug
    [ContextMenu("Stats")]
    private void PrintStats()
    {
        Debug.Log($"[PopupManager] :\n" +
                 $"Active: {ActiveCount}\n" +
                 $"Prewarm: {PrewarmCount}\n" +
                 $"WarmCache: {WarmCacheCount}\n" +
                 $"Pools: {hybridPools.Count}");
    }


    public bool IsShowingPopup<TPopup>() where TPopup : PopupBase
    {
        foreach (var a in activePopups)
        {
            if (a is TPopup) return true;
        }
        return false;
    }


    public TPopup GetActivePopup<TPopup>() where TPopup : PopupBase
    {
        foreach (var a in activePopups)
        {
            if (a is TPopup popup) return popup;
        }
        return null;
    }
    #endregion
}