using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;

public class RewardManager : Singleton<RewardManager>
{
    #region 보상 데이터 구조
    [System.Serializable]
    public class RewardConfig
    {
        public string rewardId;
        public RewardType rewardType;
        public string targetId;
        public int amount;
        public Sprite icon;
    }

    public enum RewardType
    {
        Currency,
        Item,
        Unlockable,
        Energy
    }
    #endregion

    [System.Serializable]
    public class IAPRewardMapping
    {
        public string iapProductId;
        public string rewardId;
    }

    #region 보상 이벤트
    public static event Action<string, int> OnCurrencyRewarded; // currencyId, amount
    public static event Action<string, int> OnItemRewarded; // itemId, quantity
    public static event Action<string> OnUnlockableRewarded; // unlockableId
    #endregion

    [Header("보상 설정")]
    [SerializeField] private List<RewardConfig> _rewardConfigs = new List<RewardConfig>();

    private Dictionary<string, RewardConfig> _rewardCache = new Dictionary<string, RewardConfig>();

    [Header("IAP-보상 매핑")]
    [SerializeField] private List<IAPRewardMapping> _iapRewardMappings = new List<IAPRewardMapping>();

    private Dictionary<string, string> _iapRewardMap = new Dictionary<string, string>();

    private Dictionary<string, IRewardProvider> _rewardProviders = new Dictionary<string, IRewardProvider>();

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();
        InitializeRewardCache();
        InitializeIAPRewardMap();
        
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccess.RemoveListener(HandlePurchaseSuccess);
            IAPManager.Instance.OnPurchaseSuccess.AddListener(HandlePurchaseSuccess);
        }
    }

    protected override void OnDestroy()
    {
        if (IAPManager.Instance != null)
            IAPManager.Instance.OnPurchaseSuccess.RemoveListener(HandlePurchaseSuccess);
            
        base.OnDestroy();
    }

    #region 초기화
    private void InitializeRewardCache()
    {
        _rewardCache.Clear();
        foreach (var config in _rewardConfigs)
        {
            if (!_rewardCache.ContainsKey(config.rewardId))
            {
                _rewardCache.Add(config.rewardId, config);
            }
        }
    }

    private void InitializeIAPRewardMap()
    {
        _iapRewardMap.Clear();
        foreach (var mapping in _iapRewardMappings)
        {
            if (!_iapRewardMap.ContainsKey(mapping.iapProductId))
            {
                _iapRewardMap.Add(mapping.iapProductId, mapping.rewardId);
            }
        }
    }
    #endregion

    #region 보상 지급 시스템
    public void RegisterRewardProvider(string id, IRewardProvider provider)
    {
        if (_rewardProviders.ContainsKey(id))
        {
            Debug.LogWarning($"이미 등록된 아이템 ID: {id}");
            return;
        }
        _rewardProviders.Add(id, provider);
    }

    public void GrantReward(string rewardId)
    {
        Debug.LogError($"GrantReward : {rewardId}");


        if (!_rewardCache.TryGetValue(rewardId, out RewardConfig config))
        {
            Debug.LogError($"보상 ID를 찾을 수 없음: {rewardId}");
            return;
        }
        Debug.LogError($"GrantReward config : {config}");

        switch (config.rewardType)
        {
            case RewardType.Currency:
                GrantCurrency(config.targetId, config.amount);
                break;
            case RewardType.Item:
                GrantItem(config.targetId, config.amount);
                break;
            case RewardType.Unlockable:
                GrantUnlockable(config.targetId);
                break;
            case RewardType.Energy:
                GrantEnergy(config.amount);
                break;
            default:
                Debug.LogWarning($"지원되지 않는 보상 유형: {config.rewardType}");
                break;
        }

        // AnalyticsEvent 사용 대신 Debug.Log으로 대체
        Debug.Log($"보상 지급 완료: {rewardId}, 유형: {config.rewardType}");
    }

    public void GrantRewardByItemId(string itemId)
    {
        Debug.Log($"GrantRewardBy ItemId : {itemId}");

        if (_rewardProviders.TryGetValue(itemId, out var provider))
        {
            ApplyRewards(provider.GetRewards());
        }
        else
        {
            Debug.LogError($"보상 제공자가 등록되지 않음: {itemId}");
        }
    }

    private void ApplyRewards(List<ShopReward> rewards)
    {
        foreach (var reward in rewards)
        {
            CurrencyManager.Instance.AddCurrency(reward.currencyID, reward.amount);
            Debug.Log($"보상 지급: {reward.currencyID} x {reward.amount}");
        }
    }

    public void GrantCurrency(string currencyId, int amount)
    {
        CurrencyManager.Instance.AddCurrency(currencyId, amount);
        OnCurrencyRewarded?.Invoke(currencyId, amount);
    }

    private void GrantItem(string itemId, int quantity)
    {
        OnItemRewarded?.Invoke(itemId, quantity);
    }

    private void GrantUnlockable(string unlockableId)
    {
        OnUnlockableRewarded?.Invoke(unlockableId);
    }

    private void GrantEnergy(int amount)
    {
        Debug.Log($"에너리 {amount} 추가");
    }
    #endregion

    #region IAP 통합
    public void HandlePurchaseSuccess(string productId)
    {
        // 1. 먼저 _rewardProviders에서 직접 찾기
        if (_rewardProviders.TryGetValue(productId, out var provider))
        {
            Debug.Log($"상품 {productId}의 보상 지급 시작");
            // GrantRewardByItemId(productId);
            return;
        }

        // 2. _iapRewardMap에서 매핑 찾기 (기존 방식)
        if (_iapRewardMap.TryGetValue(productId, out string rewardId))
        {
            // GrantReward(rewardId);
            return;
        }

        // 3. 둘 다 없으면 경고
        Debug.LogWarning($"구매 상품에 대한 보상 매핑이 없음: {productId}");
        
        // 4. 기본 보상 처리 (옵션)
        // if (productId.StartsWith("coin_"))
        // {
        //     int amount = int.Parse(productId.Split('_')[1]);
        //     GrantCurrency("coin_1", amount);
        // }
    }
    #endregion

    #region 에디터 유틸리티
#if UNITY_EDITOR
    [ContextMenu("보상 캐시 리로드")]
    private void ReloadRewardCache()
    {
        InitializeRewardCache();
        Debug.Log($"보상 캐시 갱신 완료. 총 {_rewardCache.Count}개 항목");
    }
#endif
    #endregion
}