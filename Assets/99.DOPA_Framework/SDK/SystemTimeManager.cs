using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class TimeEventParams
{
    public string eventId;
    public TimeEventType eventType;
    public double triggerTime;
    public float intervalSeconds;
    public int repeatCount;
    public int currentRepeatCount;
    public bool isPaused;
    public int priority;
    public string callbackHash;

    public TimeEventParams(string id, TimeEventType type, float delay, float interval, int repeat)
    {
        eventId = id;
        eventType = type;
        intervalSeconds = interval;
        repeatCount = repeat;
        currentRepeatCount = 0;
        isPaused = false;
        priority = 0;
        callbackHash = string.Empty;

        triggerTime = SystemTimeManager.IsInitialized
            ? SystemTimeManager.Instance.GetCorrectedUnixTime() + delay
            : delay;
    }
}

[Serializable]
public class TimeEventState
{
    public List<TimeEventParams> activeEvents = new List<TimeEventParams>();
    public List<TimeEventParams> pausedEvents = new List<TimeEventParams>();
    public double lastSyncTime;
    public double gameStartTime;
    public float inGamePlayTime;
    public float localToRealTimeOffset;
}

public enum TimeEventType
{
    OneShot,
    Repeating,
    Daily,
    Weekly,
    Monthly
}

public class RealNTPClient
{
    private const int NtpPacketSize = 48;
    private const int NtpPort = 123;
    private static readonly byte[] ntpData = new byte[NtpPacketSize];

    static RealNTPClient()
    {
        ntpData[0] = 0x1B;
    }

    public static async Task<double?> GetUnixTimeAsync(string server, CancellationToken cancellationToken = default)
    {
        try
        {
            using (var udpClient = new System.Net.Sockets.UdpClient())
            {
                udpClient.Client.ReceiveTimeout = 5000;
                udpClient.Connect(server, NtpPort);
                await udpClient.SendAsync(ntpData, NtpPacketSize);

                var result = await udpClient.ReceiveAsync();
                if (result.Buffer != null && result.Buffer.Length >= 48)
                {
                    byte[] buffer = result.Buffer;
                    uint seconds = (uint)(buffer[40] << 24 | buffer[41] << 16 | buffer[42] << 8 | buffer[43]);
                    uint fraction = (uint)(buffer[44] << 24 | buffer[45] << 16 | buffer[46] << 8 | buffer[47]);

                    double unixTime = seconds - 2208988800u + (double)fraction / uint.MaxValue;
                    return unixTime;
                }
            }
        }
        catch (Exception)
        {
            // _ToDo Add more robust server failover or logging if needed.
        }

        return null;
    }
}

public class SystemTimeManager : Singleton<SystemTimeManager>
{
    [Header("NTP Settings")]
    [SerializeField] private float ntpSyncInterval = 1800f;
    [SerializeField] private string[] ntpServers = { "time.google.com", "time.cloudflare.com", "time.nist.gov" };
    [SerializeField] private bool autoSyncOnStart = true;

    [Header("Debug")]
    [SerializeField] private double lastSyncUnixTime;
    [SerializeField] private float localToRealOffset;
    [SerializeField] private bool isSyncing;

    private Coroutine periodicSyncCoroutine;
    private CancellationTokenSource cancellationTokenSource;

    private const string TimeStateKey = "SystemTimeState";

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();
        cancellationTokenSource = new CancellationTokenSource();
        LoadTimeState();

        if (autoSyncOnStart)
        {
            StartCoroutine(InitialSync());
        }
    }

    private IEnumerator InitialSync()
    {
        yield return StartCoroutine(SyncNtpTime());
        periodicSyncCoroutine = StartCoroutine(PeriodicSync());
    }

    private IEnumerator PeriodicSync()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(30f, ntpSyncInterval));
            yield return StartCoroutine(SyncNtpTime());
        }
    }

    public IEnumerator SyncNtpTime()
    {
        if (isSyncing)
        {
            yield break;
        }

        isSyncing = true;

        foreach (string server in ntpServers)
        {
            var syncTask = Task.Run(() => RealNTPClient.GetUnixTimeAsync(server, cancellationTokenSource.Token));
            yield return new WaitUntil(() => syncTask.IsCompleted);

            if (syncTask.Result.HasValue)
            {
                double serverTime = syncTask.Result.Value;
                lastSyncUnixTime = serverTime;
                double localTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                localToRealOffset = (float)(serverTime - localTime);

                SaveTimeState();
                Debug.Log($"[SystemTimeManager] NTP Sync OK: {server} offset: {localToRealOffset:F3}s");
                isSyncing = false;
                yield break;
            }
        }

        Debug.LogWarning("[SystemTimeManager] All NTP servers failed");
        isSyncing = false;
    }

    public double GetCorrectedUnixTime()
    {
        double localUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return localUnixTime + localToRealOffset;
    }

    public float GetLocalToRealOffset()
    {
        return localToRealOffset;
    }

    private void SaveTimeState()
    {
        var state = new TimeEventState
        {
            lastSyncTime = lastSyncUnixTime,
            localToRealTimeOffset = localToRealOffset
        };

        UserDataManager.Instance.SetDynamicValue(TimeStateKey, JsonUtility.ToJson(state));
    }

    private void LoadTimeState()
    {
        string stateJson = UserDataManager.Instance.GetDynamicValue<string>(TimeStateKey, string.Empty);
        if (string.IsNullOrEmpty(stateJson))
        {
            return;
        }

        try
        {
            var state = JsonUtility.FromJson<TimeEventState>(stateJson);
            lastSyncUnixTime = state.lastSyncTime;
            localToRealOffset = state.localToRealTimeOffset;
            Debug.Log($"[SystemTimeManager] Loaded time state: offset {localToRealOffset:F3}s");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SystemTimeManager] Failed to load time state: {e.Message}");
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            StartCoroutine(SyncNtpTime());
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        cancellationTokenSource?.Cancel();
    }

}