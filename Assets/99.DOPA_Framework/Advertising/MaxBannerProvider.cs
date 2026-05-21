using System;
using System.Collections;
using UnityEngine;

public class MaxBannerProvider : MonoBehaviour, IBannerProvider
{
    public string ProviderName => "MaxBanner";
    public AdType GetAdType => AdType.Banner;

    [SerializeField] private string adUnitId = "";
    public string AdUnitId
    {
        get => adUnitId;
        set => adUnitId = value;
    }

    [SerializeField] private bool isInitialized;
    private bool isBannerShowing;
    public bool IsBannerShowing => isBannerShowing;

    private float currentBannerHeightPixels;

    public IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[MaxBanner] Initialize Start");
        progressCallback?.Invoke(0f);
        AdUnitId = SDKManager.Instance.GetProxy<MaxSDKManager>().GetAdUnitId(GetAdType);



        // Banner 콜백 구독
        MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdFailedEvent;
        MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClickedEvent;
        MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;

        // Banner 설정 (Adaptive Banner 기본 활성화)
        MaxSdkBase.AdViewConfiguration adViewConfiguration =
            new MaxSdkBase.AdViewConfiguration(MaxSdkBase.AdViewPosition.BottomCenter);

        MaxSdk.CreateBanner(AdUnitId, adViewConfiguration);
        MaxSdk.SetBannerBackgroundColor(AdUnitId, Color.white);

        isInitialized = true;
        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
        yield return null;
    }



    private IEnumerator WaitIapInitialized()
    {
        while (!IAPManager.Instance.IsValidateReceiptReLoad)
        {
            yield return null;
        }

        yield return null;

        Debug.Log("MaxSDKManager Now Initializing Iap...");
        InitializeBannerAds();
    }

    public void InitializeBannerAds()
    {
        if (IAPManager.Instance != null)
        {
            if (!IAPManager.Instance.IsValidateReceiptReLoad)
            {
                StartCoroutine(WaitIapInitialized());
                return;
            }
        }

        if (UserDataManager.Instance.IsAdsRemoved.Value) return;

        MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdFailedEvent;
        MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClickedEvent;
        MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;

        // Banner 설정 (Adaptive Banner 기본 활성화)
        MaxSdkBase.AdViewConfiguration adViewConfiguration =
            new MaxSdkBase.AdViewConfiguration(MaxSdkBase.AdViewPosition.BottomCenter);

        MaxSdk.CreateBanner(AdUnitId, adViewConfiguration);
        MaxSdk.SetBannerBackgroundColor(AdUnitId, Color.white);
    }



    private float lastReportedBannerHeight = -1f;
    private bool isHeightUpdatePending = false;

    public void ShowBanner()
    {
        if (isBannerShowing || !isInitialized) return;

        isBannerShowing = true;
        MaxSdk.ShowBanner(AdUnitId);
        Debug.Log("[MaxBanner] Banner shown");

        StartCoroutine(DelayedHeightUpdate());
    }

    private IEnumerator DelayedHeightUpdate()
    {
        yield return null;
        yield return null;
        UpdateSafeAreaWithBannerHeight();
    }

    public void HideBanner()
    {
        if (!isBannerShowing)
        {
            return;
        }

        isBannerShowing = false;
        MaxSdk.HideBanner(AdUnitId);

        ReportBannerHeightToSafeArea(0f);
        Debug.Log("[MaxBanner] Banner hidden");
    }

    public void ToggleBanner()
    {
        if (isBannerShowing)
        {
            HideBanner();
        }
        else
        {
            ShowBanner();
        }
    }

    private void OnBannerAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxBanner] Banner ad loaded");

        if (isBannerShowing)
        {
            UpdateSafeAreaWithBannerHeight();
        }

        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Banner, ADSRESULT.SUCCESS, adInfo);
    }

    private void OnBannerAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        Debug.LogError($"[MaxBanner] Load failed: {errorInfo.Code}");
        ReportBannerHeightToSafeArea(0f);
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Banner, ADSRESULT.FAIL, null);
    }

    private void OnBannerAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxBanner] Banner clicked");
        AnalyticsManager.Instance.Log("ad_bn");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdClickEvent(AdType.Banner, adInfo);
    }

    private void OnBannerAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log($"[MaxBanner] Revenue paid: ${adInfo.Revenue:F4} (Network: {adInfo.NetworkName})");
        SDKManager.Instance.GetProxy<MaxSDKManager>().TrackAdRevenue(adInfo, AdType.Banner);
    }

    private void ReportBannerHeightToSafeArea(float newHeight)
    {
        if (Mathf.Abs(lastReportedBannerHeight - newHeight) > 0.1f)
        {
            lastReportedBannerHeight = newHeight;

            EnhancedSafeAreaRegister.Instance.UpdateBannerHeight(newHeight);
            Debug.Log($"[MaxBanner] SafeArea updated: {newHeight:F0}px");
        }
    }

    [SerializeField] private float extraBannerPaddingPixels = 0f;

    /// <summary>
    /// Adaptive Banner Calculate Height + Padding
    /// </summary>
    private void UpdateSafeAreaWithBannerHeight()
    {
        float h = CalculateMaxBannerHeightPixels();
        h += extraBannerPaddingPixels;

        ReportBannerHeightToSafeArea(h);

        Debug.Log($"[MaxBanner] Banner UpdateSafeAreaWithBannerHeight currentBannerHeightPixels == {currentBannerHeightPixels}");
    }

    /// <summary>
    /// MAX SDK Adaptive Banner Calculate Height (dp -> pixels)
    /// </summary>
    private static float CalculateMaxBannerHeightPixels()
    {
        float adaptiveHeightDp = MaxSdkUtils.GetAdaptiveBannerHeight();
        if (adaptiveHeightDp <= 0f)
        {
            adaptiveHeightDp = MaxSdkUtils.IsTablet() ? 90f : 50f;
        }

        float density = GetPlatformDensity();
        float pixels = Mathf.Ceil(adaptiveHeightDp * density);

        Debug.Log($"[MaxBanner] adaptiveDp={adaptiveHeightDp}, density={density:F3}, pixels={pixels:F0}px");
        return pixels;
    }

    private static float GetPlatformDensity()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var resources = activity.Call<AndroidJavaObject>("getResources"))
        using (var displayMetrics = resources.Call<AndroidJavaObject>("getDisplayMetrics"))
        {
            float density = displayMetrics.Get<float>("density");
            if (density <= 0f)
            {
                return 1f;
            }
            return density;
        }
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[MaxBanner] GetPlatformDensity Android failed: {e.Message}");
        float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
        return dpi / 160f;
    }
#else
        float dpiEditorOrIos = Screen.dpi > 0f ? Screen.dpi : 160f;
        return dpiEditorOrIos / 160f;
#endif
    }

    /// <summary>
    /// dp to pixels
    /// </summary>
    private static float DpToPixels(float dp)
    {
        float dpi = Screen.dpi;
        if (dpi <= 0f)
        {
            dpi = 160f;
        }
        return dp * (dpi / 160f);
    }

    private void OnDestroy()
    {
        MaxSdkCallbacks.Banner.OnAdLoadedEvent -= OnBannerAdLoadedEvent;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= OnBannerAdFailedEvent;
        MaxSdkCallbacks.Banner.OnAdClickedEvent -= OnBannerAdClickedEvent;
        MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= OnBannerAdRevenuePaidEvent;

        if (isBannerShowing)
        {
            HideBanner();
        }
    }
}