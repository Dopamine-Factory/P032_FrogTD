using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Firebase.Analytics;
using GameAnalyticsSDK;

public class AnalyticsManager : BaseSystemManager<AnalyticsManager>
{
    private bool isGameAnalyticsInitialized = false;

    // Cache containers to prevent GC Allocations on every event log (Firebase & GA4용)
    private readonly List<Parameter> cachedFbParams = new List<Parameter>(20);
    private readonly Dictionary<string, object> cachedGaParams = new Dictionary<string, object>(20);

    // Dependencies caching (Assumes SDKManager handles these instances)
    private FirebaseManager firebaseManager;
    private AppsFlyerManager appsFlyerManager;

    public override void PostInitialize()
    {
        isGameAnalyticsInitialized = GameAnalytics.Initialized;

        firebaseManager = SDKManager.Instance.FirebaseManager;
        appsFlyerManager = SDKManager.Instance.AppsFlyerManager;

        base.PostInitialize();
    }

    public void AppOpenLog()
    {
        if (!IsEnabled) return;

        Debug.Log("[AnalyticsManager] AppOpenLog triggered.");

        if (appsFlyerManager != null && appsFlyerManager.IsEnabled)
        {
            appsFlyerManager.TrackEvent("af_app_open", new Dictionary<string, string>());
        }

        if (firebaseManager != null && firebaseManager.IsEnabled)
        {
            firebaseManager.Log(FirebaseAnalytics.EventAppOpen, Array.Empty<Parameter>());
        }

        if (isGameAnalyticsInitialized)
        {
            GameAnalytics.NewDesignEvent("app_open");
        }
    }

    /// <summary>
    /// MMP (AppsFlyer) 전용 로그 메서드
    /// 파라미터를 string으로 변환하여 AppsFlyer에 전송합니다.
    /// </summary>
    public void EventLog(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!IsEnabled || string.IsNullOrEmpty(eventName)) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AnalyticsManager] EventLog (MMP): {eventName}");
#endif

        if (appsFlyerManager != null && appsFlyerManager.IsEnabled)
        {
            Dictionary<string, string> afParams = new Dictionary<string, string>();
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    afParams[p.Key] = p.Value?.ToString() ?? "null";
                }
            }
            appsFlyerManager.TrackEvent(eventName, afParams);
        }
    }

    /// <summary>
    /// Firebase (GA4) 및 GameAnalytics 전용 로그 메서드
    /// GC를 유발하는 박싱/언박싱을 최소화하여 전송합니다.
    /// </summary>
    public void Log(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!IsEnabled || string.IsNullOrEmpty(eventName)) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AnalyticsManager] Log (Firebase/GA4): {eventName}");
