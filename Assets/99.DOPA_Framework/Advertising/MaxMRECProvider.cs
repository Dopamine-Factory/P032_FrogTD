using System;
using System.Collections;
using UnityEngine;

public class MaxMRECProvider : MonoBehaviour, IBannerProvider
{
    public string ProviderName => "MaxMREC";
    public AdType GetAdType => AdType.MREC;
    public string AdUnitId { get => adUnitId; set => adUnitId = value; }
    [SerializeField] string adUnitId = "";
    [SerializeField] private bool isInitialized;
    private bool isMRecShowing;

    public bool IsBannerShowing => isMRecShowing;

    public void ShowBanner()
    {
        if (isMRecShowing) return;

        isMRecShowing = true;
        MaxSdk.ShowMRec(AdUnitId);
        Debug.Log("[MaxMREC] MREC shown");
    }

    public void HideBanner()
    {
        if (!isMRecShowing) return;

        isMRecShowing = false;
        MaxSdk.HideMRec(AdUnitId);
        Debug.Log("[MaxMREC] MREC hidden");
    }

    public void ToggleBanner()
    {
        if (isMRecShowing)
        {
            HideBanner();
        }
        else
        {
            ShowBanner();
        }
    }

    public IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[MaxMREC] Initialize Start");
        progressCallback?.Invoke(0f);
        AdUnitId = SDKManager.Instance.GetProxy<MaxSDKManager>().GetAdUnitId(GetAdType);

        MaxSdkCallbacks.MRec.OnAdLoadedEvent += OnMRecAdLoadedEvent;
        MaxSdkCallbacks.MRec.OnAdLoadFailedEvent += OnMRecAdFailedEvent;
        MaxSdkCallbacks.MRec.OnAdClickedEvent += OnMRecAdClickedEvent;
        MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += OnMRecAdRevenuePaidEvent;

        // MREC setting (300x250 size)
        MaxSdkBase.AdViewConfiguration adViewConfiguration =
            new MaxSdkBase.AdViewConfiguration(MaxSdkBase.AdViewPosition.BottomCenter);

        MaxSdk.CreateMRec(AdUnitId, adViewConfiguration);

        isInitialized = true;
        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
        yield return null;
    }

    private void OnMRecAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxMREC] MREC ad loaded successfully");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.MREC, ADSRESULT.SUCCESS, adInfo);
    }

    private void OnMRecAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        Debug.LogError($"[MaxMREC] Load failed: {errorInfo.Code}");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.MREC, ADSRESULT.FAIL, null);
    }

    private void OnMRecAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxMREC] MREC clicked");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdClickEvent(AdType.MREC, adInfo);
    }

    private void OnMRecAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log($"[MaxMREC] Revenue paid: ${adInfo.Revenue:F4} (Network: {adInfo.NetworkName})");
        SDKManager.Instance.GetProxy<MaxSDKManager>().TrackAdRevenue(adInfo, AdType.MREC);
    }

    private void OnDestroy()
    {
        MaxSdkCallbacks.MRec.OnAdLoadedEvent -= OnMRecAdLoadedEvent;
        MaxSdkCallbacks.MRec.OnAdLoadFailedEvent -= OnMRecAdFailedEvent;
        MaxSdkCallbacks.MRec.OnAdClickedEvent -= OnMRecAdClickedEvent;
        MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent -= OnMRecAdRevenuePaidEvent;

        if (isMRecShowing)
        {
            HideBanner();
        }
    }
}