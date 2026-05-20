using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class AdButton : MonoBehaviour
{
    public enum AdButtonType
    {
        Rewarded,
        Interstitial
    }

    public enum RewardType
    {
        None,
        ScoreDouble,
        Currency,
        Custom
    }

    [Header("Ad Settings")]
    [SerializeField] private string uniqueButtonId = "MainScreen_RewardAd";

    public string UniqueButtonId { get => uniqueButtonId; set => uniqueButtonId = value; }

    [SerializeField] private AdButtonType buttonAdType = AdButtonType.Rewarded;
    public AdButtonType ButtonAdType { get => buttonAdType; set => buttonAdType = value; }

    [SerializeField] private RewardType rewardType = RewardType.None;
    [SerializeField] private float rewardValue = 0f;
    [SerializeField] private float cooldownSeconds = 60f;
    [SerializeField] private string productID = "id";

    public string ProductID { get => productID; set => productID = value; }

    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Image cooldownFill;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private GameObject loadingIndicator;

    private DateTime lastAdTime;
    private bool isLoading = false;
    private bool isAdPass = false;

    private Coroutine cooldownCoroutine;
    private Coroutine loadingTimeoutRoutine;

    public bool IsAdPass
    {
        get { return isAdPass; }
        set { isAdPass = value; }
    }


    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(OnClick);
        LoadCooldown();
    }

    private void Start()
    {
        if (AdvertisingRootManager.Instance != null)
        {
            AdvertisingRootManager.Instance.AddAdButton(this);
        }
        else
        {
            Debug.LogWarning("[AdButton] AdvertisingRootManager is null. Disabling AdButton.");
            gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // Update time correctly if app was in background
        LoadCooldown();

        if (IsInCooldown())
        {
            StartCooldownRoutine();
        }
        else
        {
            // Bug Fix: Reset UI explicitly when there is no cooldown
            ResetCooldownUI();
            UpdateInteractableState();
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }

        if (AdvertisingRootManager.Instance != null)
        {
            AdvertisingRootManager.Instance.RemoveAdButton(this);
        }

        StopCooldownRoutine();

        if (loadingTimeoutRoutine != null)
        {
            StopCoroutine(loadingTimeoutRoutine);
        }
    }

    private void OnClick()
    {
        Debug.Log("[AdButton] OnClick triggered.");

        if (isLoading || IsInCooldown())
        {
            return;
        }

        if (isAdPass)
        {
            OnAdResultCallback(ADSRESULT.SUCCESS);
            return;
        }

        isLoading = true;

        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true);
        }

        UpdateInteractableState();

        if (loadingTimeoutRoutine != null)
        {
            StopCoroutine(loadingTimeoutRoutine);
        }
        loadingTimeoutRoutine = StartCoroutine(LoadingTimeoutRoutine());

        if (buttonAdType == AdButtonType.Rewarded)
        {
            AdvertisingRootManager.Instance.ShowRewardedAd(OnAdResultCallback);
        }
        else if (buttonAdType == AdButtonType.Interstitial)
        {
            AdvertisingRootManager.Instance.ShowInterstitial(OnAdResultCallback);
        }
    }

    private IEnumerator LoadingTimeoutRoutine()
    {
        yield return new WaitForSeconds(5f);

        if (isLoading)
        {
            Debug.LogWarning("[AdButton] Ad request timeout. Resetting loading state.");
            isLoading = false;

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            UpdateInteractableState();
        }
    }

    private void OnAdResultCallback(ADSRESULT result)
    {
        Debug.Log($"[AdButton] OnAdResultCallback result : {result}");

        if (loadingTimeoutRoutine != null)
        {
            StopCoroutine(loadingTimeoutRoutine);
            loadingTimeoutRoutine = null;
        }

        isLoading = false;

        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }

        if (result == ADSRESULT.SUCCESS)
        {
            Debug.Log("[AdButton] Ad viewed successfully. Giving reward.");

            GiveReward();

            lastAdTime = DateTime.UtcNow;
            SaveCooldown();
            StartCooldownRoutine();
        }
        else
        {
            // _ToDo: Handle Ad failure/cancel logic if necessary (e.g. show toast message)
            UpdateInteractableState();
        }
    }

    private void GiveReward()
    {
        Debug.Log("[AdButton] Distributing reward.");

        switch (rewardType)
        {
            case RewardType.ScoreDouble:
                BuffManager.Instance.AddBuff(BuffType.ScoreDouble, rewardValue);
                break;

            case RewardType.Currency:
                CurrencyManager.Instance.AddCurrency(ProductID, (long)rewardValue);
                break;

            case RewardType.Custom:
                // _ToDo: Implement custom reward logic using event system
                break;
        }
    }

    private void StartCooldownRoutine()
    {
        StopCooldownRoutine();
        cooldownCoroutine = StartCoroutine(CooldownRoutine());
    }

    private void StopCooldownRoutine()
    {
        if (cooldownCoroutine != null)
        {
            StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = null;
        }
    }

    private IEnumerator CooldownRoutine()
    {
        UpdateInteractableState();

        while (IsInCooldown())
        {
            float remain = GetRemainCooldown();

            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = remain / cooldownSeconds;
            }

            if (cooldownText != null)
            {
                cooldownText.text = Mathf.CeilToInt(remain).ToString();
            }

            yield return null; // Wait for next frame
        }

        ResetCooldownUI();
        UpdateInteractableState();
    }

    private void ResetCooldownUI()
    {
        if (cooldownFill != null)
        {
            cooldownFill.fillAmount = 0f;
        }

        if (cooldownText != null)
        {
            cooldownText.text = string.Empty;
        }
    }

    private void UpdateInteractableState()
    {
        bool isReady = !isLoading && !IsInCooldown();

        if (button != null)
        {
            button.interactable = isReady;
        }
    }

    private bool IsInCooldown()
    {
        return GetRemainCooldown() > 0;
    }

    private float GetRemainCooldown()
    {
        // _ToDo: Replace DateTime.UtcNow with a server time manager in the future
        double elapsed = (DateTime.UtcNow - lastAdTime).TotalSeconds;

        if (elapsed < 0)
        {
            return 0f;
        }

        return Mathf.Max(0, cooldownSeconds - (float)elapsed);
    }

    private void SaveCooldown()
    {
        PlayerPrefs.SetString(GetCooldownKey(), lastAdTime.ToBinary().ToString());
        PlayerPrefs.Save();
    }

    private void LoadCooldown()
    {
        string key = GetCooldownKey();

        if (PlayerPrefs.HasKey(key))
        {
            if (long.TryParse(PlayerPrefs.GetString(key), out long binaryTime))
            {
                lastAdTime = DateTime.FromBinary(binaryTime);
            }
            else
            {
                lastAdTime = DateTime.MinValue;
            }
        }
        else
        {
            lastAdTime = DateTime.MinValue;
        }
    }

    private string GetCooldownKey()
    {
        return $"AdButton_{buttonAdType}_{rewardType}_{uniqueButtonId}_LastAdTime";
    }
}