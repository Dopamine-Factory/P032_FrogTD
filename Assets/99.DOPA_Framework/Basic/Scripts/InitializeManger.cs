using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class InitializeManager : MonoBehaviour
{
    public static InitializeManager Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float baseManagersTimeout = 3f;
    [SerializeField] private float sdkTimeout = 5f;
    [SerializeField] private float remoteConfigTimeout = 5f;
    [SerializeField] private float rootManagersInitTimeout = 2f;

    private const float TableWeight = 0.35f;
    private const float SDKWeight = 0.35f;
    private const float RootBaseManagersWeight = 0.15f;
    private const float AllBaseManagersWeight = 0.15f;

    private float currentProgress = 0f;
    private bool isShuttingDown = false;


    public Action<float> OnChangedProgressValue;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        isShuttingDown = true;
    }

    private void Start()
    {
        ConnectivityMonitor.Instance.OnDisconnected += () =>
        {
            // PopupManager.Instance.ShowPopup<PopupNetworkError>();
        };

        ConnectivityMonitor.Instance.OnReconnected += () =>
        {
            // PopupManager.Instance.ClosePopup<PopupNetworkError>();
        };

        StartBootstrap();
    }


    public void StartBootstrap()
    {
        StartCoroutine(Bootstrap());
    }
    private IEnumerator Bootstrap()
    {
        Task initTask = InitializeAsync();
        yield return new WaitUntil(() => initTask.IsCompleted);
        if (initTask.IsFaulted)
        {
            Debug.LogError("[InitializeManager] Bootstrap failed: " + initTask.Exception);
        }
    }

    private async Task InitializeAsync()
    {
        await SequentialProgressPhase(
            () => Tables.LoadAllAsync(UpdateTableProgress),
            TableWeight,
            "Tables"
        );
        
        await SequentialProgressPhase(
            InitializeSDKManagerAsync,
            SDKWeight,
            "SDKManager"
        );

        await SequentialProgressPhase(
            () => TriggerRootBaseManagersAsync(rootManagersInitTimeout),
            RootBaseManagersWeight,
            "RootBaseManagers"
        );

        await SequentialProgressPhase(
            () => WaitForAllBaseManagersAsync(baseManagersTimeout),
            AllBaseManagersWeight,
            "AllBaseManagers"
        );
    }

    private async Task TriggerRootBaseManagersAsync(float timeoutSeconds)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;

        var allManagers = ManagerRegistry.AllManagers.Where(m => m != null && m.IsEnabled && !m.IsFullyInitialized).ToList();
        var roots = new List<IBaseManager>();

        foreach (var mgr in allManagers)
        {
            var depProp = mgr.GetType().GetProperty("Dependencies", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (depProp != null)
            {
                var deps = depProp.GetValue(mgr) as System.Collections.IEnumerable;
                int depCount = deps != null ? deps.Cast<object>().Count() : 0;

                if (depCount == 0)
                {
                    roots.Add(mgr);
                }
            }
        }

        if (roots.Count == 0)
        {
            return;
        }

        var tasks = roots.Select(root => Task.Run(() =>
        {
            var initMethod = root.GetType().GetMethod("Initialize");
            initMethod?.Invoke(root, null);
        })).ToArray();

        await Task.WhenAll(tasks.Concat(new[] { Task.Delay((int)(timeoutSeconds * 1000)) }));
    }

    private async Task SequentialProgressPhase(Func<Task> phaseTask, float weight, string phaseName)
    {
        float phaseStart = currentProgress;
        float phaseEnd = phaseStart + weight;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await phaseTask();
        stopwatch.Stop();
        await SmoothProgressTo(phaseEnd, 0.3f);
    }

    private async Task SmoothProgressTo(float target, float duration = 0.3f)
    {
        float startTime = Time.unscaledTime;
        float startProgress = currentProgress;
        while (Time.unscaledTime < startTime + duration && !isShuttingDown)
        {
            float elapsed = Time.unscaledTime - startTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            currentProgress = Mathf.Lerp(startProgress, target, t);
            OnChangedProgressValue?.Invoke(currentProgress);
            await Task.Yield();
        }
        currentProgress = target;
        OnChangedProgressValue?.Invoke(currentProgress);
    }

    private float tableTargetProgress = 0f;
    private float tableCurrentProgress = 0f;

    private void UpdateTableProgress(float rawProgress)
    {
        tableTargetProgress = TableWeight * rawProgress;

        tableCurrentProgress = Mathf.Lerp(tableCurrentProgress, tableTargetProgress, Time.unscaledDeltaTime * 10f);
        currentProgress = tableCurrentProgress;
        OnChangedProgressValue?.Invoke(currentProgress);

        Debug.Log($"[TableSmooth] raw:{rawProgress:P0} → smooth:{tableCurrentProgress:P0}");
    }



    private async Task InitializeSDKManagerAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        float sdkLocalProgress = 0f;

        SDKManager.Instance.InitializeSDKs(
            p => sdkLocalProgress = p,
            success => tcs.TrySetResult(success)
        );

        await Task.WhenAny(tcs.Task, Task.Delay((int)(sdkTimeout * 1000)));
        if (!tcs.Task.IsCompleted)
        {
            Debug.LogError("[InitializeManager] SDKManager TIMEOUT");
            tcs.TrySetResult(false);
        }
    }

    private async Task WaitForAllBaseManagersAsync(float timeoutSeconds)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
        const float logInterval = 1f;
        float nextLog = Time.realtimeSinceStartup + logInterval;
        int initialPending = ManagerRegistry.AllManagers
            .Count(m => m != null && m.IsEnabled && !m.IsFullyInitialized);

        while (Time.realtimeSinceStartup < timeoutAt && !isShuttingDown)
        {
            var pending = ManagerRegistry.AllManagers
                .Where(m => m != null && m.IsEnabled && !m.IsFullyInitialized)
                .ToList();

            float localProgress = initialPending > 0 ? 1f - ((float)pending.Count / initialPending) : 1f;
            float phaseOffset = 0.35f + SDKWeight + RootBaseManagersWeight;
            currentProgress = phaseOffset + (localProgress * AllBaseManagersWeight);
            OnChangedProgressValue?.Invoke(currentProgress);

            if (pending.Count == 0)
            {
                Debug.Log("[InitializeManager] All BaseSystemManagers ready");
                return;
            }

            if (Time.realtimeSinceStartup >= nextLog)
            {
                var ids = string.Join(", ", pending.Select(m => m.Id));
                Debug.LogWarning($"[InitializeManager] BaseManagers waiting ({pending.Count}/{initialPending}): {ids}");
                nextLog = Time.realtimeSinceStartup + logInterval;
            }

            await Task.Yield();
        }

        var finalPending = ManagerRegistry.AllManagers
            .Where(m => m != null && m.IsEnabled && !m.IsFullyInitialized)
            .ToList();
        if (finalPending.Count > 0)
        {
            Debug.LogError($"[InitializeManager] BaseManagers TIMEOUT. Pending: {string.Join(", ", finalPending.Select(m => m.Id))}");
        }
    }

    bool isStart = false;
    public void StartGame()
    {
        if (isStart) return;

        isStart = true;
        StartCoroutine(StartGameLoad());
    }

    private IEnumerator StartGameLoad()
    {
        yield return new WaitForSeconds(0.5f);
        yield return LoadGameRoutine();
    }

    private IEnumerator LoadGameRoutine()
    {
        Task runTask = LoadGameAsync();
        yield return new WaitUntil(() => runTask.IsCompleted);
    }

    private async Task LoadGameAsync()
    {
        Debug.Log("[InitializeManager] LoadGame");
        if (RemoteConfigManager.Instance != null)
        {
            await WaitRemoteConfigReadyAsync(remoteConfigTimeout);
        }

#if UNITY_EDITOR
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        await LoadSceneAsync(nextIndex);
#else
        bool proceed = GameUpdateManager.Instance?.CheckUpdate() != true;
        if (proceed)
        {
            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
            await LoadSceneAsync(nextIndex);
        }
#endif
    }

    private async Task WaitRemoteConfigReadyAsync(float timeoutSeconds)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < timeoutAt && !isShuttingDown)
        {
            if (RemoteConfigManager.Instance?.GetIsRemoteInitialized == true)
            {
                return;
            }
            await Task.Yield();
        }
        Debug.LogWarning("[InitializeManager] RemoteConfig timeout.");
    }

    private static Task LoadSceneAsync(int buildIndex)
    {
        var tcs = new TaskCompletionSource<bool>();
        var operation = SceneManager.LoadSceneAsync(buildIndex);
        operation.completed += _ => tcs.TrySetResult(true);
        return tcs.Task;
    }
}