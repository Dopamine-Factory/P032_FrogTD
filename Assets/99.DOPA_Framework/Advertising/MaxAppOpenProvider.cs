using System;
using System.Collections;
using UnityEngine;

public class MaxAppOpenProvider : MonoBehaviour, IAdProvider
{
    public string ProviderName => "MaxAppOpen";
    public AdType GetAdType => AdType.AppOpen;
    public string AdUnitId { get => adUnitId; set => adUnitId = value; }
    [SerializeField] string adUnitId = "";
    [SerializeField] private bool isInitialized;
    private bool isShowingAd;

    public bool IsReady()
    {
        if (!MaxSdk.IsInitialized())
        {
            StartCoroutine(SDKManager.Instance.GetProxy<MaxSDKManager>().Initialize(null, null));
            return false;
        }

        return MaxSdk.IsAppOpenAdReady(AdUnitId);
    }

    public bool CanShowAd() => IsReady() && !isShowingAd;
    public bool IsShowing => isShowingAd;

    public void TryShowAd(Action<ADSRESULT> callback)
    {
        if (CanShowAd())
        {
            MaxSdk.ShowAppOpenAd(AdUnitId);
            isShowingAd = true;
            Debug.Log("[MaxAppOpen] Showing ad");
            callback?.Invoke(ADSRESULT.SUCCESS);
        }
        else
        {
            Debug.LogWarning("[MaxAppOpen] Cannot show ad - not ready or already showing");
            callback?.Invoke(ADSRESULT.NOTREADY);
        }
    }

    public IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[MaxAppOpen] Initialize Start");
        isInitialized = false;
        progressCallback?.Invoke(0f);
        AdUnitId = SDKManager.Instance.GetProxy<MaxSDKManager>().GetAdUnitId(GetAdType);

        MaxSdk.SetAppOpenAdExtraParameter(AdUnitId, "mute_audio", "false");

        MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += OnAdLoadedEvent;
        MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += OnAdLoadFailedEvent;
        MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += OnAdDisplayedEvent;
        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAdHiddenEvent;
        MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;

        MaxSdk.LoadAppOpenAd(AdUnitId);

        float timeout = Time.time + 0.1f;
        while (!isInitialized && Time.time < timeout)
        {
            yield return null;
        }

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);

        yield return null;
    }

    private void OnAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxAppOpen] Ad loaded successfully");
        isInitialized = true;
    }

    private void OnAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        Debug.LogError($"[MaxAppOpen] Load failed: {errorInfo.Code} - {errorInfo.Message}");
        isInitialized = true;
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.AppOpen, ADSRESULT.FAIL, null);
    }

    private void OnAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxAppOpen] Ad displayed");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.AppOpen, ADSRESULT.SUCCESS, adInfo);
    }

    private void OnAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxAppOpen] Ad hidden");
        isShowingAd = false;
    }

    private void OnAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log($"[MaxAppOpen] Revenue paid: ${adInfo.Revenue:F4} (Network: {adInfo.NetworkName})");
        SDKManager.Instance.GetProxy<MaxSDKManager>().TrackAdRevenue(adInfo, AdType.AppOpen);
    }

    private void OnDestroy()
    {
        MaxSdkCallbacks.AppOpen.OnAdLoadedEvent -= OnAdLoadedEvent;
        MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent -= OnAdLoadFailedEvent;
        MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent -= OnAdDisplayedEvent;
        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= OnAdHiddenEvent;
        MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent -= OnAdRevenuePaidEvent;
    }
}