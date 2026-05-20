using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IRewardProvider
{
    public List<ShopReward> GetRewards();
}

[Serializable]
public class ShopReward
{
    public string currencyID;
    public int amount;
    public Sprite rewardIcon;
    
    public static ShopReward operator +(ShopReward a, ShopReward b)
    {
        if (a.currencyID != b.currencyID) 
            throw new ArgumentException("Currency ID mismatch");
            
        return new ShopReward {
            currencyID = a.currencyID,
            amount = a.amount + b.amount,
            rewardIcon = a.rewardIcon
        };
    }
}

[Serializable]
public class ShopPrice
{
    public string currencyID;
    public int amount;
}

public enum ShopItemType
{
    Single,
    Package
}

public enum ShopTagType
{
    None,
    Popular,
    Discount,
    BestValue,
    Limited,
    New
}

[Serializable]
public class ShopTag
{
    public ShopTagType tagType;
    public string customText;
    public Sprite tagIcon;
    public Color tagColor = Color.white;
    public bool isActive;
}

[System.Serializable]
public abstract class ShopItemBase : IRewardProvider
{
    [Header("기본 정보")]
    public string itemID;
    public string displayName;
    [TextArea]
    public string description;
    public Sprite itemIcon;
    public ShopItemType itemType;

    [Header("보상/가격")]
    public List<ShopReward> rewards = new List<ShopReward>();
    public List<ShopPrice> prices = new List<ShopPrice>();
    public List<ShopTag> tags = new List<ShopTag>();

    [Header("IAP 설정")]
    public bool isIAPItem = false;
    public string iapProductID;

    [Header("할인 설정")]
    public bool hasDiscount = false;
    [Range(0, 100)]
    public int discountPercentage = 0;
    public List<ShopPrice> originalPrices = new List<ShopPrice>();

    [Header("구매 제한/상태")]
    public bool isAvailable = true;
    public bool isLimitedTime = false;
    public DateTime expirationTime;
    public int purchaseLimit = -1; // -1 = 무제한
    public int currentPurchaseCount = 0;

    public virtual bool CanPurchase()
    {
        if (!isAvailable) return false;
        if (isLimitedTime && DateTime.Now > expirationTime) return false;
        if (purchaseLimit > 0 && currentPurchaseCount >= purchaseLimit) return false;
        return true;
    }

    public virtual List<ShopReward> GetRewards()
    {
        return rewards;
    }

    public virtual void OnPurchased()
    {
        currentPurchaseCount++;
        foreach (var reward in rewards)
        {
            CurrencyManager.Instance.AddCurrency(reward.currencyID, reward.amount);
        }
    }
}

[System.Serializable]
public class SingleShopItem : ShopItemBase
{
    public SingleShopItem()
    {
        itemType = ShopItemType.Single;
    }
}

[System.Serializable]
public class PackageShopItem : ShopItemBase
{
    [Header("패키지 구성")]
    public List<SingleShopItem> packageItems = new List<SingleShopItem>();
    public float packageDiscountRate = 0.1f;

    public PackageShopItem()
    {
        itemType = ShopItemType.Package;
    }

    public override List<ShopReward> GetRewards()
    {
        var rewardDict = new Dictionary<string, ShopReward>();
        
        foreach (var r in base.GetRewards())
        {
            rewardDict[r.currencyID] = new ShopReward {
                currencyID = r.currencyID,
                amount = r.amount
            };
        }
        
        foreach (var item in packageItems)
        {
            foreach (var r in item.GetRewards())
            {
                if (rewardDict.TryGetValue(r.currencyID, out var existing))
                {
                    existing.amount += r.amount;
                }
                else
                {
                    rewardDict[r.currencyID] = new ShopReward {
                        currencyID = r.currencyID,
                        amount = r.amount
                    };
                }
            }
        }
        
        return rewardDict.Values.ToList();
    }


    public override void OnPurchased()
    {
        base.OnPurchased();

        foreach (var item in packageItems)
        {
            foreach (var reward in item.rewards)
            {
                CurrencyManager.Instance.AddCurrency(reward.currencyID, reward.amount);
            }
        }
    }
}