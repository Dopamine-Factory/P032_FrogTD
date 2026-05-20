using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AppsFlyerSDK;
using UnityEngine;

public class MaxSDKManager : BaseSDKManager
{
#if UNITY_IOS
/* iOS 광고 유닛 세팅 */
    private const string AppopenAdUnitId = "404a30fae5841b61";
    private const string InterstitialAdUnitId = "bcb1f704cb497551";
    private const string RewardedAdUnitId = "2fdb4c1915b84d06";
    private const string BannerAdUnitId = "1161159dc6beca7c";
    private const string MRecAdUnitId = "ENTER_IOS_MREC_AD_UNIT_ID_HERE";
#elif UNITY_ANDROID
    /* ANDROID 광고 유닛 세팅 */
    private const string AppopenAdUnitId = "53179235b2f79433";
    private const string InterstitialAdUnitId = "bcb1f704cb497551";
    private const string RewardedAdUnitId = "2fdb4c1915b84d06";
    private const string BannerAdUnitId = "1161159dc6beca7c";
    private const string MRecAdUnitId = "ENTER_ANDROID_MREC_AD_UNIT_ID_HERE";
#else
/* 그 외 광고 유닛 세팅 -> 현재 그 외 플랫폼은 상정하지 않는다. */
    private const string AppopenAdUnitId = "53179235b2f79433";
    private const string InterstitialAdUnitId = "bcb1f704cb497551";
    private const string RewardedAdUnitId = "2fdb4c1915b84d06";
    private const string BannerAdUnitId = "1161159dc6beca7c";
    private const string MRecAdUnitId = "ENTER_ANDROID_MREC_AD_UNIT_ID_HERE";
#endif
    [Header("Ad Providers")]
    [SerializeField] private MonoBehaviour[] providerBehaviours;

    private readonly List<IAdProvider> adProviders = new List<IAdProvider>();
    private readonly List<IBannerProvider> bannerProviders = new List<IBannerProvider>();

    public override IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[MaxSDKManager] Initialize Start");

        CacheProviders();

        int totalSteps = 1 + adProviders.Count + bannerProviders.Count;
        int currentStepIndex = 0;

        bool initSuccess = false;

        yield return InitializeMaxSdk(
            progress =>
            {
                float overall = CalculateOverallProgress(currentStepIndex, totalSteps, progress);
                progressCallback?.Invoke(overall);
            },
            success =>
            {
                initSuccess = success;
            }
        );

        if (!initSuccess)
        {
            Debug.LogError("[MaxSDKManager] MAX SDK initialization failed");
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(false);
            yield break;
        }

        currentStepIndex++;

        for (int i = 0; i < adProviders.Count; i++)
        {
            IAdProvider provider = adProviders[i];
            bool providerSuccess = false;

            Debug.Log($"[MaxSDKManager] Initializing Provider: {provider.ProviderName}");

            yield return provider.Initialize(
                progress =>
                {
                    float overall = CalculateOverallProgress(currentStepIndex, totalSteps, progress);
                    progressCallback?.Invoke(overall);
                },
                success =>
                {
                    providerSuccess = success;
                }
            );

            if (!providerSuccess)
            {
                Debug.LogError($"[MaxSDKManager] Provider initialization failed: {provider.ProviderName}");
                progressCallback?.Invoke(1f);
                onComplete?.Invoke(false);
                yield break;
            }

            currentStepIndex++;
        }

