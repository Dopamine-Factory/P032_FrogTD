using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_IOS
using Apple.Core;
using Apple.CoreHaptics;
#endif

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


public enum HapticPreset
{
    BasicVibe,
    SmallVibe,
    UI_Confirm
}


[System.Serializable]
public struct HapticPattern
{
    public float[] timings;
    public float[] intensities;
    public float[] sharpness;
}


public class HapticManager : MonoBehaviour
{
    private static HapticManager _instance;
    public static HapticManager Instance => _instance;


    [Header("Android Settings")]
    [SerializeField][Range(0.1f, 5f)] private float _maxAndroidDuration = 2f;


    [Header("iOS Settings")]
    [SerializeField][Range(0.1f, 1f)] private float _minIOSIntensity = 0.1f;


    [Header("Gamepad Settings")]
    [SerializeField][Range(0f, 1f)] private float _leftMotorIntensity = 0.8f;
    [SerializeField][Range(0f, 1f)] private float _rightMotorIntensity = 0.8f;


    private Queue<HapticRequest> _requestQueue = new Queue<HapticRequest>();
    private bool _isProcessing = false;


#if UNITY_IOS
    private CHHapticEngine _engine;
    private bool _supportsHaptics;
#endif


    private void Awake() => InitializeSingleton();


    private void InitializeSingleton()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);


        if (UserDataManager.Instance != null)
            IsHapticEnabled = UserDataManager.Instance.IsHapticOn.Value;


#if UNITY_IOS
        InitializeIOSEngine();
#endif
    }


    public static bool IsHapticEnabled { get; private set; } = true;


    public static void SetHapticEnabled(bool enabled)
    {
        Debug.Log($"[Haptic] SetHapticEnabled: {enabled}");
        IsHapticEnabled = enabled;
        if (UserDataManager.Instance != null)
            UserDataManager.Instance.IsHapticOn.Value = enabled;
    }


    public void TriggerHaptic(float intensity, float duration)
    {
        if (!IsHapticEnabled) return;


#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsAndroidVibrationSupported())
            AndroidVibrate((int)(Mathf.Clamp(duration, 0.1f, _maxAndroidDuration) * 1000));
#elif UNITY_IOS && !UNITY_EDITOR
        PlayIOSHaptic(Mathf.Clamp(intensity, _minIOSIntensity, 1f), duration);
#else
        TriggerGamepadHaptic(intensity, duration);
#endif
        LogHapticEvent($"TriggerHaptic(intensity:{intensity:F2}, duration:{duration:F2})");
    }


    public void PlayPreset(HapticPreset preset)
    {
        switch (preset)
        {
            case HapticPreset.BasicVibe:
                TriggerHaptic(1f, 0.3f);
                break;
            case HapticPreset.SmallVibe:
                PlayGranularHaptic(0.2f, 0.1f, 5);
                break;
            case HapticPreset.UI_Confirm:
                TriggerHaptic(0.5f, 0.15f);
                break;
        }
    }


    public void PlayCustomPattern(HapticPattern pattern)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (_engine == null) return;
        var events = CreateIOSHapticEvents(pattern);
        var player = _engine.MakePlayer(events);
        player.Start();
#elif UNITY_ANDROID && !UNITY_EDITOR
        PlayAndroidPattern(pattern);
#else
        TriggerGamepadHaptic(0.7f, 0.3f);
#endif
        LogHapticEvent("PlayCustomPattern");
    }


#if UNITY_IOS
    private void InitializeIOSEngine()
    {
        _supportsHaptics = CHHapticEngine.HardwareSupportsHaptics();

        if (!_supportsHaptics)
        {
            Debug.LogWarning("[Haptic] Device does not support haptics");
            return;
        }

        try
        {
            _engine = new CHHapticEngine();
            _engine.IsAutoShutdownEnabled = true;
            _engine.NotifyWhenPlayersFinished(false);
            _engine.Start();
            Debug.Log("[Haptic] CoreHaptics engine ready");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Haptic] Engine init failed: {e.Message}");
            _engine = null;
        }
    }


    private List<CHHapticEvent> CreateIOSHapticEvents(HapticPattern pattern)
    {
        var events = new List<CHHapticEvent>();
        for (int i = 0; i < pattern.timings.Length; i++)
        {
            float intensity = Mathf.Clamp(pattern.intensities[i], _minIOSIntensity, 1f);
            float sharpness = i < pattern.sharpness.Length ? pattern.sharpness[i] : 0.5f;

            List<CHHapticEventParameter> parameters = new List<CHHapticEventParameter>
            {
                new CHHapticEventParameter(CHHapticEventParameterID.HapticIntensity, intensity),
                new CHHapticEventParameter(CHHapticEventParameterID.HapticSharpness, sharpness)
            };

            var continuousEvent = new CHHapticContinuousEvent();
            continuousEvent.Time = pattern.timings[i];
            continuousEvent.SetEventDuration(0.1f);
            continuousEvent.EventParameters = parameters;
            events.Add(continuousEvent);
        }
        return events;
    }


    private void PlayIOSHaptic(float intensity, float duration)
    {
        if (_engine == null) return;

        try
        {
            List<CHHapticEventParameter> parameters = new List<CHHapticEventParameter>
            {
                new CHHapticEventParameter(CHHapticEventParameterID.HapticIntensity, intensity),
                new CHHapticEventParameter(CHHapticEventParameterID.HapticSharpness, 0.5f)
            };

            var transientEvent = new CHHapticTransientEvent();
            transientEvent.Time = 0f;
            transientEvent.EventParameters = parameters;

            var events = new List<CHHapticEvent> { transientEvent };
            var player = _engine.MakePlayer(events);
            player.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Haptic] Play failed: {e.Message}");
        }
    }
