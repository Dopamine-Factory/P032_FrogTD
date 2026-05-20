using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.RemoteConfig;

public class RemoteConfigManager : BaseSystemManager<RemoteConfigManager>
{
    private const string MIN_APP_VERSION_KEY = "min_app_version";
    private const string MIN_BUNDLE_VERSION_KEY = "min_bundle_version";
    private const string LATEST_APP_VERSION_KEY = "latest_app_version";
    private const string FORCE_UPDATE_REQUIRED_KEY = "force_update_required";
    private const string AD_INTERSTITIAL_START_LEVEL_KEY = "ad_interstitial_start_level";
    private const string AD_INTERSTITIAL_START_SECOND_KEY = "ad_interstitial_start_second";
    private const string AD_INTERSTITIAL_INTERVAL_LEVEL_KEY = "ad_interstitial_interval_level";
    private const string AD_INTERSTITIAL_INTERVAL_SECOND_KEY = "ad_interstitial_interval_second";
    private const string AD_INTERSTITIAL_ON_KEY = "ad_interstitial_on";

#if UNITY_ANDROID
    private const string REVIEW_VERSION_KEY = "aos_review_version";
#elif UNITY_IOS
    private const string REVIEW_VERSION_KEY = "ios_review_version";
#endif
    private const string TIME_A = "time_a";

    private const string PRODUCT_SETTING_AD_REMOVE_TYPE = "product_setting_ad_remove_type";

    public int adInterstitialStartLevel = 90018;
    public int adInterstitialStartSecond = -1;
    public int adInterstitialIntervalLevel = -1;
    public int adInterstitialIntervalSecond = 420;
    public bool ad_interstitial_on = true;

    public int productSettingAdRemoveType = -1;

    public string LatestVersionString { get; private set; }
    public string ReviewVersionString { get; private set; }
    public bool IsForceUpdate { get; private set; }
    public string minAppVersion { get; private set; }
    public int minBundleVersion { get; private set; }

    private bool isRemoteInitialized = false;
    public bool GetIsRemoteInitialized { get => isRemoteInitialized; set => isRemoteInitialized = value; }

    public bool HasValidRemoteConfig { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        HasValidRemoteConfig = false;

#if UNITY_EDITOR
        StartCoroutine(InitializeVersionManagerCheckAsync());
#else
        StartCoroutine(InitializeFlow(null));
#endif
    }

    private void ValueInitialize()
    {
        IsForceUpdate = false;
        minAppVersion = VersionManager.Instance.VersionString;
        minBundleVersion = VersionManager.Instance.BundleVersionInt;
        LatestVersionString = VersionManager.Instance.VersionString;
        ReviewVersionString = "0";

        HasValidRemoteConfig = false;
        isRemoteInitialized = true;
    }

#if UNITY_EDITOR
    private IEnumerator InitializeVersionManagerCheckAsync()
    {
        while (VersionManager.Instance.VersionString == null)
            yield return null;

        ValueInitialize();
    }
#endif

    public IEnumerator InitializeFlow(Action<bool> onComplete)
    {
        Debug.Log("[RemoteConfigManager] InitializeFlow Start");

        if (isRemoteInitialized)
        {
            onComplete?.Invoke(true);
            yield break;
        }

        if (!IsEnabled)
        {
            ValueInitialize();

            onComplete?.Invoke(true);
            yield break;
        }

        yield return StartCoroutine(InitializeFirebaseAndCheck());

        Debug.Log("[RemoteConfigManager] Initialization complete.");
        onComplete?.Invoke(true);
    }

    private IEnumerator InitializeFirebaseAndCheck()
    {
        Debug.Log("~#~#~#~#~#~#~#~#~#~#~#~# [RemoteConfigManager] InitializeFirebaseAndCheck Start");

        var checkTask = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => checkTask.IsCompleted);

        if (checkTask.Result != DependencyStatus.Available)
        {
            Debug.Log("~#~#~#~#~#~#~#~#~#~#~#~# [RemoteConfigManager] InitializeFirebaseAndCheck Firebase Init Fail");

            ValueInitialize();

            yield break;
        }