        for (int i = 0; i < bannerProviders.Count; i++)
        {
            IBannerProvider bannerProvider = bannerProviders[i];
            bool providerSuccess = false;
            Debug.Log($"[MaxSDKManager] Initializing Banner: {bannerProvider.ProviderName}");
            yield return bannerProvider.Initialize(progress =>
            {
                float overall = CalculateOverallProgress(currentStepIndex, totalSteps, progress);
                progressCallback?.Invoke(overall);
            }, success => providerSuccess = success);
            currentStepIndex++;

            if (!providerSuccess)
            {
                Debug.LogError($"[MaxSDKManager] Banner initialization failed: {bannerProvider.ProviderName}");
                progressCallback?.Invoke(1f);
                onComplete?.Invoke(false);
                yield break;
            }
        }

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
    }

    private void CacheProviders()
    {
        adProviders.Clear();
        bannerProviders.Clear();  // 추가!

        if (providerBehaviours == null || providerBehaviours.Length == 0)
        {
            Debug.LogWarning("[MaxSDKManager] No provider behaviours assigned");
            return;
        }

        for (int i = 0; i < providerBehaviours.Length; i++)
        {
            if (providerBehaviours[i] is IAdProvider adProvider)
            {
                adProviders.Add(adProvider);
                Debug.Log($"[MaxSDKManager] Added IAdProvider: {adProvider.ProviderName}");
            }
            else if (providerBehaviours[i] is IBannerProvider bannerProvider)
            {
                bannerProviders.Add(bannerProvider);  // 추가!
                Debug.Log($"[MaxSDKManager] Added IBannerProvider: {bannerProvider.ProviderName}");
            }
            else if (providerBehaviours[i] != null)
            {
                Debug.LogWarning($"[MaxSDKManager] Unknown behaviour: {providerBehaviours[i].name}");
            }
        }

        Debug.Log($"[MaxSDKManager] IAdProviders: {adProviders.Count}, IBannerProviders: {bannerProviders.Count}");
    }


    private IEnumerator InitializeMaxSdk(Action<float> progressCallback, Action<bool> onComplete)
    {
        bool isInitDone = false;
        bool isSuccess = false;

        progressCallback?.Invoke(0f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string[] testDeviceIds = new string[]
        {
#if DEBUG
            GetAndroidAdvertiserId(),
#endif
            "0768e99a-a119-4d2a-aabf-656f94e48363",
            "a46e6ad8-fbb3-431e-9ef1-d0ae47cb1fca",
            "bb0199db-69b2-467c-813a-196a0f7e4bbe",
            "c04a0fb9-ff1e-458e-a639-185094104bdb",
            "09654a01-25a4-43f5-9c0b-fd6fd97fa456"
        };

        MaxSdk.SetTestDeviceAdvertisingIdentifiers(testDeviceIds);
#endif
        void OnSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            Debug.Log("[MaxSDKManager] MAX SDK initialized");
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            isInitDone = true;
            isSuccess = true;
        }

        MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;

        MaxSdk.InitializeSdk();

        float timeout = Time.realtimeSinceStartup + 3f;
        while (!isInitDone && Time.realtimeSinceStartup < timeout)
        {
            progressCallback?.Invoke(0.5f);
            yield return null;
        }

        if (!isInitDone)
        {
            Debug.LogError("[MaxSDKManager] MAX SDK initialization timeout");
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            isSuccess = false;
        }

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(isSuccess);
    }

    private float CalculateOverallProgress(int stepIndex, int totalSteps, float localProgress)
    {
        float stepSize = 1f / totalSteps;
        float currentBase = stepSize * stepIndex;
        return currentBase + stepSize * Mathf.Clamp01(localProgress);
    }

    #region Legacy Compatibility
    public bool CanShowAppopen() => GetProvider(AdType.AppOpen)?.CanShowAd() ?? false;
    public void ShowAdIfReady() => GetProvider(AdType.AppOpen)?.TryShowAd(null);
    public bool CanShowInterstitial() => GetProvider(AdType.Interstitial)?.CanShowAd() ?? false;
    public void TryShowInterstitial(Action<ADSRESULT> callback) => GetProvider(AdType.Interstitial)?.TryShowAd(callback);
    public bool IsRewardedAdReady() => GetProvider(AdType.Rewarded)?.IsReady() ?? false;
    public void ShowRewardedAd(Action<ADSRESULT> callback) => GetProvider(AdType.Rewarded)?.TryShowAd(callback);

    public IBannerProvider GetBannerProvider(AdType type) =>
        bannerProviders.FirstOrDefault(p => p.GetAdType == type);

    public IAdProvider GetProvider(AdType type) =>
        adProviders.FirstOrDefault(p => p.GetAdType == type);
    #endregion

    #region Ad Event Analytics

    public void TrackAdRevenue(MaxSdkBase.AdInfo adInfo, AdType type)
    {
        SendAdRevenueEvent(adInfo.Revenue, MaxSdk.GetSdkConfiguration().CountryCode, adInfo.NetworkName, type, adInfo.AdUnitIdentifier);
    }
    public void SendAdClickEvent(AdType type, MaxSdkBase.AdInfo adInfo)
    {
        var eventValues = new Dictionary<string, object>
        {
            { "af_ad_type", type.ToString() },
            { "af_ad_unit_id", GetAdUnitId(type) },
            { "af_network", adInfo.NetworkName ?? "Unknown" },
        };

        AnalyticsManager.Instance.EventLog("af_ad_click", eventValues);
        Debug.Log($"[MaxSDKManager] af_ad_click sent: {type}, {adInfo.NetworkName}");
    }

    public string GetAdUnitId(AdType type)
    {
        switch (type)
        {
            case AdType.Banner: return BannerAdUnitId;
            case AdType.Interstitial: return InterstitialAdUnitId;
            case AdType.Rewarded: return RewardedAdUnitId;
            case AdType.AppOpen: return AppopenAdUnitId;
            case AdType.MREC: return MRecAdUnitId;

            default: return "";
        }
    }

    public void SendAdViewEvent(AdType type, ADSRESULT result, MaxSdkBase.AdInfo adInfo = null)
    {
        string networkName = adInfo != null ? adInfo.NetworkName ?? "Unknown" : "Unknown";

        var eventValues = new Dictionary<string, object>
        {
            { "af_ad_type", type.ToString() },
            { "af_ad_unit_id", GetAdUnitId(type) },
            { "af_result", result.ToString() },
            { "af_network", networkName },
        };

        AnalyticsManager.Instance.EventLog("af_ad_view", eventValues);
        Debug.Log($"[MaxSDKManager] af_ad_view sent: {type}, {result}, {adInfo?.NetworkName ?? "Unknown"}");
    }

    void SendAdRevenueEvent(double revenue, string country, string network, AdType type, string adUnitIdentifier)
    {
        var eventValues = new Dictionary<string, object>
        {
            { AdRevenueScheme.AD_TYPE, ConvertAdTypeForAppsFlyer(type) },
            { AFInAppEvents.REVENUE, revenue },
            { AFInAppEvents.CURRENCY, "USD" },
            { "af_network", network },
            { "af_mediationNetwork", MediationNetwork.ApplovinMax },
            { AdRevenueScheme.AD_UNIT, adUnitIdentifier },
            { AdRevenueScheme.COUNTRY, country },
            { AdRevenueScheme.PLACEMENT, "Lobby" },
            //{ "version", Application.version}
        };

        var eventValues_log = new Dictionary<string, object>
        {
            { "ad_platform", "AppLovin" },
            { "ad_source", network },
            { "ad_format", ConvertAdFormatForGa4(type)},
            { "ad_unit_name", adUnitIdentifier },
            { "currency", "USD" },
            { "value", revenue }
        };

        AnalyticsManager.Instance.EventLog("af_ad_revenue", eventValues);
        AnalyticsManager.Instance.Log("ad_impression", eventValues_log);
        Debug.Log($"[MaxSDKManager] af_ad_revenue sent: {revenue}, {country}, {network}");
    }
    #endregion

#if DEBUG
    public static string GetAndroidAdvertiserId()
    {
        string advertisingID = "";
        try
        {
            AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaClass client = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
            AndroidJavaObject adInfo = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", currentActivity);
            advertisingID = adInfo.Call<string>("getId").ToString();
            Debug.Log("advertisingID " + advertisingID);
        }
        catch (Exception e)
        {
            Debug.Log("advertisingID error " + e.Message);
        }
        return advertisingID;
    }
#endif


    private string ConvertAdTypeForAppsFlyer(AdType type)
    {
        return type switch
        {
            AdType.Banner => "Banner",
            AdType.Interstitial => "Interstitial",
            AdType.Rewarded => "Rewarded",
            AdType.MREC => "MRec",
            AdType.AppOpen => "AppOpen",
            _ => "Unknown"
        };
    }

    private string ConvertAdFormatForGa4(AdType type)
    {
        return type switch
        {
            AdType.Banner => "banner",
            AdType.Interstitial => "interstitial",
            AdType.Rewarded => "rewarded",
            AdType.MREC => "mrec",
            AdType.AppOpen => "app_open",
            _ => "unknown"
        };
    }
}