#endif
        if (firebaseManager != null && firebaseManager.IsEnabled)
        {
            if (parameters != null && parameters.Count > 0)
            {
                PopulateFirebaseParameters(parameters);
                firebaseManager.Log(eventName, cachedFbParams.ToArray());
            }
            else
            {
                firebaseManager.Log(eventName, Array.Empty<Parameter>());
            }
        }

        if (isGameAnalyticsInitialized)
        {
            if (parameters != null && parameters.Count > 0)
            {
                PopulateGameAnalyticsParameters(parameters);
                GameAnalytics.NewDesignEvent(eventName, cachedGaParams);
            }
            else
            {
                GameAnalytics.NewDesignEvent(eventName);
            }
        }
    }

    #region Parameter Parsers (Optimized to prevent Boxing/Unboxing GC)

    private void PopulateFirebaseParameters(Dictionary<string, object> inputParams)
    {
        cachedFbParams.Clear();

        foreach (var kvp in inputParams)
        {
            if (kvp.Value == null) continue;

            switch (kvp.Value)
            {
                case string sVal:
                    cachedFbParams.Add(new Parameter(kvp.Key, sVal));
                    break;
                case long lVal:
                    cachedFbParams.Add(new Parameter(kvp.Key, lVal));
                    break;
                case int iVal:
                    cachedFbParams.Add(new Parameter(kvp.Key, (long)iVal)); // FB expects long
                    break;
                case double dVal:
                    cachedFbParams.Add(new Parameter(kvp.Key, dVal));
                    break;
                case float fVal:
                    cachedFbParams.Add(new Parameter(kvp.Key, (double)fVal)); // FB expects double
                    break;
                case bool bVal:
                    cachedFbParams.Add(new Parameter(kvp.Key, bVal ? 1L : 0L));
                    break;
                default:
                    cachedFbParams.Add(new Parameter(kvp.Key, kvp.Value.ToString()));
                    break;
            }
        }
    }

    private void PopulateGameAnalyticsParameters(Dictionary<string, object> inputParams)
    {
        cachedGaParams.Clear();

        foreach (var kvp in inputParams)
        {
            if (kvp.Value == null) continue;

            switch (kvp.Value)
            {
                case string _:
                case int _:
                case long _:
                case float _:
                case double _:
                case bool _:
                    cachedGaParams[kvp.Key] = kvp.Value;
                    break;
                default:
                    cachedGaParams[kvp.Key] = kvp.Value.ToString();
                    Debug.LogWarning($"[AnalyticsManager] Unrecognized type for GA parameter '{kvp.Key}'. Converted to string.");
                    break;
            }
        }
    }

    #endregion

    #region GameAnalytics Specific Methods

    public void LogProgression(GAProgressionStatus status, string progression01, string progression02 = null, string progression03 = null, int score = 0, Dictionary<string, object> parameters = null)
    {
        if (!IsEnabled || !isGameAnalyticsInitialized) return;

        if (string.IsNullOrEmpty(progression01))
        {
            Debug.LogError("[AnalyticsManager] progression01 cannot be null or empty.");
            return;
        }

        if (parameters != null)
        {
            PopulateGameAnalyticsParameters(parameters);
        }

        bool hasParams = parameters != null && cachedGaParams.Count > 0;

        if (score != 0)
        {
            if (hasParams)
                GameAnalytics.NewProgressionEvent(status, progression01, progression02, progression03, score, cachedGaParams);
            else
                GameAnalytics.NewProgressionEvent(status, progression01, progression02, progression03, score);
        }
        else
        {
            if (hasParams)
                GameAnalytics.NewProgressionEvent(status, progression01, progression02, progression03, cachedGaParams);
            else
                GameAnalytics.NewProgressionEvent(status, progression01, progression02, progression03);
        }
    }

    public void LogDesign(string segment01, string segment02 = null, string segment03 = null, string segment04 = null, string segment05 = null, float eventValue = 0f, Dictionary<string, object> parameters = null)
    {
        if (!IsEnabled || !isGameAnalyticsInitialized) return;

        if (string.IsNullOrEmpty(segment01))
        {
            Debug.LogError("[AnalyticsManager] segment01 cannot be null or empty.");
            return;
        }

        string[] segments = { segment01, segment02, segment03, segment04, segment05 };

        var validSegments = segments
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s.Length > 64 ? s.Substring(0, 64) : s)
            .ToArray();

        string eventName = string.Join(":", validSegments);

        if (parameters != null)
        {
            PopulateGameAnalyticsParameters(parameters);
        }

        bool hasParams = parameters != null && cachedGaParams.Count > 0;

        if (eventValue != 0f)
        {
            if (hasParams)
                GameAnalytics.NewDesignEvent(eventName, eventValue, cachedGaParams);
            else
                GameAnalytics.NewDesignEvent(eventName, eventValue);
        }
        else
        {
            if (hasParams)
                GameAnalytics.NewDesignEvent(eventName, cachedGaParams);
            else
                GameAnalytics.NewDesignEvent(eventName);
        }
    }

    #endregion
}