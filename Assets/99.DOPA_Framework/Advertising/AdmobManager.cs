using System;
using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

public class AdmobManager : BaseSDKManager
{
    #region Ad Unit IDs
#if UNITY_IOS
    private const string AppOpenAdUnitId = "ca-app-pub-2587334266128089~2097231678";
#elif UNITY_ANDROID
    private const string AppOpenAdUnitId = "ca-app-pub-2587334266128089~2097231678";
#else
    private const string AppOpenAdUnitId = "ca-app-pub-2587334266128089~2097231678";
#endif
    #endregion

    #region Fields
    public AppOpenAd appOpenAd = null;
    private bool isAdmobAppOpenShowing = false;
    private DateTime admobAppOpenLoadTime;
    private const double AdmobAppOpenExpireHours = 0.001; // 매우 짧게 설정 (테스트용)
    private Action<ADSRESULT> appOpenAdCallback;
    #endregion


    #region Public API
    public void InitiaizeMobileAds(Action<ADSRESULT> callback = null)
    {
        appOpenAdCallback = callback;
        Debug.Log("[AdmobManager] InitializeAppOpenAds Start");
        MobileAds.Initialize(initStatus => RequestAdMobAppOpenAd());
    }

    public void ShowAdMobAppOpenAdIfAvailable()
    {
        if (isAdmobAppOpenShowing) return;
        if (appOpenAd == null)
        {
            RequestAdMobAppOpenAd();
            return;
        }
        if ((DateTime.Now - admobAppOpenLoadTime).TotalHours > AdmobAppOpenExpireHours)
        {
            RequestAdMobAppOpenAd();
            return;
        }
        appOpenAd.Show();
    }
    #endregion

    #region Private Implementation
    private void RequestAdMobAppOpenAd()
    {
        Debug.Log("[AdmobManager] RequestAdMobAppOpenAd");
        string adUnitId = AppOpenAdUnitId;

        if (appOpenAd != null)
        {
            appOpenAd.Destroy();
            appOpenAd = null;
        }

        AdRequest request = new AdRequest();
        AppOpenAd.Load(adUnitId, request, (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"[AdmobManager] Load Fail: {error}");
                return;
            }

            appOpenAd = ad;
            admobAppOpenLoadTime = DateTime.Now;
            RegisterAdMobAppOpenEvents();
            Debug.Log("[AdmobManager] Load Success");
        });
    }

    private void RegisterAdMobAppOpenEvents()
    {
        if (appOpenAd == null) return;

        // Revenue Tracking
        appOpenAd.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[AdmobManager] App open ad paid: {adValue.Value} {adValue.CurrencyCode}");
            string countryCode = MaxSdk.GetSdkConfiguration()?.CountryCode ?? "US";
            double revenueUsd = adValue.Value / 1000000.0;

            var eventValues = new Dictionary<string, object>
            {
                    { "af_ad_type", "AppOpen" },
                    { "af_revenue", revenueUsd },
                    { "af_currency", adValue.CurrencyCode ?? "USD" },
                    { "af_network", "AdMob" },
                    { "af_ad_unit_id", AppOpenAdUnitId },
                    { "af_country", countryCode }
            };

            // var eventValuesLog = new Dictionary<string, object>
            // {
            //         { "adplatform", "AdMob" },
            //         { "adsource", "AdMob" },
            //         { "adformat", "AppOpen" },
            //         { "adunitname", AppOpenAdUnitId },
            //         { "currency", adValue.CurrencyCode ?? "USD" },
            //         { "value", revenueUsd }
            // };

            AnalyticsManager.Instance.EventLog("af_ad_revenue", eventValues);
            // SDKManager.Instance.Log("adimpression", eventValuesLog);
            Debug.Log($"[AdmobManager] afadrevenue sent: {revenueUsd}, {countryCode}, AdMob");
        };

        appOpenAd.OnAdImpressionRecorded += () => Debug.Log("[AdmobManager] App open ad recorded an impression.");
        appOpenAd.OnAdClicked += () => Debug.Log("[AdmobManager] App open ad was clicked.");
        appOpenAd.OnAdFullScreenContentOpened += () => Debug.Log("[AdmobManager] App open ad full screen content opened.");

        appOpenAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdmobManager] App open ad full screen content closed.");

            isAdmobAppOpenShowing = false;
            appOpenAd.Destroy();
            appOpenAd = null;

            appOpenAdCallback?.Invoke(ADSRESULT.SUCCESS);
            appOpenAdCallback = null;
        };

        appOpenAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"[AdmobManager] App open ad failed to open full screen content: {error}");

            isAdmobAppOpenShowing = false;
            appOpenAd.Destroy();
            appOpenAd = null;

            appOpenAdCallback?.Invoke(ADSRESULT.FAIL);
            appOpenAdCallback = null;

            RequestAdMobAppOpenAd();
        };
    }
    #endregion

    #region BaseSDKManager Implementation
    public override IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[AdmobManager] Initialize Start");
        progressCallback?.Invoke(0f);

        // MobileAds.Initialize는 비동기지만 여기서는 즉시 완료로 처리
        // 실제 초기화는 InitializeAppOpenAds에서 처리
        yield return null;

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
    }
    #endregion
}