        var defaults = new Dictionary<string, object>
        {
            { MIN_APP_VERSION_KEY, VersionManager.Instance.VersionString },
            { MIN_BUNDLE_VERSION_KEY, VersionManager.Instance.BundleVersionInt },
            { LATEST_APP_VERSION_KEY, VersionManager.Instance.VersionString },
            { REVIEW_VERSION_KEY, "0" },
            { FORCE_UPDATE_REQUIRED_KEY, false },
            { AD_INTERSTITIAL_START_LEVEL_KEY, 90018 },
            { AD_INTERSTITIAL_START_SECOND_KEY, -1 },
            { AD_INTERSTITIAL_INTERVAL_LEVEL_KEY, -1 },
            { AD_INTERSTITIAL_INTERVAL_SECOND_KEY, 420 },
            { AD_INTERSTITIAL_ON_KEY, true },
            { PRODUCT_SETTING_AD_REMOVE_TYPE, -1 }
        };

        var setDefaultsTask = FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults);
        yield return new WaitUntil(() => setDefaultsTask.IsCompleted);

        Debug.Log("~#~#~#~#~#~#~#~#~#~#~#~# [RemoteConfigManager] InitializeFirebaseAndCheck IsCompleted");

        yield return StartCoroutine(FetchRemoteConfig());
    }

    private IEnumerator FetchRemoteConfig()
    {

        var fetchTask = FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
        yield return new WaitUntil(() => fetchTask.IsCompleted);

        if (fetchTask.IsFaulted || fetchTask.IsCanceled)
        {
            ValueInitialize();

            yield break;
        }

        var activateTask = FirebaseRemoteConfig.DefaultInstance.ActivateAsync();
        yield return new WaitUntil(() => activateTask.IsCompleted);

        if (activateTask.IsFaulted || activateTask.IsCanceled)
        {

            ValueInitialize();

            yield break;
        }

        CheckVersionCompatibility();

        yield return new WaitForEndOfFrame();
    }

    private void CheckVersionCompatibility()
    {
        try
        {
            var remoteConfig = FirebaseRemoteConfig.DefaultInstance;

            adInterstitialStartLevel = (int)remoteConfig.GetValue(AD_INTERSTITIAL_START_LEVEL_KEY).LongValue;
            adInterstitialStartSecond = (int)remoteConfig.GetValue(AD_INTERSTITIAL_START_SECOND_KEY).LongValue;
            adInterstitialIntervalLevel = (int)remoteConfig.GetValue(AD_INTERSTITIAL_INTERVAL_LEVEL_KEY).LongValue;
            adInterstitialIntervalSecond = (int)remoteConfig.GetValue(AD_INTERSTITIAL_INTERVAL_SECOND_KEY).LongValue;

            ad_interstitial_on = remoteConfig.GetValue(AD_INTERSTITIAL_ON_KEY).BooleanValue;

            productSettingAdRemoveType = (int)remoteConfig.GetValue(PRODUCT_SETTING_AD_REMOVE_TYPE).LongValue;

            if (adInterstitialStartLevel > 0) ConfigManager.IntistialShowLevel.Value = (ushort)adInterstitialStartLevel;
            if (adInterstitialStartSecond >= 0) ConfigManager.IntistialShowSecond.Value = (ushort)adInterstitialStartSecond;
            if (adInterstitialIntervalLevel >= 0) ConfigManager.IntistialIntervalLevel.Value = (ushort)adInterstitialIntervalLevel;
            if (adInterstitialIntervalSecond >= 0) ConfigManager.IntistialIntervalSecond.Value = (ushort)adInterstitialIntervalSecond;

            ConfigManager.InterstitialAdOn.Value = ad_interstitial_on;
            ConfigManager.AdRemoveItemType.Value = productSettingAdRemoveType;

            LatestVersionString = remoteConfig.GetValue(LATEST_APP_VERSION_KEY).StringValue;
            IsForceUpdate = remoteConfig.GetValue(FORCE_UPDATE_REQUIRED_KEY).BooleanValue;

            ReviewVersionString = remoteConfig.GetValue(REVIEW_VERSION_KEY).StringValue;

            minAppVersion = remoteConfig.GetValue(MIN_APP_VERSION_KEY).StringValue;
            minBundleVersion = (int)remoteConfig.GetValue(MIN_BUNDLE_VERSION_KEY).LongValue;

            HasValidRemoteConfig = true;
            isRemoteInitialized = true;
        }
        catch (Exception e)
        {
            ValueInitialize();

            Debug.LogError("[RemoteConfigManager] CheckVersionCompatibility Exception: " + e);
        }
        finally
        {
            isRemoteInitialized = true;
        }
    }
}