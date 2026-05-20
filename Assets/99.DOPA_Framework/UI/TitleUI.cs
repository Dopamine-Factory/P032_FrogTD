using UnityEngine;
using UnityEngine.UI;

public class TitleUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GaugeBarBase loadingBar;
    [SerializeField] private Button startButton;

    public bool IsAutoStart = true;

    void Awake()
    {
        InitializeManager.Instance.OnChangedProgressValue += OnChangedProgressValue;

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
            startButton.onClick.AddListener(OnClickStartBtn);
        }
    }

    private void OnChangedProgressValue(float value)
    {
        loadingBar.SetGaugePercent(value);

        if (value >= 1)
        {
            if (IsAutoStart || startButton == null)
            {
                OnClickStartBtn();
            }
            else
            {
                startButton.gameObject.SetActive(true);
            }
        }
    }

    private void OnClickStartBtn()
    {
        if (startButton != null)
            startButton.onClick.RemoveAllListeners();

        InitializeManager.Instance.StartGame();
    }

}
