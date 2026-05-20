using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameAnalyticsSDK;
using System.Reflection;

public class SDKManager : Singleton<SDKManager>, IBaseManager
{
    [SerializeField] private Transform scanRoot;
    [SerializeField] private List<string> systemDependencies = new();
    [SerializeField] private float initTimeout = 30f;

    [Header("Runtime Status")]
    [SerializeField] private List<BaseSDKManager> discoveredSDKs = new();
    [SerializeField] private bool allSDKsInitialized;

    [Header("Proxy Cache (Auto-populated)")]
    [SerializeField] private int proxyCount;

    private readonly Dictionary<string, MonoBehaviour> _proxies = new();
    private Coroutine _currentInitCoroutine;

    public IReadOnlyList<BaseSDKManager> DiscoveredSDKs => discoveredSDKs.AsReadOnly();
    public bool AllSDKsInitialized => allSDKsInitialized;


    public string Id => "SDKManager";
    public bool IsFullyInitialized => allSDKsInitialized;
    public bool IsEnabled => true;
    public event Action<string> OnFullyInitialized;

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();
        ManagerRegistry.RegisterManager(Id, this);
        if (scanRoot == null) scanRoot = transform;
        Debug.Log($"~#~ SDKManager '{Id}' registered to ManagerRegistry");
    }

    public void InitializeSDKs(Action<float> progressCallback, Action<bool> onComplete)
    {
        if (_currentInitCoroutine != null)
        {
            Debug.LogWarning("~#~ SDKManager: Already initializing");
            return;
        }
        _currentInitCoroutine = StartCoroutine(InitializeAllSDKs(progressCallback, onComplete));
    }

    private IEnumerator InitializeAllSDKs(Action<float> uiProgress, Action<bool> onComplete)
    {
        bool allSuccess = true;
        float overallProgress = 0f;

        try
        {
            // Phase 1: System Dependencies (10%)
            uiProgress?.Invoke(0f);
            yield return StartCoroutine(WaitForSystemDependencies());
            overallProgress = 0.1f;
            uiProgress?.Invoke(overallProgress);

            // Phase 2: Auto Scan & Sort (10%)
            ScanAndSortSDKs();
            overallProgress = 0.2f;
            uiProgress?.Invoke(overallProgress);

            // Phase 3: SDK Initialization (70%)
            bool sdkSuccess = false;
            yield return StartCoroutine(InitializeSDKHierarchy(0.2f, 0.9f, uiProgress, result => sdkSuccess = result));
            allSuccess = sdkSuccess;

            // Phase 4: Post-Initialization (20%)
            if (allSuccess)
            {
                PostSDKInitialization();
                overallProgress = 1f;
                uiProgress?.Invoke(1f);
                allSDKsInitialized = true;
            }
        }
        finally
        {
            _currentInitCoroutine = null;
            onComplete?.Invoke(allSuccess);
        }
    }

    private IEnumerator WaitForSystemDependencies()
    {
        foreach (var depId in systemDependencies)
        {
            var sysMgr = ManagerRegistry.GetManager(depId);
            if (sysMgr == null || !sysMgr.IsEnabled)
            {
                Debug.LogError($"~#~ SDKManager: System dep '{depId}' not available");
                continue;
            }

            float timeout = Time.time + initTimeout;
            while (!sysMgr.IsFullyInitialized && Time.time < timeout)
            {
                yield return null;
            }

            if (sysMgr.IsFullyInitialized)
                Debug.Log($"~#~ SDKManager: '{depId}' ready");
            else
                Debug.LogError($"~#~ SDKManager: '{depId}' timeout");
        }
    }

    private void ScanAndSortSDKs()
    {
        discoveredSDKs.Clear();
        var candidates = scanRoot.GetComponentsInChildren<BaseSDKManager>(true)
            .Where(sdk => sdk.IsEnabled && sdk.gameObject.activeInHierarchy)
            .ToList();

        discoveredSDKs = TopologicalSort(candidates);
        Debug.Log($"~#~ SDKManager: Scanned {discoveredSDKs.Count}/{candidates.Count} SDKs: " +
                 $"{string.Join(", ", discoveredSDKs.Select(s => s.name))}");
    }

    private List<BaseSDKManager> TopologicalSort(List<BaseSDKManager> sdkList)
    {
        var sorted = new List<BaseSDKManager>();
        var visiting = new HashSet<BaseSDKManager>();
        var visited = new HashSet<BaseSDKManager>();

        foreach (var sdk in sdkList)
        {
            if (!visited.Contains(sdk))
                TopoSortUtil(sdk, sorted, visiting, visited, sdkList);
        }
        return sorted;
    }

    private void TopoSortUtil(BaseSDKManager sdk, List<BaseSDKManager> sorted,
                            HashSet<BaseSDKManager> visiting, HashSet<BaseSDKManager> visited,
                            List<BaseSDKManager> allSDKs)
    {
        if (visiting.Contains(sdk))
        {
            Debug.LogError($"~#~ SDKManager: Dependency cycle at {sdk.name}");
            return;
        }
        if (visited.Contains(sdk)) return;

        visiting.Add(sdk);
        foreach (var depName in sdk.Dependencies)
        {
            var dep = allSDKs.FirstOrDefault(s => s.name == depName);
            if (dep != null) TopoSortUtil(dep, sorted, visiting, visited, allSDKs);
        }
        visiting.Remove(sdk);
        visited.Add(sdk);
        sorted.Add(sdk);
    }

    private IEnumerator InitializeSDKHierarchy(float startProgress, float endProgress, Action<float> uiProgress, Action<bool> onComplete)
    {
        bool allSuccess = true;

        int total = discoveredSDKs.Count;
        if (total <= 0)
        {
            uiProgress?.Invoke(endProgress);
            onComplete?.Invoke(true);
            yield break;
        }

        for (int index = 0; index < total; index++)
        {
            BaseSDKManager sdk = discoveredSDKs[index];
            if (sdk == null || !sdk.IsEnabled)
            {
                continue;
            }

            bool success = false;

            yield return StartCoroutine(
                sdk.InitializeHierarchy(
                    localProgress =>
                    {
                        float perSdkRange = (endProgress - startProgress) / total;
                        float totalProgress = startProgress + (index * perSdkRange) + (localProgress * perSdkRange);
                        uiProgress?.Invoke(totalProgress);
                    },
                    result =>
                    {
                        success = result;
                    }
                )
            );

            if (!success)
            {
                allSuccess = false;
                Debug.LogError($"~#~ SDKManager: {sdk.name} FAILED");
                break;
            }

            Debug.Log($"~#~ SDKManager: {sdk.name} ✓");
        }

        uiProgress?.Invoke(endProgress);
        onComplete?.Invoke(allSuccess);
    }


    private void PostSDKInitialization()
    {
        // GameAnalytics
        if (!GameAnalytics.Initialized)
        {
            GameAnalytics.Initialize();
            Debug.Log("~#~ SDKManager: GameAnalytics initialized");
        }

        InitializeAllRegisteredManagers();

        Debug.Log("~#~ SDKManager: All systems triggered");
    }

    private void TriggerSDKDependentManagers()
    {
        const string sdkManagerId = "SDKManager";

        foreach (IBaseManager mgr in ManagerRegistry.AllManagers)
        {
            if (!mgr.IsEnabled || mgr.IsFullyInitialized) continue;

            List<string> dependencies = TryGetDependencies(mgr);
            if (dependencies != null && dependencies.Contains(sdkManagerId))
            {
                var initMethod = mgr.GetType().GetMethod("Initialize");
                initMethod?.Invoke(mgr, null);
                Debug.Log($"~#~ SDKManager triggered {mgr.Id}");
            }
        }
    }
    private void InitializeAllRegisteredManagers()
    {
        foreach (var mgr in ManagerRegistry.AllManagers)
        {
            if (mgr.IsEnabled && !mgr.IsFullyInitialized)
            {
                Debug.Log($"~#~ SDKManager: Initializing registered manager {mgr.Id}");
                mgr.GetType().GetMethod("Initialize")?.Invoke(mgr, null);
            }
        }
    }

    private List<string> TryGetDependencies(IBaseManager mgr)
    {
        var type = mgr.GetType();

        var prop = type.GetProperty("Dependencies", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null)
        {
            return prop.GetValue(mgr) as List<string>;
        }

        var field = type.GetField("Dependencies", BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            return field.GetValue(mgr) as List<string>;
        }

        return null;
    }

    public Coroutine RunCoroutine(IEnumerator routine)
    {
        if (routine == null)
        {
            return null;
        }
        return StartCoroutine(routine);
    }

    public void StopRunningCoroutine(Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }
        StopCoroutine(routine);
    }

    #region Proxy System (Null-Safe Global Access)

    public T GetProxy<T>() where T : MonoBehaviour
    {
        string key = typeof(T).Name;
        if (!_proxies.TryGetValue(key, out var cached))
        {
            cached = scanRoot?.GetComponentInChildren<T>(true) ?? FindObjectOfType<T>();
            if (cached == null)
            {
                Debug.LogError($"~#~ SDKManager: Proxy {key} not found in hierarchy");
                return null;
            }
            _proxies[key] = cached;
            proxyCount++;
        }
        return (T)cached;
    }

    // 단축 프로퍼티
    public FirebaseManager FirebaseManager => GetProxy<FirebaseManager>();
    public AppsFlyerManager AppsFlyerManager => GetProxy<AppsFlyerManager>();

    #endregion

    #region Cleanup

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _proxies.Clear();
        if (_currentInitCoroutine != null)
            StopCoroutine(_currentInitCoroutine);
    }

    #endregion
}