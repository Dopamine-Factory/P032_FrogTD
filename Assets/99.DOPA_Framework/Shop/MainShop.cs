using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MainShop : ShopBase
{
    [Header("Shop Item Option Object")]
    [SerializeField] private Transform adsSection;
    [SerializeField] private Transform dealsSection;
    [SerializeField] private Transform oneTimeSection;
    [SerializeField] private Transform coinSection;
    [SerializeField] private Transform moneySection;

    [SerializeField] Image[] rewardItemIcons;
    [SerializeField] Image[] rewardItemIcons2;

    [Header("Featured ID List")]
    [SerializeField] private List<string> featuredItemIDs = new List<string>();

    private readonly Dictionary<Transform, List<ShopItemUI>> sectionActiveItems = new Dictionary<Transform, List<ShopItemUI>>();


    protected override void Awake()
    {
        base.Awake();

        InitializeSectionDictionary(adsSection);
        InitializeSectionDictionary(dealsSection);
        InitializeSectionDictionary(oneTimeSection);
        InitializeSectionDictionary(coinSection);
        InitializeSectionDictionary(moneySection);

        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccess.AddListener((productID) =>
            {
                ShowRewardAnimation(productID);

                RefreshShopDisplay();
            });

            IAPManager.Instance.OnPurchaseFailedEvent.AddListener((id, msg) =>
            {
                RefreshShopDisplay();
            });
        }
    }

    public override void PurchaseItem(ShopItemBase item)
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
                ShowRewardAnimation(item.itemID);
                RefreshShopDisplay();
            }
        }
    }

    private void ShowRewardAnimation(string productID)
    {
        // Shop_form shopData = Tables.GetTable<ShopTable>().GetDatas().FirstOrDefault(s => s.Value.aos_code == productID || s.Value.ios_code == productID || s.Value.id == productID).Value;
        // if (shopData.id == null)
        // {
        //     return;
        // }

        // Vector2 productPos = activeItemUIs.FirstOrDefault(ui => ui.itemID == shopData.id)?.transform.position ?? Vector2.zero;

        // int index = 0;
        // if (shopData.reward_id_1 == 1 || shopData.reward_id_1 == 2)
        // {
        //     FlyItems(index, shopData.reward_id_1, productPos, shopData.reward_id_1 == 1 ? moneyCell.GetComponent<RectTransform>() : coinCell.GetComponent<RectTransform>());
        //     ++index;
        // }

        // if (shopData.reward_id_2 == 1 || shopData.reward_id_2 == 2)
        // {
        //     FlyItems(index, shopData.reward_id_2, productPos, shopData.reward_id_2 == 1 ? moneyCell.GetComponent<RectTransform>() : coinCell.GetComponent<RectTransform>());
        //     ++index;
        // }
    }


    private void FlyItems(int index, uint itemID, Vector2 startPos, RectTransform targetRT)
    {
        // AddressablesAssetManager
        // Sprite sprite = ResourceManager.Instance.GetSprite(itemID);
        // if (index == 0)
        // {
        //     foreach (var icon in rewardItemIcons)
        //     {
        //         icon.sprite = sprite;
        //         icon.gameObject.SetActive(true);

        //         // 아이콘의 투명도와 크기 초기화
        //         icon.color = new Color(1, 1, 1, 0);
        //         icon.transform.localScale = Vector3.zero;

        //         // 2. 연출 시퀀스 생성
        //         Sequence seq = DOTween.Sequence();

        //         // [A] 나타나면서 살짝 랜덤한 위치로 퍼짐 (Explosion 효과)
        //         Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 150f; // 퍼지는 반경

        //         seq.Append(icon.transform.DOMove(startPos + randomOffset, 0.3f).SetEase(Ease.OutBack));
        //         seq.Join(icon.DOFade(1, 0.2f));
        //         seq.Join(icon.transform.DOScale(1.2f, 0.3f));

        //         // [B] 잠시 대기 (또는 바로 이동) - 아이콘마다 약간의 시차를 줌
        //         float delay = UnityEngine.Random.Range(0f, 0.2f);
        //         seq.AppendInterval(delay);

        //         // [C] 돈 위치로 빨려 들어감 (Swoosh)
        //         // Ease.InBack이나 Ease.InExponential을 쓰면 빨려 들어가는 느낌이 강해집니다.
        //         seq.Append(icon.transform.DOMove(targetRT.position, 0.5f).SetEase(Ease.InBack));
        //         seq.Join(icon.transform.DOScale(0.5f, 0.5f)); // 작아지면서 흡수

        //         // 3. 완료 후 처리
        //         seq.OnComplete(() =>
        //         {
        //             icon.gameObject.SetActive(false);
        //         });
        //     }
        // }
    }

    private void InitializeSectionDictionary(Transform section)
    {
        if (section == null)
        {
            return;
        }

        if (!sectionActiveItems.ContainsKey(section))
        {
            sectionActiveItems[section] = new List<ShopItemUI>();
        }
    }

    public override void Open(string title = "", string content = "")
    {
        base.Open(title, content);

        RefreshShopDisplay();
    }

    public async UniTaskVoid ShowingMoneys()
    {
        while (activeItemUIs.Count == 0)
        {
            await UniTask.Yield();
        }

        var srollRect = scrollRects[0];
        float contentHeight = srollRect.content.sizeDelta.y;
        float viewportHeight = srollRect.viewport.rect.height;

        var y = Math.Min(contentHeight - viewportHeight, contentHeight * 0.5f + (viewportHeight));

        srollRect.StopMovement();
        srollRect.content.anchoredPosition = new Vector2(0, y);
    }

    public void ShowingCoins()
    {
        ShowingMoneys().Forget();
    }

    private void CreateSampleItems()
    {
        shopItems.Clear();

        bool isReviewVersion = RemoteConfigManager.Instance.ReviewVersionString == VersionManager.Instance.VersionString;

        // var shopTableDatas = Tables.GetTable<ShopTable>().GetDatas();
        /*foreach (var data in shopTableDatas)
        {
            if (data.Value.is_shop == false) continue;
            if (isReviewVersion == false)
            {
                if (data.Value.time != 0)
                {
                    string endDateStr = LocalSaveManager.Purchase.GetTimingProductEndDate(data.Value.id);
                    if (string.IsNullOrEmpty(endDateStr) || GameDataManager.ConvertStringToDateTime(endDateStr) < DateTime.UtcNow)
                    {
                        continue;
                    }
                }
                if (data.Value.shop_tag_type == 3 && data.Value.id != specialOfferID) continue;
                // if (data.Value.purchase_chance != 0 && LocalSaveManager.Log.GetPurchaseCount(data.Value.id) >= data.Value.purchase_chance) continue;
            }

            ShopItemBase item;
            item = new SingleShopItem
            {
                itemID = data.Value.id,
                displayName = data.Value.name,
                description = data.Value.name,
                isIAPItem = data.Value.price_type == 1,
#if UNITY_ANDROID
                iapProductID = data.Value.aos_code,
#elif UNITY_IOS
                iapProductID = data.Value.ios_code,
#else
                iapProductID = data.Value.id,
#endif
                purchaseLimit = data.Value.purchase_chance == 0 ? -1 : data.Value.purchase_chance,
                prices = data.Value.price_type == 0 ? new List<ShopPrice>
                    {
                        new ShopPrice { currencyID = "2", amount = data.Value.price_quantity }
                    } : null,
                tags = new List<ShopTag>
                    {
                        new ShopTag { tagType = (ShopTagType)data.Value.shop_tag_type, isActive = true }
                    }
            };

            if (data.Value.purchase_chance != 0)
            {
                // item.currentPurchaseCount = (int)LocalSaveManager.Log.GetPurchaseCount(data.Value.id);
            }

            if (data.Value.time != 0)
            {
                item.isLimitedTime = data.Value.time > 0;

                if (isReviewVersion)
                {
                    // LocalSaveManager.Log.SetTimingProductEndDate(data.Value.id, GameDataManager.ConvertDateTimeToString(DateTime.UtcNow.AddSeconds(data.Value.time)));
                }
                // item.expirationTime = GameDataManager.ConvertStringToDateTime(LocalSaveManager.Log.GetTimingProductEndDate(data.Value.id));
            }

            item.rewards = new List<ShopReward>();
            if (data.Value.reward_id_1 != 0 && data.Value.reward_count_1 > 0)
            {
                item.rewards.Add(new ShopReward { currencyID = data.Value.reward_id_1.ToString(), amount = data.Value.reward_count_1 });
            }
            if (data.Value.reward_id_2 != 0 && data.Value.reward_count_2 > 0)
            {
                item.rewards.Add(new ShopReward { currencyID = data.Value.reward_id_2.ToString(), amount = data.Value.reward_count_2 });
            }
            if (data.Value.reward_id_3 != 0 && data.Value.reward_count_3 > 0)
            {
                item.rewards.Add(new ShopReward { currencyID = data.Value.reward_id_3.ToString(), amount = data.Value.reward_count_3 });
            }

            shopItems.Add(item);
        }*/
    }

    public override void RefreshShopDisplay()
    {
        CreateSampleItems();

        DeactivateAllItems();

        foreach (var itemUI in prebuiltItemUIs)
        {
            itemUI.gameObject.SetActive(false);
        }

        //필요하면 사용 -> 상품 분류별 관리 위의 일괄 상품 정리 대체용! 아래에 상품 별 새로고침을 통해 관리
        RefreshAdsSection();
        RefreshDealsSection();
        RefreshOneTimeSection();
        RefreshCoinSection();
        RefreshMoneySection();
    }

    private void RefreshAdsSection()
    {
        if (adsSection == null) return;
        ClearSectionItems(adsSection);

        var adsItems = shopItems.Where(item =>
            item.CanPurchase() &&
            item.itemID == "no_ads_pack"
        ).ToList();

        AssignItemsToSection(adsSection, adsItems);
    }

    private void RefreshDealsSection()
    {
        if (dealsSection == null) return;
        ClearSectionItems(dealsSection);

        var dealItems = shopItems.Where(item =>
            item.hasDiscount ||
            item.tags.Any(tag => tag.tagType == ShopTagType.Popular && tag.isActive) ||
            item.tags.Any(tag => tag.tagType == ShopTagType.BestValue && tag.isActive)
        ).ToList();

        AssignItemsToSection(dealsSection, dealItems);
    }

    private void RefreshOneTimeSection()
    {
        if (oneTimeSection == null) return;
        ClearSectionItems(oneTimeSection);

        var dealItems = shopItems.Where(item =>
            item.hasDiscount ||
            item.tags.Any(tag => tag.tagType == ShopTagType.Limited && tag.isActive)
        ).ToList();

        AssignItemsToSection(oneTimeSection, dealItems);
    }

    private void RefreshCoinSection()
    {
        if (coinSection == null) return;
        ClearSectionItems(coinSection);

        var coinItems = shopItems.Where(item => item.GetRewards().Count == 1 && item.GetRewards()[0].currencyID == "2" && item.tags.Any(tag => tag.tagType == ShopTagType.None && tag.isActive)).ToList();

        AssignItemsToSection(coinSection, coinItems);
    }

    private void RefreshMoneySection()
    {
        if (moneySection == null) return;
        ClearSectionItems(moneySection);

        var moneyItems = shopItems.Where(item => item.GetRewards().Count == 1 && item.GetRewards()[0].currencyID == "1" && item.tags.Any(tag => tag.tagType == ShopTagType.None && tag.isActive)).ToList();

        AssignItemsToSection(moneySection, moneyItems);
    }



    private void ClearSectionItems(Transform section)
    {
        if (sectionActiveItems.ContainsKey(section))
        {
            foreach (var itemUI in sectionActiveItems[section])
            {
                itemUI.gameObject.SetActive(false);
                itemUI.ResetItem();
            }
            sectionActiveItems[section].Clear();
        }
    }

    private void AssignItemsToSection(Transform section, List<ShopItemBase> items)
    {
        section.gameObject.SetActive(items.Count > 0);

        foreach (var item in items)
        {
            var itemUI = GetAvailableItemUI(item.itemID, item.tags[0].tagType);
            if (itemUI != null)
            {
                itemUI.transform.SetParent(section, false);
                itemUI.SetupItem(item, this);
                itemUI.gameObject.SetActive(true);

                if (!sectionActiveItems.ContainsKey(section))
                {
                    sectionActiveItems[section] = new List<ShopItemUI>();
                }
                sectionActiveItems[section].Add(itemUI);
            }
        }

        Debug.Log($"[{section.name}] {items.Count}개 아이템 배치");
    }

    protected ShopItemUI GetAvailableItemUI(string itemID, ShopTagType? requiredTag = null)
    {
        ShopItemUI availableUI;

        // 비활성화된 UI 요소 중 하나를 반환
        availableUI = prebuiltItemUIs.FirstOrDefault(x => x.itemID == itemID && !x.gameObject.activeSelf);

        if (availableUI != null)
        {
            availableUI.gameObject.SetActive(true);
            activeItemUIs.Add(availableUI);
            return availableUI;
        }

        Debug.LogWarning($"[{shopName}] 사용 가능한 UI 요소가 부족합니다. 추가 UI 요소를 배치해주세요.");
        return null;
    }

}