using UnityEngine;

public abstract class GameBase : MonoBehaviour
{
    protected GameManager gameManager;

    [Header("UI Settings")]
    [SerializeField] protected RectTransform gameUI;

    [SerializeField] protected int winScore = 100;

    [SerializeField] protected Transform[] gameContainers;

    public Transform[] GameContainers
    {
        get => gameContainers;
        set => gameContainers = value;
    }

    public virtual void Initialize(GameManager manager)
    {
        gameManager = manager;
        InitializeGameComponents();
        SetupUI();
    }

    protected virtual void InitializeGameComponents()
    {
        
    }

    protected virtual void SetupUI()
    {
        if (gameUI == null)
        {
            return;
        }

        if (UIManager.Instance == null)
        {
            Debug.LogError("[GameBase] UIManager.Instance is null.");
            return;
        }

        if (UIManager.Instance.canvasUiList == null || UIManager.Instance.canvasUiList.Length == 0)
        {
            Debug.LogError("[GameBase] UIManager canvasUiList is not set.");
            return;
        }

        gameUI.SetParent(UIManager.Instance.canvasUiList[0].transform);
        gameUI.SetAsFirstSibling();
        gameUI.anchoredPosition = Vector2.zero;
        gameUI.sizeDelta = Vector2.zero;
    }

    public virtual void StartGameplay()
    {
        Debug.Log("[GameBase] StartGameplay");
    }

    public virtual void PauseGameplay()
    {
        Debug.Log("[GameBase] PauseGameplay");
    }

    public virtual void ResumeGameplay()
    {
        Debug.Log("[GameBase] ResumeGameplay");
    }

    public virtual void Restart()
    {
        Debug.Log("[GameBase] Restart");
        InitializeGameComponents();
    }

    public virtual void ReturnToHome()
    {
        Debug.Log("[GameBase] ReturnToHome");
        InitializeGameComponents();
    }

    public virtual void StartGame() => gameManager?.StartGame();
    public virtual void PauseGame() => gameManager?.PauseGame();
    public virtual void ResumeGame() => gameManager?.ResumeGame();

    public virtual void GameOver() => gameManager?.RequestGameOver();

    public bool IsGameActive
    {
        get
        {
            if (GameStateManager.Instance == null)
            {
                return false;
            }

            return GameStateManager.Instance.CurrentState == GameState.Playing;
        }
    }

    public virtual void OnStartJoystickControll()
    {
    }

    public virtual void OnControllJoystick(Vector2 joyPos)
    {
    }

    public abstract int GetCurrentScore();

    public abstract bool CheckWinCondition();
}