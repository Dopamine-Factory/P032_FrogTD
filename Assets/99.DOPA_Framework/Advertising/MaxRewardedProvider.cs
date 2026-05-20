using System;
using System.Collections;
using UnityEngine;

public class MaxRewardedProvider : MonoBehaviour, IAdProvider
{
    public string ProviderName => "MaxRewarded";
    public AdType GetAdType => AdType.Rewarded;
    public string AdUnitId { get => adUnitId; set => adUnitId = value; }
    [SerializeField] string adUnitId = "";

    [SerializeField] private bool isInitialized;
    private Action<ADSRESULT> rewardedCallback;
    private int rewardedRetryAttempt;

    
    
    public bool IsReady()
    {
        if (!MaxSdk.IsInitialized())
        {
            StartCoroutine(SDKManager.Instance.GetProxy<MaxSDKManager>().Initialize(null, null));
            return false;
        }

        return MaxSdk.IsRewardedAdReady(AdUnitId);
    }

    public bool CanShowAd() => IsReady(); // Rewarded는 단순히 준비상태만 확인
    public bool IsShowing => rewardedCallback != null;

    public IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[MaxRewarded] Initialize Start");
        isInitialized = false;
        rewardedCallback = null;

        progressCallback?.Invoke(0f);

        AdUnitId = SDKManager.Instance.GetProxy<MaxSDKManager>().GetAdUnitId(GetAdType);

        MaxSdk.SetRewardedAdExtraParameter(AdUnitId, "mute_audio", "false");

        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnAdLoadedEvent;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnAdLoadFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnAdDisplayFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnAdDisplayedEvent;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnAdClickedEvent;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnAdHiddenEvent;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnAdReceivedRewardEvent;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;

        LoadRewardedAd();

        float timeout = Time.time + 0.1f;
        while (!isInitialized && Time.time < timeout)
        {
            yield return null;
        }

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
        yield return null;
    }
    private void LoadRewardedAd()
    {
        MaxSdk.LoadRewardedAd(AdUnitId);
    }

    public void TryShowAd(Action<ADSRESULT> callback)
    {
        rewardedCallback = callback;

        if (CanShowAd())
        {
            MaxSdk.ShowRewardedAd(AdUnitId);
            Debug.Log("[MaxRewarded] Showing rewarded ad");
        }
        else
        {
            LoadRewardedAd();
            Debug.LogWarning("[MaxRewarded] Rewarded not ready, loading...");
            InvokeRewardedCallback(ADSRESULT.NOTREADY);
        }
    }

    private void InvokeRewardedCallback(ADSRESULT result)
    {
        rewardedCallback?.Invoke(result);
        rewardedCallback = null;
    }

    private void OnAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxRewarded] Ad loaded successfully");
        rewardedRetryAttempt = 0;

        if (!isInitialized) isInitialized = true;
    }

    private void OnAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        rewardedRetryAttempt++;
        float retryDelay = Mathf.Pow(2, Mathf.Min(6, rewardedRetryAttempt));
        Debug.LogError($"[MaxRewarded] Load failed (attempt {rewardedRetryAttempt}): {errorInfo.Code}");

        if (!isInitialized)
        {
            isInitialized = true;
            SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Rewarded, ADSRESULT.FAIL, null);
        }

        Invoke(nameof(LoadRewardedAd), retryDelay);
    }

    private void OnAdDisplayFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        Debug.LogError($"[MaxRewarded] Display failed: {errorInfo.Code}");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Rewarded, ADSRESULT.FAIL, adInfo);

        LoadRewardedAd();

        InvokeRewardedCallback(ADSRESULT.NOTREADY);
    }

    private void OnAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxRewarded] Ad displayed");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Rewarded, ADSRESULT.SUCCESS, adInfo);
    }

    private void OnAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxRewarded] Ad clicked");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdClickEvent(AdType.Rewarded, adInfo);
    }

    private void OnAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxRewarded] Ad hidden - Preloading next");
        LoadRewardedAd();

        InvokeRewardedCallback(ADSRESULT.CANCEL);
    }

    private void OnAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log($"[MaxRewarded] Reward received: {reward.Label} x{reward.Amount}");
        InvokeRewardedCallback(ADSRESULT.SUCCESS);
    }

    private void OnAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log($"[MaxRewarded] Revenue paid: ${adInfo.Revenue:F4} (Network: {adInfo.NetworkName})");
        SDKManager.Instance.GetProxy<MaxSDKManager>().TrackAdRevenue(adInfo, AdType.Rewarded);
    }

    private void OnDestroy()
    {
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnAdLoadedEvent;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnAdLoadFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnAdDisplayFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= OnAdDisplayedEvent;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent -= OnAdClickedEvent;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnAdHiddenEvent;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnAdReceivedRewardEvent;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= OnAdRevenuePaidEvent;
    }
}