using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MaxSDKManager; // Assuming this is defined elsewhere

public enum AppOpenAdProvider
{
    None = 0,
    MaxSDK = 1,
    Admob = 2,
    MaxThenAdmob = 3,
    AdmobThenMax = 4
}

public enum AdType
{
    Banner,
    Interstitial,
    Rewarded,
    MREC,
    AppOpen
}

public enum ADSRESULT
{
    SUCCESS,
    CANCEL,
    NOTREADY,
    FAIL
}

[System.Serializable]
public class AdConfig
{
    public bool enableAppOpenAds = true;
    public bool enableInterstitialAds = true;
    public bool enableRewardedAds = true;
    public bool enableBannerAds = true;
    public bool enableMRecAds = false;
    public AppOpenAdProvider appOpenAdProvider = AppOpenAdProvider.MaxSDK;

    [Header("No Ads Affected Types")]
    public bool disableBannerWhenNoAds = true;
    public bool disableInterstitialWhenNoAds = true;
    public bool disableAppOpenWhenNoAds = true;
    public bool disableRewardedWhenNoAds = false;
    public bool disableMRecWhenNoAds = true;
}

public interface IAdProvider
{
    string ProviderName { get; }
    AdType GetAdType { get; }
    string AdUnitId { get; set; }

    IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete);
    bool IsReady();
    bool CanShowAd();
    bool IsShowing { get; }
    void TryShowAd(Action<ADSRESULT> callback);
}

public interface IBannerProvider
{
    string ProviderName { get; }
    AdType GetAdType { get; }
    string AdUnitId { get; set; }

    IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete);
    void ShowBanner();
    void HideBanner();
    void ToggleBanner();
    bool IsBannerShowing { get; }
}

public class AdvertisingRootManager : BaseSystemManager<AdvertisingRootManager>
{
    #region SerializeFields
    [Header("Ad Managers")]
    [SerializeField] private MaxSDKManager maxSDKManager;
    [SerializeField] private AdmobManager admobManager;

    [Header("Direct Providers (Optional - MaxSDKManager Priority)")]
    [SerializeField] private IAdProvider[] directAdProviders;
    [SerializeField] private IBannerProvider[] directBannerProviders;

    [Header("Ad Configuration")]
    [SerializeField] private AdConfig adConfig = new AdConfig();

    [Header("NoAds Management")]
    [SerializeField] private bool overrideNoAdsStatus = false;
    #endregion

    #region Fields
    // Naming convention: PascalCase for constants
    private const string AdsDisabledKey = "AdsDisabled";
    private const string PurchaseTimeKey = "PurchaseTime";

    private List<AdButton> adButtons = new List<AdButton>();
    private bool isNoAdsOn;
    #endregion

    #region Properties
    public bool IsAppOpenReady
    {
        get { return GetAdReadiness(AdType.AppOpen); }
    }

    public bool IsInterstitialReady
    {
        get { return GetAdReadiness(AdType.Interstitial); }
    }

    public bool IsRewardedAdReady
    {
        get { return GetAdReadiness(AdType.Rewarded); }
    }

    public bool IsNoAdsOn
    {
        get { return overrideNoAdsStatus ? overrideNoAdsStatus : isNoAdsOn; }
    }

    public AdConfig Config
    {
        get { return adConfig; }
    }

    public IReadOnlyList<AdButton> AdButtons
    {
        get { return adButtons.AsReadOnly(); }
    }
    #endregion

    public override string Id
    {
        get { return "AdvertisingRootManager"; }
    }

    #region Events
    public static event Action<AdType, ADSRESULT> OnAdResult;
    public static event Action<bool> OnNoAdsStatusChanged;
    #endregion

    public override void Initialize()
    {
        base.Initialize();
        LoadNoAdsStatus();
        UpdateAllAdButtons();

        Debug.Log("[AdvertisingRootManager] Initialize completed.");
    }

    protected override void CompleteInitialization()
    {
        Debug.Log("[AdvertisingRootManager] SDK dependency check completed.");
        base.CompleteInitialization();
    }

