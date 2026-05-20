using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ShopBase : PopupBase
{
    [Header("Shop Basic Info")]
    [SerializeField] protected string shopID;
    [SerializeField] protected string shopName;

    [Header("Shop Item Data")]

    [SerializeReference]
    [SerializeField] protected List<ShopItemBase> shopItems = new List<ShopItemBase>();

    [Header("Item Container")]
    [SerializeField] protected Transform itemContainer;

    // 이미 배치된 UI 요소들을 관리
    [SerializeField] protected List<ShopItemUI> prebuiltItemUIs = new List<ShopItemUI>();
    [SerializeField] protected List<ShopItemUI> activeItemUIs = new List<ShopItemUI>();

    protected override void Awake()
    {
        base.Awake();

        InitializePrebuiltItems();
    }


    #region 기존 UI 요소 관리
    /// <summary>
    /// 이미 배치된 UI 요소들을 찾아서 리스트로 관리
    /// </summary>
    protected virtual void InitializePrebuiltItems()
    {
        if (itemContainer == null) return;

        prebuiltItemUIs.Clear();

        var allItemUIs = itemContainer.GetComponentsInChildren<ShopItemUI>(true);

        foreach (var itemUI in allItemUIs)
        {
            if (IsValidShopItemUI(itemUI))
            {
                prebuiltItemUIs.Add(itemUI);
            }
        }

        foreach (var itemUI in prebuiltItemUIs)
        {
            itemUI.gameObject.SetActive(false);
            itemUI.ResetItem();
        }

        // Debug.Log($"[{shopName}] 전체 탐색: {allItemUIs.Length}개, 유효 상품 UI: {prebuiltItemUIs.Count}개 초기화 완료");
    }

    private bool IsValidShopItemUI(ShopItemUI itemUI)
    {
        if (itemUI.gameObject.name.StartsWith("StoreCoin") ||
            itemUI.gameObject.name.StartsWith("ShopItem") ||
            itemUI.gameObject.name.StartsWith("ADS"))

            return true;

        Transform current = itemUI.transform;
        while (current.parent != null && current.parent != itemContainer)
        {
            current = current.parent;
            Debug.Log("@@@ " + current.name);
            if (current.name.Contains("Grid") || current.name.Contains("Container"))
                return true;
        }

        return false;
    }

    protected override void OnOpenAnimationComplete()
    {
        base.OnOpenAnimationComplete();

        Time.timeScale = 0;
    }

    public override void Open(string title = "", string content = "")
    {
        if (IAPManager.Instance == null)
        {
            Debug.Log("[ShopBase] Open IAPManager.Instance == null");
            return;
        }

        if (!IAPManager.Instance.IsFullyInitialized)
        {
            IAPManager.Instance.Initialize();
        }

        if (!IAPManager.Instance.IsConnected)
        {
            IAPManager.Instance.SDKInitiailze();
        }

        base.Open(title, content);
    }

    public override void Close()
    {
        Time.timeScale = 1;

        base.Close();
    }


    /// <summary>
    /// 사용 가능한 UI 요소 가져오기
    /// </summary>
    protected ShopItemUI GetAvailableItemUI()
    {
        // 비활성화된 UI 요소 중 하나를 반환
        var availableUI = prebuiltItemUIs.FirstOrDefault(x => !x.gameObject.activeSelf);

        if (availableUI != null)
        {
            availableUI.gameObject.SetActive(true);
            activeItemUIs.Add(availableUI);
            return availableUI;
        }

        Debug.LogWarning($"[{shopName}] 사용 가능한 UI 요소가 부족합니다. 추가 UI 요소를 배치해주세요.");
        return null;
    }

    /// <summary>
    /// 모든 활성화된 UI 요소 비활성화
    /// </summary>
    protected void DeactivateAllItems()
    {
        foreach (var itemUI in activeItemUIs)
        {
            itemUI.gameObject.SetActive(false);
            itemUI.ResetItem();
        }
        activeItemUIs.Clear();
    }
    #endregion

    #region 상점 표시/갱신
    /// <summary>
    /// 상점 UI 전체 갱신
    /// </summary>
    public virtual void RefreshShopDisplay()
    {
        DeactivateAllItems();

        // 표시할 상품 수가 사전 배치된 UI 수보다 많으면 경고
        if (shopItems.Count > prebuiltItemUIs.Count)
        {
            Debug.LogWarning($"[{shopName}] 상품 수({shopItems.Count})가 UI 요소 수({prebuiltItemUIs.Count})보다 많습니다.");
        }

        // 상품 데이터를 UI에 할당
        for (int i = 0; i < shopItems.Count && i < prebuiltItemUIs.Count; i++)
        {
            var itemUI = GetAvailableItemUI();
            if (itemUI != null)
            {
                itemUI.SetupItem(shopItems[i], this);
            }
        }
    }
    #endregion

    #region 구매 처리
    public virtual void PurchaseItem(ShopItemBase item)
    {
        if (item.isIAPItem)
        {
            IAPManager.Instance.BuyProduct(item.itemID);
        }
        else
        {
            if (CanAfford(item))
            {
                SpendCurrencies(item);
                item.OnPurchased();
                RefreshShopDisplay();
            }
        }
    }

    protected bool CanAfford(ShopItemBase item)
    {
        foreach (var price in item.prices)
        {
            // if (GameManager.Instance.GameController.View.Player.Item.GetItemCount(uint.Parse(price.currencyID)) < price.amount)
                return false;
        }
        return true;
    }

    protected void SpendCurrencies(ShopItemBase item)
    {
        foreach (var price in item.prices)
        {
            // GameManager.Instance.GameController.View.Player.Item.Add(uint.Parse(price.currencyID), -price.amount);
        }
    }
    #endregion

    #region 상품 관리
    public ShopItemBase GetItem(string itemID)
    {
        return shopItems.FirstOrDefault(item => item.itemID == itemID);
    }

    public void AddItem(ShopItemBase item)
    {
        if (!shopItems.Contains(item))
            shopItems.Add(item);
    }

    public void RemoveItem(string itemID)
    {
        var item = GetItem(itemID);
        if (item != null)
            shopItems.Remove(item);
    }

    public List<ShopItemBase> GetAllItems()
    {
        return shopItems;
    }
    #endregion

    #region 에디터 유틸리티
#if UNITY_EDITOR
    [ContextMenu("UI 요소 개수 확인")]
    private void CheckUIElementCount()
    {
        InitializePrebuiltItems();
        Debug.Log($"사전 배치된 UI 요소: {prebuiltItemUIs.Count}개, 상품 데이터: {shopItems.Count}개");
    }

    [ContextMenu("테스트 상점 새로고침")]
    private void TestRefreshShop()
    {
        RefreshShopDisplay();
    }
#endif
    #endregion
}