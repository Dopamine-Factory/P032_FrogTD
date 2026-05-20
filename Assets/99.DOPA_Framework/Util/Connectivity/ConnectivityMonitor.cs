using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ConnectivityMonitor : MonoBehaviour
{
    [Header("Time Scale Control")]
    [SerializeField] private bool pauseTimeScaleOnDisconnect = true;

    public static ConnectivityMonitor Instance { get; private set; }

    // Event
    public event Action OnDisconnected;
    public event Action OnReconnected;

    // Polling
    private float connectedIntervalSeconds = 2f;
    private float disconnectedIntervalSeconds = 1f;

    // HTTP Probe
    private int requestTimeoutSeconds = 2;
    private string checkUrl = "https://www.gstatic.com/generate_204";

    // Debounce
    private int failCountToDisconnect = 2;
    private int successCountToReconnect = 2;

    public bool IsConnected { get; private set; } = true;
    private bool isLoop;

    private CancellationTokenSource _cts;

    // 동시에 여러 체크가 도는 걸 방지
    private readonly SemaphoreSlim _probeLock = new(1, 1);

    private int _consecutiveFails;
    private int _consecutiveSuccesses;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        _cts = new CancellationTokenSource();
    }

    private void Start()
    {
        // 연결 감시 루프 시작
        _ = MonitorConnectionAsync(_cts.Token);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async Awaitable MonitorConnectionAsync(CancellationToken token)
    {
        try
        {
            isLoop = true;

            while (!token.IsCancellationRequested)
            {
                // Debug.Log("[ConnectivityMonitor] Checking internet access...");
                bool ok = await CheckInternetAccessAsync(token);
                // Debug.Log($"[ConnectivityMonitor] Check result: {ok}");

                ApplyProbeResult(ok);

                float delay = IsConnected ? connectedIntervalSeconds : disconnectedIntervalSeconds;
                // Debug.Log($"[ConnectivityMonitor] Waiting for {delay} seconds before next check.");
                await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true, cancellationToken: token);
            }
        }
        catch (OperationCanceledException)
        {
            // 정상 종료
            isLoop = false;
        }
    }

    private async Awaitable<bool> CheckInternetAccessAsync(CancellationToken token)
    {
        // 1) 빠른 힌트 (최종 판정은 아님)
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            // Debug.Log("[ConnectivityMonitor] Fast hint: NotReachable.");
            return false;
        }

        await _probeLock.WaitAsync(token);
        try
        {
            // Debug.Log($"[ConnectivityMonitor] Sending web request to {checkUrl} ...");
            using var request = UnityWebRequest.Get(checkUrl);
            request.timeout = requestTimeoutSeconds;

            // 캐시 영향 줄이기(선택)
            request.SetRequestHeader("Cache-Control", "no-cache");
            request.SetRequestHeader("Pragma", "no-cache");

            await request.SendWebRequest().WithCancellation(token);

            // gstatic generate_204 는 성공 시 204가 오는 게 일반적
            bool success = request.result == UnityWebRequest.Result.Success && request.responseCode == 204;
            // Debug.Log($"[ConnectivityMonitor] Web request result: {request.result}, code: {request.responseCode}, success: {success}");
            return success;
        }
        catch (OperationCanceledException)
        {
            // Debug.Log("[ConnectivityMonitor] Web request canceled.");
            throw; // 취소는 종료 흐름
        }
        catch (Exception ex)
        {
            // Debug.Log($"[ConnectivityMonitor] Web request exception: {ex.Message}");
            return false;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    private void ApplyProbeResult(bool connected)
    {
        if (connected)
        {
            _consecutiveFails = 0;
            _consecutiveSuccesses++;
            // Debug.Log($"[ConnectivityMonitor] Network success. Consecutive successes: {_consecutiveSuccesses}");

            if (!IsConnected && _consecutiveSuccesses >= successCountToReconnect)
            {
                Debug.Log("[ConnectivityMonitor] Meets reconnect threshold. Changing status to Connected.");
                SetStatus(true);
            }
        }
        else
        {
            _consecutiveSuccesses = 0;
            _consecutiveFails++;
            // Debug.Log($"[ConnectivityMonitor] Network fail. Consecutive fails: {_consecutiveFails}");

            if (IsConnected && _consecutiveFails >= failCountToDisconnect)
            {
                Debug.Log("[ConnectivityMonitor] Meets disconnect threshold. Changing status to Disconnected.");
                SetStatus(false);
            }
        }
    }

    private void SetStatus(bool connected)
    {
        if (IsConnected == connected) return;

        Debug.Log($"[ConnectivityMonitor] Status changed to: {(connected ? "Connected" : "Disconnected")}");
        IsConnected = connected;

        if (pauseTimeScaleOnDisconnect)
            Time.timeScale = connected ? 1f : 0f;

        if (connected)
        {
            OnReconnected?.Invoke();
        }
        else
        {
            OnDisconnected?.Invoke();
        }
    }

    /// <summary>
    /// 외부 서비스 에러 발생 시 즉시 네트워크 체크 강제 실행
    /// </summary>
    public async Awaitable ForceCheckNow()
    {
        if (_cts == null || _cts.IsCancellationRequested) return;

        // Debug.Log("[ConnectivityMonitor] ForceCheckNow called.");

        bool ok = await CheckInternetAccessAsync(_cts.Token);
        ApplyProbeResult(ok);
    }

    // 앱을 나갔다 들어왔을 때, 바로 연결 점검
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!isLoop) return;

        if (hasFocus)
            _ = ForceCheckNow();
    }
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!isLoop) return;

        if (!pauseStatus)
            _ = ForceCheckNow();
    }
}
