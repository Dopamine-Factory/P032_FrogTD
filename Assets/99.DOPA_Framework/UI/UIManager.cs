using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }


    public Canvas[] canvasUiList;

    public EnhancedSafeArea enhancedSafeArea;

    [Header("Canvas Pools")]
    [SerializeField] private List<PopupManager.CanvasPool> sceneCanvasPools = new();

    private void Awake() => InitializeSingleton();

    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (PopupManager.Instance != null)
        {
            PopupManager.Instance.CleanupAndRefreshCanvasPools();
            PopupManager.Instance.RegisterCanvasPools(sceneCanvasPools);
            Debug.Log($"UIManager : Registered {sceneCanvasPools.Count} CanvasPools to PopupManager.");
        }
    }


    public void GameOverPopupOnOff(bool isOn = false, bool isWin = false, string scoreStr = "0")
    {
        if (isOn)
        {
            string title = isWin ? "Victory!" : "Game Over";
            PopupManager.Instance.ShowPopup<GameOverPopup>(
                title,
                scoreStr,
                popup =>
                {
                    popup.ResultSetting(isWin);
                },
                PopupManager.PopupPriority.Notice
            );
        }
        else
        {
            PopupManager.Instance.ClosePopup<GameOverPopup>();
        }

        PlayButtonSound(1);
    }

    public void UpdatePopupOnOff(bool isOn = false)
    {
        Debug.Log($"[UIManager] UpdatePopupOnOff isOn : {isOn}");

        if (isOn)
        {
            PopupManager.Instance.ShowPopup<PopupUpdate>();
        }
        else
        {
            PopupManager.Instance.ClosePopup<PopupUpdate>();
        }
    }

    public void SettingPopupOnOff(bool isOn = false)
    {
        Debug.Log($"[UIManager] SettingPopupOnOff isOn : {isOn}");

        if (isOn)
        {
            PopupManager.Instance.ShowPopup<SettingPopup>(
            null,
            null,
            null,
            PopupManager.PopupPriority.System);
        }
        else
        {
            PopupManager.Instance.ClosePopup<SettingPopup>();
        }
        PlayButtonSound(1);
    }

    public void ShopPopupOnOff(bool isOn = false)
    {
        if (!PopupManager.Instance.IsReady)
        {
            Debug.LogWarning("[UIManager] PopupManager Not Ready for MainShop");
            return;
        }

        if (!isOn)
        {
            PopupManager.Instance.ClosePopupByKey("MainShop");
            return;
        }

        PopupManager.Instance.ShowPopup<MainShop>(
            null,
            null,
            popup =>
            {
                Debug.Log("[UIManager] MainShop opened with auto-refresh");
            },
            PopupManager.PopupPriority.Main
        );

        PlayButtonSound(1);
    }

    public void OnRestartButton()
    {
        PlayButtonSound(2);
    }

    public void OnHomeButton()
    {
        PlayButtonSound(3);
    }


    public void OnWebViewButton(int type) => GameManager.Instance.WebViewOnOff(type);

    private void PlayButtonSound(int index) => SoundManager.Instance.PlayEffect($"BUTTON_0{index}");
}