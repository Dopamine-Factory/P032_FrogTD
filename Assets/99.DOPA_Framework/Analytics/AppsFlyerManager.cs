using UnityEngine;
using AppsFlyerSDK;
using System;
using System.Collections.Generic;
using System.Collections;

public class AppsFlyerManager : BaseSDKManager, IAppsFlyerConversionData
{
    [Header("AppsFlyer Settings")]
    [SerializeField] private string iosAppID;
    [SerializeField] private string androidAppID;
    [SerializeField] private string devKey;

    private bool isInitialized = false;
    private bool hasDeepLinkRegistered = false;
    private Coroutine initializationCoroutine;

    public event Action<Dictionary<string, object>> OnDeepLinkProcessed;

    public override IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        if (isInitialized)
        {
            Debug.Log("[AppsFlyer] Already initialized.");
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(true);
            yield break;
        }

        if (!IsEnabled)
        {
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(true);
            yield break;
        }

        Debug.Log("[AppsFlyer] Initialization Start");
        progressCallback?.Invoke(0f);

        initializationCoroutine = StartCoroutine(ProcessInitialization(progressCallback, onComplete));
    }

    private IEnumerator ProcessInitialization(Action<float> progressCallback, Action<bool> onComplete)
    {
        Exception initializationException = null;

        try
        {
            RegisterEvents();
            SetupSDK();
        }
        catch (Exception ex)
        {
            initializationException = ex;
        }

        if (initializationException != null)
        {
            Debug.LogError($"[AppsFlyer] Initialization setup failed: {initializationException.Message}");
            progressCallback?.Invoke(0f);
            onComplete?.Invoke(false);
            yield break;
        }

        yield return null;

        try
        {
            // _ToDo: Implement push token retrieval securely via Firebase Messaging integration
            UpdateUninstallToken(PlayerPrefs.GetString("PushToken", string.Empty));

            isInitialized = true;
        }
        catch (Exception ex)
        {
            initializationException = ex;
        }

        if (initializationException != null)
        {
            Debug.LogError($"[AppsFlyer] Token update failed: {initializationException.Message}");
            progressCallback?.Invoke(0f);
            onComplete?.Invoke(false);
            yield break;
        }

        Debug.Log("[AppsFlyer] Initialization Complete");
        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
    }

    private void SetupSDK()
    {
        if (string.IsNullOrEmpty(devKey))
        {
            Debug.LogError("[AppsFlyer] DevKey is missing. Setup aborted.");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AppsFlyer.setIsDebug(true);
#endif

#if UNITY_IOS && !UNITY_EDITOR
            AppsFlyer.initSDK(devKey, iosAppID, this);
#elif UNITY_ANDROID && !UNITY_EDITOR
            AppsFlyer.initSDK(devKey, androidAppID, this);
#else
        Debug.LogWarning("[AppsFlyer] Editor or Unsupported platform. Skipping real SDK initialization.");
#endif

        AppsFlyer.startSDK();
    }

    private void RegisterEvents()
    {
        if (hasDeepLinkRegistered)
        {
            return;
        }

        AppsFlyer.OnDeepLinkReceived += HandleDeepLinkReceived;
        hasDeepLinkRegistered = true;
    }

    private void HandleDeepLinkReceived(object sender, EventArgs args)
    {
        try
        {
            if (args is DeepLinkEventsArgs deepLinkArgs && deepLinkArgs.status == DeepLinkStatus.FOUND)
            {
                Dictionary<string, object> parameters = deepLinkArgs.deepLink;

                if (parameters != null)
                {
                    Debug.Log($"[AppsFlyer] DeepLink campaign: {deepLinkArgs.getCampaign()}");
                    OnDeepLinkProcessed?.Invoke(parameters);
                }
            }
            else
            {
                Debug.LogWarning("[AppsFlyer] DeepLink not found or status error.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AppsFlyer] DeepLink parse error: {ex.Message}");
        }
    }

    public void TrackEvent(string eventName, Dictionary<string, string> eventValues)
    {
        if (!isInitialized || !IsEnabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(eventName))
        {
            Debug.LogWarning("[AppsFlyer] TrackEvent: eventName is null or empty.");
            return;
        }

        try
        {
            AppsFlyer.sendEvent(eventName, eventValues ?? new Dictionary<string, string>());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AppsFlyer] TrackEvent failed: {ex.Message}");
        }
    }

    private void UpdateUninstallToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[AppsFlyer] Token is empty. Uninstall tracking skipped.");
            return;
        }

#if UNITY_IOS && !UNITY_EDITOR
            // _ToDo: Provide byte array for iOS uninstall registration from Unity.Notifications.iOS
            Debug.Log("[AppsFlyer] iOS uninstall token registration requires byte[]. Skipped.");
#elif UNITY_ANDROID && !UNITY_EDITOR
            AppsFlyer.updateServerUninstallToken(token);
            Debug.Log("[AppsFlyer] Android uninstall token registered.");
#endif
    }

    #region IAppsFlyerConversionData Implementation

    public void onConversionDataSuccess(string conversionData)
    {
        Debug.Log($"[AppsFlyer] Conversion Data Success: {conversionData}");
    }

    public void onConversionDataFail(string error)
    {
        Debug.LogError($"[AppsFlyer] Conversion Data Failed: {error}");
    }

    public void onAppOpenAttribution(string attributionData)
    {
        Debug.Log($"[AppsFlyer] App Open Attribution: {attributionData}");
    }

    public void onAppOpenAttributionFailure(string error)
    {
        Debug.LogError($"[AppsFlyer] App Open Attribution Failed: {error}");
    }

    #endregion

    private void OnDestroy()
    {
        if (hasDeepLinkRegistered)
        {
            AppsFlyer.OnDeepLinkReceived -= HandleDeepLinkReceived;
            hasDeepLinkRegistered = false;
        }

        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }
}