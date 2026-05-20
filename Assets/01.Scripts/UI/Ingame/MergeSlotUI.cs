using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;

/// <summary>
/// 머지 그리드 개별 슬롯 UI
/// </summary>
public class MergeSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Slot Visuals")]
    [SerializeField] private Image bgImage;
    [SerializeField] private Image lockIcon;
    [SerializeField] private GameObject cannonRoot;      // 캐논 표시 루트
    [SerializeField] private Image cannonIcon;           // 캐논 이미지
    [SerializeField] private Text levelText;             // 레벨 텍스트
    [SerializeField] private Image selectedBorder;       // 선택 테두리

    [Header("Slot Colors")]
    [SerializeField] private Color emptyColor    = new Color(0.79f, 0.72f, 0.59f, 1f);
    [SerializeField] private Color lockedColor   = new Color(0.70f, 0.63f, 0.52f, 1f);
    [SerializeField] private Color hasCannonColor= new Color(0.82f, 0.75f, 0.61f, 1f);

    [Header("Cannon Level Colors")]
    [SerializeField] private Color[] levelColors = new Color[]
    {
        new Color(0.91f, 0.25f, 0.25f, 1f), // Lv1 빨강
        new Color(0.91f, 0.47f, 0.13f, 1f), // Lv2 주황
        new Color(0.91f, 0.78f, 0.13f, 1f), // Lv3 노랑
        new Color(0.25f, 0.78f, 0.25f, 1f), // Lv4 초록
        new Color(0.25f, 0.50f, 0.91f, 1f), // Lv5 파랑
        new Color(0.56f, 0.25f, 0.91f, 1f), // Lv6 보라
        new Color(0.91f, 0.25f, 0.63f, 1f), // Lv7 분홍
    };

    private int slotIndex;
    private bool isLocked;
    private Action onClickCallback;
    private RectTransform rectTransform;

    public void Init(int index, bool locked, Action onClick)
    {
        slotIndex = index;
        isLocked = locked;
        onClickCallback = onClick;
        rectTransform = GetComponent<RectTransform>();

        if (isLocked)
        {
            bgImage.color = lockedColor;
            if (lockIcon) lockIcon.gameObject.SetActive(true);
            if (cannonRoot) cannonRoot.SetActive(false);
        }
    }

    public void Refresh(int level, bool selected)
    {
        if (isLocked) return;

        bool hasCannon = level > 0;

        bgImage.color = hasCannon ? hasCannonColor : emptyColor;
        if (cannonRoot) cannonRoot.SetActive(hasCannon);
        if (selectedBorder) selectedBorder.gameObject.SetActive(selected);

        if (hasCannon)
        {
            if (levelText)
                levelText.text = level.ToString();

            if (cannonIcon)
            {
                int colorIdx = Mathf.Clamp(level - 1, 0, levelColors.Length - 1);
                cannonIcon.color = levelColors[colorIdx];
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked) return;
        onClickCallback?.Invoke();
    }

    public void PlayMergeEffect()
    {
        StopAllCoroutines();
        StartCoroutine(MergePulse());
    }

    private IEnumerator MergePulse()
    {
        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 originalScale = Vector3.one;
        Vector3 peakScale = Vector3.one * 1.18f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = t < 0.5f
                ? Mathf.Lerp(1f, 1.18f, t * 2f)
                : Mathf.Lerp(1.18f, 1f, (t - 0.5f) * 2f);
            rectTransform.localScale = Vector3.one * scale;
            yield return null;
        }
        rectTransform.localScale = originalScale;
    }
}
