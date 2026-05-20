using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyUI : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string currencyID = "";
    [SerializeField] private TextMeshProUGUI displayText;
    
    [Header("Shop Integration")]
    [SerializeField] private Button shopShortcutButton;
    [SerializeField] private GameObject plusIconObject;

    private void Awake()
    {
        InitializeShopShortcut();
    }

    private void OnEnable()
    {
        Debug.Log($"CurrencyUI OnEnable : {gameObject.name}");

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged.AddListener(UpdateUI);
            UpdateUI(currencyID, CurrencyManager.Instance.GetCurrencyAmount(currencyID));
        }

        if (shopShortcutButton != null)
        {
            shopShortcutButton.onClick.AddListener(OnShopShortcutButtonClicked);
        }
    }

    private void OnDisable()
    {
        Debug.Log($"CurrencyUI OnDisable : {gameObject.name}");

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged.RemoveListener(UpdateUI);
        }

        if (shopShortcutButton != null)
        {
            shopShortcutButton.onClick.RemoveListener(OnShopShortcutButtonClicked);
        }
    }

    private void InitializeShopShortcut()
    {
        bool hasButton = shopShortcutButton != null;

        if (plusIconObject != null)
        {
            plusIconObject.SetActive(hasButton);
        }
    }

    private void UpdateUI(string changedCurrencyID, long newAmount)
    {
        Debug.Log($"CurrencyUI UpdateUI : {gameObject.name} | ID : {currencyID} | Amount : {newAmount}");

        if (changedCurrencyID == currencyID)
        {
            var currency = CurrencyManager.Instance.GetCurrency(currencyID);

            if (currency != null)
            {
                if (displayText != null)
                {
                    displayText.text = newAmount.ToString(currency.formatString ?? "N0");
                }
            }
        }
    }

    private void OnShopShortcutButtonClicked()
    {
        Debug.Log($"CurrencyUI shop shortcut button clicked for currency: {currencyID}");

        UIManager.Instance.ShopPopupOnOff(true);
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh UI Now")]
    private void RefreshNow()
    {
        if (CurrencyManager.Instance != null)
        {
            UpdateUI(currencyID, CurrencyManager.Instance.GetCurrencyAmount(currencyID));
        }
    }
#endif
}