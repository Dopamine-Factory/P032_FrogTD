using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopTagUI : MonoBehaviour
{
    [SerializeField] private Image tagBackground;
    [SerializeField] private Image tagIcon;
    [SerializeField] private TextMeshProUGUI tagText;

    /// <summary>
    /// 태그 정보를 받아 UI를 세팅합니다.
    /// </summary>
    public void SetupTag(ShopTag tag)
    {
        if (tagBackground != null)
            tagBackground.color = tag.tagColor;

        if (tagIcon != null)
            tagIcon.sprite = tag.tagIcon;

        if (tagText != null)
            tagText.text = string.IsNullOrEmpty(tag.customText) ? tag.tagType.ToString() : tag.customText;
    }
}