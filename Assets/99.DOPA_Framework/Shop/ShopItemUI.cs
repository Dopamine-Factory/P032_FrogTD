using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
{
    [Header("상품 ID (에디터에서 직접 입력)")]
    public string itemID;

    [Header("UI 참조")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI itemDescription;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button purchaseButton;
    
    [SerializeField] private TextMeshProUGUI rewardAmountText;
    [SerializeField] private Image rewardIcon;
    
    [SerializeField] private GameObject discountBadge;
    [SerializeField] private TextMeshProUGUI discountText;
    [SerializeField] private Transform tagsContainer;
    [SerializeField] private GameObject tagPrefab;

    [SerializeReference] private ShopItemBase currentItem;
    [SerializeField] private ShopBase parentShop;

    private void Start()
    {
        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(OnPurchaseClicked);
        }
    }

    private void OnPurchaseClicked()
    {
        if (currentItem != null && parentShop != null)
        {
            Debug.Log($"구매 버튼 클릭: {currentItem.itemID}");
            parentShop.PurchaseItem(currentItem);
        }
        else
        {
            Debug.LogWarning("구매할 상품이 없거나 상점이 설정되지 않았습니다.");
        }
    }

    public void SetupItem(ShopItemBase item, ShopBase shop)
    {
        currentItem = item;
        parentShop = shop;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (currentItem == null)
        {
            Debug.LogWarning("currentItem이 null입니다.");
            return;
        }

        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("CurrencyManager 인스턴스가 없습니다!");
            return;
        }

        UpdateItemInfo();
        
        UpdateRewardDisplay();
        
        UpdatePriceDisplay();
        
        UpdateItemIcon();
        
        UpdateDiscountDisplay();
        
        UpdatePurchaseButton();
    }

    /// <summary>
    /// 아이템 아이콘 업데이트 (보상 정보 기반)
    /// </summary>
    private void UpdateItemIcon()
    {
        if (itemIcon == null) return;

        if (currentItem.itemIcon != null)
        {
            itemIcon.sprite = currentItem.itemIcon;
            itemIcon.gameObject.SetActive(true);
            return;
        }

        if (currentItem.rewards != null && currentItem.rewards.Count > 0)
        {
            var mainReward = currentItem.rewards[0];
            var currency = CurrencyManager.Instance.GetCurrency(mainReward.currencyID);
            
            Debug.Log("mainReward amount : " + mainReward.amount.ToString());
            if (currency != null && currency.icon != null)
            {
                itemIcon.sprite = currency.icon;
                itemIcon.gameObject.SetActive(true);
                return;
            }
        }

        itemIcon.gameObject.SetActive(false);
    }

    private void UpdateItemInfo()
    {
        if (itemName != null)
            itemName.text = string.IsNullOrEmpty(currentItem.displayName) 
                ? currentItem.itemID 
                : currentItem.displayName;

        if (itemDescription != null)
            itemDescription.text = currentItem.description ?? "";
    }

    private void UpdateRewardDisplay()
    {
        if (currentItem.rewards == null || currentItem.rewards.Count == 0) 
            return;

        var mainReward = currentItem.rewards[0];
        
        if (rewardAmountText != null)
            rewardAmountText.text = FormatAmount(mainReward.amount);

        if (rewardIcon != null)
        {
            var currency = CurrencyManager.Instance.GetCurrency(mainReward.currencyID);
            if (currency != null && currency.icon != null)
            {
                rewardIcon.sprite = currency.icon;
                rewardIcon.gameObject.SetActive(true);
            }
            else
            {
                rewardIcon.gameObject.SetActive(false);
            }
        }
    }

    private string FormatAmount(int amount) => 
        amount >= 1000000 ? $"{amount / 1000000f:F1}M" :
        amount >= 1000 ? $"{amount / 1000f:F0}K" :
        amount.ToString("N0");

    private void UpdatePriceDisplay()
    {
        if (priceText == null) return;

        if (currentItem.isIAPItem)
        {
            var product = IAPManager.Instance.GetProduct(currentItem.iapProductID);
            priceText.text = product != null 
                ? product.metadata.localizedPriceString 
                : "Loading...";
        }
        else if (currentItem.prices.Count > 0)
        {
            priceText.text = $"{currentItem.prices[0].amount:N0}";
        }
    }

    private void UpdateDiscountDisplay()
    {
        if (discountBadge == null) return;
        
        discountBadge.SetActive(currentItem.hasDiscount);
        if (currentItem.hasDiscount && discountText != null)
        {
            discountText.text = $"-{currentItem.discountPercentage}%";
        }
    }

    private void UpdatePurchaseButton()
    {
        if (purchaseButton == null) return;

        bool canPurchase = currentItem.CanPurchase();
        
        if (!currentItem.isIAPItem)
        {
            foreach (var price in currentItem.prices)
            {
                if (!CurrencyManager.Instance.HasCurrency(price.currencyID))
                {
                    canPurchase = false;
                    break;
                }
            }
        }

        purchaseButton.interactable = canPurchase;
    }

    public void ResetItem()
    {
        currentItem = null;
        parentShop = null;
        
        if (itemIcon) itemIcon.sprite = null;
        if (itemName) itemName.text = "";
        if (itemDescription) itemDescription.text = "";
        if (priceText) priceText.text = "";
        if (rewardAmountText) rewardAmountText.text = "";
        if (rewardIcon) rewardIcon.sprite = null;
        
        if (purchaseButton != null)
            purchaseButton.interactable = false;
    }
}