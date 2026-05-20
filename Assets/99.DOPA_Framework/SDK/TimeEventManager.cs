using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeEventManager : Singleton<TimeEventManager>
{
    [Header("Event Settings")]
    [SerializeField] private bool isGlobalPaused;

    [Header("Debug Info")]
    [SerializeField] private int activeEventCount;
    [SerializeField] private int pausedEventCount;
    [SerializeField] private double nextEventTime;
    [SerializeField] private List<TimeEventParams> debugActiveEvents = new List<TimeEventParams>();

    public Action<string, TimeEventType> OnTimeEventTriggered;
    public Action OnDailyFirstLogin;

    [SerializeField] private List<TimeEventParams> activeEvents = new List<TimeEventParams>();
    [SerializeField] private List<TimeEventParams> pausedEvents = new List<TimeEventParams>();

    private Dictionary<string, TimeEventParams> eventLookup;
    private Coroutine schedulerCoroutine;
    private TimeEventState persistentState;

    private const string EventStateKey = "TimeEventManagerState";

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();
        InitializeManager();
    }

    private void InitializeManager()
    {
        activeEvents = new List<TimeEventParams>();
        pausedEvents = new List<TimeEventParams>();
        eventLookup = new Dictionary<string, TimeEventParams>();

        LoadEventState();
        LoadInGameTime(); 
        schedulerCoroutine = StartCoroutine(SchedulerLoop());
        UpdateDebugDisplay();
    }

    #region In-Game Play Time Tracking
    [Header("In-Game Time Tracking")]
    [SerializeField] private bool trackInGameTime = true;
    [SerializeField] private float inGamePlayTime;

    public float GetInGameActiveTime() => inGamePlayTime;

    private bool isInGamePaused;

    private void Update()  // 새로 추가
    {
        if (trackInGameTime && !isGlobalPaused && !isInGamePaused)
        {
            inGamePlayTime += Time.unscaledDeltaTime;
        }
    }

    public void PauseInGameTime(bool paused)
    {
        isInGamePaused = paused;
    }

    public void ResetInGameTime()
    {
        inGamePlayTime = 0f;
    }

    private void SaveInGameTime()
    {
        persistentState ??= new TimeEventState();
        persistentState.inGamePlayTime = inGamePlayTime;
    }

    private void LoadInGameTime()
    {
        if (persistentState != null)
        {
            inGamePlayTime = persistentState.inGamePlayTime;
        }
    }
    #endregion

    private IEnumerator SchedulerLoop()
    {
        while (true)
        {
            if (!isGlobalPaused && activeEvents.Count > 0)
            {
                double currentTime = SystemTimeManager.Instance.GetCorrectedUnixTime();

                TimeEventParams nextEvent = activeEvents[0];
                double nextTrigger = nextEvent.triggerTime;

                if (currentTime >= nextTrigger)
                {
                    ProcessEvent(nextEvent);
                }
                else
                {
                    double waitTime = nextTrigger - currentTime;
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.016f, (float)waitTime));
                }
            }
            else
            {
                yield return null;
            }

            UpdateDebugDisplay();
        }
    }

    private void ProcessEvent(TimeEventParams evtParams)
    {
        activeEvents.Remove(evtParams);

        try
        {
            Debug.Log($"[TimeEventManager] Executed: {evtParams.eventId}");
            OnTimeEventTriggered?.Invoke(evtParams.eventId, evtParams.eventType);

            if (ShouldRepeat(evtParams))
            {
                evtParams.currentRepeatCount++;
                evtParams.triggerTime += evtParams.intervalSeconds;
                EnqueueEvent(evtParams);
            }
            else
            {
                eventLookup.Remove(evtParams.eventId);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TimeEventManager] Event failed: {evtParams.eventId} - {e.Message}");
        }
    }

    private bool ShouldRepeat(TimeEventParams evt)
    {
        return evt.repeatCount == 0 || evt.currentRepeatCount < evt.repeatCount - 1;
    }

    public void RegisterTimeEvent(string eventId, TimeEventType type, float delay, float interval = 0f, int repeatCount = 1)
    {
        var evtParams = new TimeEventParams(eventId, type, delay, interval, repeatCount);
        EnqueueEvent(evtParams);
        SaveEventState();
    }

    public bool UnregisterTimeEvent(string eventId)
    {
        if (eventLookup.TryGetValue(eventId, out TimeEventParams evt))
        {
            eventLookup.Remove(eventId);
            activeEvents.Remove(evt);
            pausedEvents.Remove(evt);
            SaveEventState();
            UpdateDebugDisplay();
            return true;
        }

        return false;
    }

    public void PauseEvent(string eventId, bool pause)
    {
        if (eventLookup.TryGetValue(eventId, out TimeEventParams evt))
        {
            evt.isPaused = pause;
            if (pause)
            {
                activeEvents.Remove(evt);
                if (!pausedEvents.Contains(evt))
                {
                    pausedEvents.Add(evt);
                }
            }
            else
            {
                pausedEvents.Remove(evt);
                EnqueueEvent(evt);
            }

            SaveEventState();
            UpdateDebugDisplay();
        }
    }

    public void PauseAllEvents(bool pause)
    {
        isGlobalPaused = pause;
    }

    public bool IsFirstLoginToday()
    {
        string key = $"LastLogin_{DateTime.UtcNow.Year}_{DateTime.UtcNow.Month}_{DateTime.UtcNow.Day}";

        if (!UserDataManager.Instance.GetDynamicValue<bool>(key, false))
        {
            UserDataManager.Instance.SetDynamicValue(key, true);
            OnDailyFirstLogin?.Invoke();
            return true;
        }

        return false;
    }

    private void EnqueueEvent(TimeEventParams evtParams)
    {
        eventLookup[evtParams.eventId] = evtParams;

        if (evtParams.isPaused)
        {
            if (!pausedEvents.Contains(evtParams))
            {
                pausedEvents.Add(evtParams);
            }
        }
        else
        {
            if (!activeEvents.Contains(evtParams))
            {
                activeEvents.Add(evtParams);
            }

            activeEvents.Sort((a, b) => a.triggerTime.CompareTo(b.triggerTime));
        }
    }

    private void UpdateDebugDisplay()
    {
        activeEventCount = activeEvents.Count;
        pausedEventCount = pausedEvents.Count;
        nextEventTime = activeEvents.Count > 0 ? activeEvents[0].triggerTime : 0;

        debugActiveEvents.Clear();
        debugActiveEvents.AddRange(activeEvents);
    }

    private void SaveEventState()
    {
        persistentState ??= new TimeEventState();
        persistentState.activeEvents.Clear();
        persistentState.pausedEvents.Clear();

        foreach (var evt in eventLookup.Values)
        {
            if (evt.isPaused)
            {
                persistentState.pausedEvents.Add(evt);
            }
            else
            {
                persistentState.activeEvents.Add(evt);
            }
        }
        SaveInGameTime();
        persistentState.gameStartTime = SystemTimeManager.Instance.GetCorrectedUnixTime();
        UserDataManager.Instance.SetDynamicValue(EventStateKey, JsonUtility.ToJson(persistentState));
    }

    private void LoadEventState()
    {
        string stateJson = UserDataManager.Instance.GetDynamicValue<string>(EventStateKey, string.Empty);
        if (string.IsNullOrEmpty(stateJson))
        {
            Debug.Log("[TimeEventManager] No saved event state");
            return;
        }

        try
        {
            persistentState = JsonUtility.FromJson<TimeEventState>(stateJson);

            foreach (var evt in persistentState.activeEvents)
            {
                evt.isPaused = false;
                EnqueueEvent(evt);
            }

            foreach (var evt in persistentState.pausedEvents)
            {
                evt.isPaused = true;
                eventLookup[evt.eventId] = evt;
                if (!pausedEvents.Contains(evt))
                {
                    pausedEvents.Add(evt);
                }
            }

            Debug.Log($"[TimeEventManager] Loaded {persistentState.activeEvents.Count} events");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TimeEventManager] Failed to load events: {e.Message}");
        }
    }
}