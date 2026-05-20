using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuffIcon : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image cooldownFill;
    [SerializeField] private TextMeshProUGUI timerText;

    private float maxDuration;

    public void Initialize(Sprite icon, float maxDuration)
    {
        iconImage.sprite = icon;
        this.maxDuration = maxDuration;
        cooldownFill.fillAmount = 1f;
        UpdateTimer(maxDuration);
    }

    public void UpdateStatus(float remaining)
    {
        cooldownFill.fillAmount = remaining / maxDuration;
        UpdateTimer(remaining);
    }

    private void UpdateTimer(float time)
    {
        timerText.text = Mathf.CeilToInt(time).ToString();
    }
}