    #region ShowAd
    public void ShowAd(AdType adType, Action<ADSRESULT> callback = null)
    {
        Debug.Log($"[AdvertisingRootManager] ShowAd called for type: {adType}");

#if MARKETING
            callback?.Invoke(ADSRESULT.SUCCESS);
            return;
#endif

        if (IsNoAdsOn && IsAdTypeDisabledByNoAds(adType))
        {
            Debug.Log($"[AdvertisingRootManager] {adType} skipped due to NoAds active.");
            callback?.Invoke(ADSRESULT.SUCCESS);
            return;
        }

        ExecuteAdStrategy(adType, callback);
    }
    #endregion

    #region Explicit Ad Methods
    public void ShowAppOpenAd(Action<ADSRESULT> callback = null)
    {
        ShowAd(AdType.AppOpen, callback);
    }

    public void ShowInterstitial(Action<ADSRESULT> callback = null)
    {
        ShowAd(AdType.Interstitial, callback);
    }

    public void ShowRewardedAd(Action<ADSRESULT> callback)
    {
        ShowAd(AdType.Rewarded, callback);
    }

    public void ShowBannerAd(Action<ADSRESULT> callback)
    {
        ShowAd(AdType.Banner, callback);
    }

    public void ShowBanner()
    {
        ShowAd(AdType.Banner, null);
    }

    public void ToggleBannerVisibility()
    {
        ExecuteBannerToggle();
    }

    public void HideBanner()
    {
        ExecuteBannerHide();
    }
    #endregion

    #region ExecuteAdStrategy
    private void ExecuteAdStrategy(AdType adType, Action<ADSRESULT> callback)
    {
        bool isEnabled = false;

        switch (adType)
        {
            case AdType.AppOpen:
                isEnabled = adConfig.enableAppOpenAds;
                break;
            case AdType.Interstitial:
                isEnabled = adConfig.enableInterstitialAds;
                break;
            case AdType.Rewarded:
                isEnabled = adConfig.enableRewardedAds;
                break;
            case AdType.Banner:
                isEnabled = adConfig.enableBannerAds;
                break;
            case AdType.MREC:
                isEnabled = adConfig.enableMRecAds;
                break;
        }

        if (!isEnabled)
        {
            Debug.Log($"[AdvertisingRootManager] {adType} is disabled by configuration.");
            callback?.Invoke(ADSRESULT.SUCCESS);
            return;
        }

        switch (adType)
        {
            case AdType.AppOpen:
                ExecuteAppOpen(callback);
                break;
            case AdType.Interstitial:
                ExecuteInterstitial(callback);
                break;
            case AdType.Rewarded:
                ExecuteRewarded(callback);
                break;
            case AdType.Banner:
                ExecuteBanner();
                break;
            case AdType.MREC:
                ExecuteMREC();
                break;
            default:
                callback?.Invoke(ADSRESULT.NOTREADY);
                break;
        }
    }
    #endregion

    #region IAdProvider Execution Methods
    private void ExecuteAppOpen(Action<ADSRESULT> callback)
    {
        switch (adConfig.appOpenAdProvider)
        {
            case AppOpenAdProvider.MaxSDK:
                MaxAppOpen(callback);
                break;
            case AppOpenAdProvider.Admob:
                AdmobAppOpen(callback);
                break;
            case AppOpenAdProvider.MaxThenAdmob:
                MaxThenAdmob(callback);
                break;
            case AppOpenAdProvider.AdmobThenMax:
                AdmobThenMax(callback);
                break;
            default:
                callback?.Invoke(ADSRESULT.NOTREADY);
                break;
        }
    }

    private void ExecuteInterstitial(Action<ADSRESULT> callback)
    {
        IAdProvider provider = GetAdProvider(AdType.Interstitial);

        if (provider != null)
        {
            provider.TryShowAd(callback ?? (result => { }));
        }
    }

    private void ExecuteRewarded(Action<ADSRESULT> callback)
    {
        IAdProvider provider = GetAdProvider(AdType.Rewarded);

        if (provider != null)
        {
            provider.TryShowAd(callback);
        }
    }
    #endregion

    #region IBannerProvider Execution Methods
    private void ExecuteBanner()
    {
        IBannerProvider provider = GetBannerProvider(AdType.Banner);

        if (provider != null)
        {
            provider.ShowBanner();
        }
    }