#endif


#if UNITY_ANDROID
    private bool IsAndroidVibrationSupported()
    {
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            return vibrator.Call<bool>("hasVibrator");
        }
        catch
        {
            return false;
        }
    }


    private void AndroidVibrate(int milliseconds)
    {
        Handheld.Vibrate();
    }


    private void PlayAndroidPattern(HapticPattern pattern)
    {
        Handheld.Vibrate();
    }
#else
    private bool IsAndroidVibrationSupported() => false;
    private void AndroidVibrate(int milliseconds) { }
    private void PlayAndroidPattern(HapticPattern pattern) { }
#endif

    // 게임패드 지원 (Gamepad가 있는 경우만) -> 스팀 출시를 위한 사전 작업 이후 고도화
    private void TriggerGamepadHaptic(float intensity, float duration)
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                gamepad.SetMotorSpeeds(
                    _leftMotorIntensity * intensity,
                    _rightMotorIntensity * intensity
                );
                StartCoroutine(StopGamepadHapticAfter(duration));
                LogHapticEvent($"Gamepad haptic triggered (L:{_leftMotorIntensity * intensity:F2}, R:{_rightMotorIntensity * intensity:F2})");
            }
            else
            {
                Debug.LogWarning("[Haptic] No gamepad connected");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Haptic] Gamepad haptic error: {e.Message}");
        }
#else
        Debug.LogWarning("[Haptic] InputSystem not enabled. Gamepad haptics unavailable.");
#endif
    }


    private IEnumerator StopGamepadHapticAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
#if ENABLE_INPUT_SYSTEM
        try
        {
            var gamepad = Gamepad.current;
            if (gamepad != null)
                gamepad.SetMotorSpeeds(0f, 0f);
        }
        catch
        {
            // Gamepad disconnected 
        }
#endif
    }


    public void QueueHapticRequest(float intensity, float duration)
    {
        _requestQueue.Enqueue(new HapticRequest { intensity = intensity, duration = duration });
        if (!_isProcessing)
            StartCoroutine(ProcessHapticQueue());
    }


    public void PlayGranularHaptic(float baseIntensity, float interval, int count)
    {
        StartCoroutine(GranularHapticRoutine(baseIntensity, interval, count));
    }


    private IEnumerator GranularHapticRoutine(float baseIntensity, float interval, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float currentIntensity = baseIntensity * (1f - (float)i / count);
            TriggerHaptic(currentIntensity, interval);
            yield return new WaitForSeconds(interval);
        }
    }


    private IEnumerator ProcessHapticQueue()
    {
        _isProcessing = true;
        while (_requestQueue.Count > 0)
        {
            var request = _requestQueue.Dequeue();
            TriggerHaptic(request.intensity, request.duration);
            yield return new WaitForSeconds(request.duration * 0.8f);
        }
        _isProcessing = false;
    }


    private void LogHapticEvent(string message)
    {
#if UNITY_EDITOR
        Debug.Log($"[Haptic] {message} | Platform: {Application.platform}");
#endif
    }


    private struct HapticRequest
    {
        public float intensity;
        public float duration;
    }


    private void OnDestroy()
    {
#if UNITY_IOS
        if (_engine != null)
        {
            _engine.DestroyAllPlayers();
            _engine.Destroy();
        }
#endif
    }


#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(HapticManager))]
    public class HapticManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            HapticManager manager = (HapticManager)target;

            GUILayout.Space(10);
            GUILayout.Label("Haptic Test", UnityEditor.EditorStyles.boldLabel);

            if (GUILayout.Button("테스트: BasicVibe", GUILayout.Height(40)))
                manager.PlayPreset(HapticPreset.BasicVibe);
            if (GUILayout.Button("테스트: UI_Confirm", GUILayout.Height(40)))
                manager.PlayPreset(HapticPreset.UI_Confirm);
            if (GUILayout.Button("테스트: Custom (0.8f, 0.5f)", GUILayout.Height(40)))
                manager.TriggerHaptic(0.8f, 0.5f);
        }
    }
#endif
}