using System;
using System.Collections;
using UnityEngine;

public class MaxInterstitialProvider : MonoBehaviour, IAdProvider
{
    public string ProviderName => "MaxInterstitial";
    public AdType GetAdType => AdType.Interstitial;
    public string AdUnitId { get => adUnitId; set => adUnitId = value; }
    [SerializeField] string adUnitId = "";
    [SerializeField] private bool isInitialized;
    private Action<ADSRESULT> interstitialCallback;
    private int interstitialRetryAttempt;
    private int lastAdShownLevel = -1;
    private float lastAdShownTime = -Mathf.Infinity;

    public bool IsReady()
    {
        if (!MaxSdk.IsInitialized())
        {
            StartCoroutine(SDKManager.Instance.GetProxy<MaxSDKManager>().Initialize(null, null));
            return false;
        }

        return MaxSdk.IsInterstitialReady(AdUnitId);
    }

    
    void LoadInterstitial()
    {
        if (!MaxSdk.IsInitialized())
        {
            StartCoroutine(SDKManager.Instance.GetProxy<MaxSDKManager>().Initialize(null, null));
            return;
        }

        MaxSdk.LoadInterstitial(AdUnitId);
    }

    public bool CanShowAd()
    {
        // SDK 준비 상태 확인
        if (!IsReady()) return false;


        // Config 확인
        if (!ConfigManager.InterstitialAdOn.Value) return false;

        return true;

        // 현재 레벨 확인
        int currentLevel = UserDataManager.Instance.CurrentLevel.Value;
        if (currentLevel < ConfigManager.IntistialShowLevel.Value) return false;

        // 플레이 시간 확인
        float activeTime = 1000000.0f;

        if (TimeEventManager.Instance != null)
        {
            activeTime = TimeEventManager.Instance.GetInGameActiveTime();
        }
        else
        {
            activeTime = Time.realtimeSinceStartup;
        }

        if (ConfigManager.IntistialShowSecond.Value > 0 && activeTime < ConfigManager.IntistialShowSecond.Value)
            return false;

        // 레벨 간격 확인
        if (lastAdShownLevel >= 0 && ConfigManager.IntistialIntervalLevel.Value > 0)
        {
            if (currentLevel - lastAdShownLevel < ConfigManager.IntistialIntervalLevel.Value)
                return false;
        }

        // 시간 간격 확인
        if (ConfigManager.IntistialIntervalSecond.Value > 0 &&
            Time.realtimeSinceStartup - lastAdShownTime < ConfigManager.IntistialIntervalSecond.Value)
            return false;

        return true;
    }

    public bool IsShowing => interstitialCallback != null;

    public void TryShowAd(Action<ADSRESULT> callback)
    {
        if (CanShowAd())
        {
            ShowInterstitial(result =>
            {
                if (result == ADSRESULT.SUCCESS)
                {
                    MarkAdShown();
                }
                callback?.Invoke(result);
            });
        }
        else
        {
            Debug.LogWarning("[MaxInterstitial] Cannot show ad - conditions not met");
            callback?.Invoke(ADSRESULT.NOTREADY);
        }
    }

    public IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[MaxInterstitial] Initialize Start");
        isInitialized = false;
        progressCallback?.Invoke(0f);
        AdUnitId = SDKManager.Instance.GetProxy<MaxSDKManager>().GetAdUnitId(GetAdType);

        // MaxSdk 설정
        MaxSdk.SetInterstitialExtraParameter(AdUnitId, "mute_audio", "false");

        // 모든 이벤트 구독
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnAdLoadedEvent;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnAdLoadFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnAdDisplayedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnAdDisplayFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnAdClickedEvent;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnAdHiddenEvent;
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;

        // 첫 광고 로드
        LoadInterstitial();

        // 초기화 완료 대기 (2초 타임아웃)
        float timeout = Time.time + 2f;
        while (!isInitialized && Time.time < timeout)
        {
            yield return null;
        }

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
        yield return null;
    }

    private void ShowInterstitial(Action<ADSRESULT> callback)
    {
        interstitialCallback = callback;

        if (IsReady())
        {
            MaxSdk.ShowInterstitial(AdUnitId);
        }
        else
        {
            LoadInterstitial();
            InvokeInterstitialCallback(ADSRESULT.NOTREADY);
        }
    }

    private void MarkAdShown()
    {
        int currentLevel = UserDataManager.Instance.CurrentLevel.Value;
        lastAdShownLevel = currentLevel;
        lastAdShownTime = Time.realtimeSinceStartup;
        Debug.Log($"[MaxInterstitial] Ad shown marked - Level: {currentLevel}, Time: {lastAdShownTime:F1}s");
    }

    private void InvokeInterstitialCallback(ADSRESULT result)
    {
        interstitialCallback?.Invoke(result);
        interstitialCallback = null;
    }

    private void OnAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxInterstitial] Ad loaded successfully");
        interstitialRetryAttempt = 0;
        isInitialized = true;
    }

    private void OnAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        interstitialRetryAttempt++;
        float retryDelay = Mathf.Pow(2, Mathf.Min(6, interstitialRetryAttempt));
        Debug.LogError($"[MaxInterstitial] Load failed (attempt {interstitialRetryAttempt}): {errorInfo.Code}");
        if (!isInitialized)
        {
            isInitialized = true;
            SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Interstitial, ADSRESULT.FAIL, null);
        }

        Invoke(nameof(LoadInterstitial), retryDelay);
    }

    private void OnAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxInterstitial] Ad displayed");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Interstitial, ADSRESULT.SUCCESS, adInfo);
    }

    private void OnAdDisplayFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        Debug.LogError($"[MaxInterstitial] Display failed: {errorInfo.Code}");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdViewEvent(AdType.Interstitial, ADSRESULT.FAIL, adInfo);
        LoadInterstitial();
        InvokeInterstitialCallback(ADSRESULT.FAIL);
    }

    private void OnAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxInterstitial] Ad clicked");
        SDKManager.Instance.GetProxy<MaxSDKManager>().SendAdClickEvent(AdType.Interstitial, adInfo);
    }

    private void OnAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("[MaxInterstitial] Ad hidden - Preloading next");
        LoadInterstitial();
        InvokeInterstitialCallback(ADSRESULT.SUCCESS);
    }

    private void OnAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log($"[MaxInterstitial] Revenue paid: ${adInfo.Revenue:F4} (Network: {adInfo.NetworkName})");
        SDKManager.Instance.GetProxy<MaxSDKManager>().TrackAdRevenue(adInfo, AdType.Interstitial);
    }

    private void OnDestroy()
    {
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnAdLoadedEvent;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnAdLoadFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= OnAdDisplayedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnAdDisplayFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent -= OnAdClickedEvent;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnAdHiddenEvent;
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= OnAdRevenuePaidEvent;
    }
}