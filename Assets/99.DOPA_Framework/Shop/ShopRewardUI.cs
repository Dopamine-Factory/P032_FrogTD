using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopRewardUI : MonoBehaviour
{
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TextMeshProUGUI rewardAmount;

    /// <summary>
    /// 보상 정보를 받아 UI를 세팅합니다.
    /// </summary>
    public void SetupReward(ShopReward reward)
    {
        // 아이콘
        if (rewardIcon != null)
        {
            if (reward.rewardIcon != null)
            {
                rewardIcon.sprite = reward.rewardIcon;
            }
            else
            {
                // CurrencyManager에서 아이콘 가져오기
                var currency = CurrencyManager.Instance.HasCurrency(reward.currencyID)
                    ? CurrencyManager.Instance.GetCurrency(reward.currencyID)
                    : null;
                rewardIcon.sprite = currency != null ? currency.icon : null;
            }
        }

        // 수량
        if (rewardAmount != null)
            rewardAmount.text = $"x{reward.amount:N0}";
    }
}