    private void ExecuteBannerToggle()
    {
        IBannerProvider provider = GetBannerProvider(AdType.Banner);

        if (provider != null)
        {
            provider.ToggleBanner();
        }
    }

    private void ExecuteBannerHide()
    {
        IBannerProvider provider = GetBannerProvider(AdType.Banner);

        if (provider != null)
        {
            provider.HideBanner();
        }
    }

    private void ExecuteMREC()
    {
        IBannerProvider provider = GetBannerProvider(AdType.MREC);

        if (provider != null)
        {
            provider.ShowBanner();
        }
    }
    #endregion

    #region AppOpen Strategy
    private void MaxAppOpen(Action<ADSRESULT> callback)
    {
        IAdProvider provider = GetAdProvider(AdType.AppOpen);

        if (provider != null && provider.CanShowAd())
        {
            provider.TryShowAd(callback);
        }
        else
        {
            callback?.Invoke(ADSRESULT.NOTREADY);
        }
    }

    private void AdmobAppOpen(Action<ADSRESULT> callback)
    {
        if (admobManager != null)
        {
            // _ToDo: Ensure AdmobManager has this method
            admobManager.ShowAdMobAppOpenAdIfAvailable();
            callback?.Invoke(ADSRESULT.SUCCESS);
        }
        else
        {
            callback?.Invoke(ADSRESULT.NOTREADY);
        }
    }

    private void MaxThenAdmob(Action<ADSRESULT> callback)
    {
        IAdProvider provider = GetAdProvider(AdType.AppOpen);

        if (provider != null && provider.CanShowAd())
        {
            MaxAppOpen(callback);
        }
        else
        {
            AdmobAppOpen(callback);
        }
    }

    private void AdmobThenMax(Action<ADSRESULT> callback)
    {
        if (admobManager != null)
        {
            AdmobAppOpen(callback);
        }
        else
        {
            MaxAppOpen(callback);
        }
    }
    #endregion

    #region Provider Helper Methods
    private IAdProvider GetAdProvider(AdType adType)
    {
        if (directAdProviders != null)
        {
            IAdProvider directProvider = directAdProviders.FirstOrDefault(p => p.GetAdType == adType);
            if (directProvider != null)
            {
                return directProvider;
            }
        }

        return maxSDKManager != null ? maxSDKManager.GetProvider(adType) : null;
    }

    private IBannerProvider GetBannerProvider(AdType adType)
    {
        if (directBannerProviders != null)
        {
            IBannerProvider directProvider = directBannerProviders.FirstOrDefault(p => p.ProviderName.Contains(adType.ToString()));
            if (directProvider != null)
            {
                return directProvider;
            }
        }

        return maxSDKManager != null ? maxSDKManager.GetBannerProvider(adType) : null;
    }
    #endregion

    #region NoAds Management
    public void LoadNoAdsStatus()
    {
        if (IAPManager.Instance != null)
        {
            if (IAPManager.Instance.IsIAPInitialized())
            {
                isNoAdsOn = IAPManager.Instance.IsPurchased(IAPManager.Instance.IsNoAdsProductID());
            }
        }

        if (isNoAdsOn)
        {
            HideBanner();
        }

        OnNoAdsStatusChanged?.Invoke(isNoAdsOn);
    }


    public void ForceNoAdsStaus(bool noAds)
    {
        isNoAdsOn = noAds;

        if (isNoAdsOn)
        {
            HideBanner();
        }
        else
        {
            ShowBanner();
        }

        OnNoAdsStatusChanged?.Invoke(isNoAdsOn);
    }


    public void DisableAdsOnOff(bool isOn = true)
    {
        PlayerPrefs.SetInt(AdsDisabledKey, isOn ? 1 : 0);
        PlayerPrefs.SetString(PurchaseTimeKey, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();

        isNoAdsOn = isOn;
        UpdateAllAdButtons();

        OnNoAdsStatusChanged?.Invoke(isNoAdsOn);
    }


    private bool IsAdTypeDisabledByNoAds(AdType adType)
    {
        return adType switch
        {
            AdType.Banner => adConfig.disableBannerWhenNoAds,
            AdType.Interstitial => adConfig.disableInterstitialWhenNoAds,
            AdType.AppOpen => adConfig.disableAppOpenWhenNoAds,
            AdType.Rewarded => adConfig.disableRewardedWhenNoAds,
            AdType.MREC => adConfig.disableMRecWhenNoAds,
            _ => false,
        };
    }


    #endregion

    #region AdButton Management
    public void AddAdButton(AdButton adButton)
    {
        if (adButton == null || adButtons.Contains(adButton))
        {
            return;
        }

        adButtons.Add(adButton);
        adButton.IsAdPass = IsNoAdsOn && IsAdTypeDisabledByNoAds(MapButtonToAdType(adButton));

        Debug.Log($"[AdvertisingRootManager] AdButton added: {adButton.UniqueButtonId}");
    }

    // Newly added method to prevent Memory Leaks!
    public void RemoveAdButton(AdButton adButton)
    {
        if (adButton != null && adButtons.Contains(adButton))
        {
            adButtons.Remove(adButton);
            Debug.Log($"[AdvertisingRootManager] AdButton removed: {adButton.UniqueButtonId}");
        }
    }

    public AdButton GetAdButton(string uniqueButtonId)
    {
        // Null check added for safety
        return adButtons.FirstOrDefault(btn => btn != null && btn.UniqueButtonId == uniqueButtonId);
    }

    public void UpdateAdButtonStatus(string uniqueButtonId)
    {
        AdButton button = GetAdButton(uniqueButtonId);

        if (button != null)
        {
            button.IsAdPass = IsNoAdsOn && IsAdTypeDisabledByNoAds(MapButtonToAdType(button));
            Debug.Log($"[AdvertisingRootManager] AdButton updated: {uniqueButtonId}");
        }
    }

    private void UpdateAllAdButtons()
    {
        // Clean up null references first (objects destroyed but not removed)
        adButtons.RemoveAll(btn => btn == null);

        foreach (AdButton button in adButtons)
        {
            button.IsAdPass = IsNoAdsOn && IsAdTypeDisabledByNoAds(MapButtonToAdType(button));
        }
    }

    private AdType MapButtonToAdType(AdButton adButton)
    {
        switch (adButton.ButtonAdType)
        {
            case AdButton.AdButtonType.Rewarded:
                return AdType.Rewarded;
            case AdButton.AdButtonType.Interstitial:
                return AdType.Interstitial;
            default:
                return AdType.Interstitial;
        }
    }
    #endregion

    #region State Validation
    private bool GetAdReadiness(AdType adType)
    {
        if (IsNoAdsOn)
        {
            return false;
        }

        switch (adType)
        {
            case AdType.AppOpen:
                return GetAppOpenReady();

            case AdType.Interstitial:
                IAdProvider interstitialProvider = GetAdProvider(AdType.Interstitial);
                return interstitialProvider != null && interstitialProvider.CanShowAd();

            case AdType.Rewarded:
                IAdProvider rewardedProvider = GetAdProvider(AdType.Rewarded);
                return rewardedProvider != null && rewardedProvider.IsReady();

            case AdType.Banner:
                return GetBannerProvider(AdType.Banner) != null;

            case AdType.MREC:
                return GetBannerProvider(AdType.MREC) != null;

            default:
                return false;
        }
    }

    private bool GetAppOpenReady()
    {
        switch (adConfig.appOpenAdProvider)
        {
            case AppOpenAdProvider.MaxSDK:
                IAdProvider provider = GetAdProvider(AdType.AppOpen);
                return provider != null && provider.CanShowAd();

            case AppOpenAdProvider.Admob:
                // _ToDo: Ensure appOpenAd is accessible or wrap it in a proper method check
                return admobManager != null && admobManager.appOpenAd != null;

            case AppOpenAdProvider.MaxThenAdmob:
                IAdProvider maxProvider = GetAdProvider(AdType.AppOpen);
                bool isMaxReady = maxProvider != null && maxProvider.CanShowAd();
                bool isAdmobReady = admobManager != null && admobManager.appOpenAd != null;
                return isMaxReady || isAdmobReady;

            default:
                return false;
        }
    }
    #endregion

    #region Event Invocation
    private void InvokeAdResult(AdType adType, ADSRESULT result)
    {
        OnAdResult?.Invoke(adType, result);
    }
    #endregion
}