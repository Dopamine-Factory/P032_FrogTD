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

    public virtual void Initialize()
    {
        InitializeGameComponents();

        SetupUI();

        GameManager.GameState.Subscribe(GameStateController.GameState.GameStart, GameStart);
        GameManager.GameState.Subscribe(GameStateController.GameState.GameClear, GameClear);
        GameManager.GameState.Subscribe(GameStateController.GameState.GameOver, GameOver);
        GameManager.GameState.Subscribe(GameStateController.GameState.Resume, GameResume);
        GameManager.GameState.Subscribe(GameStateController.GameState.Paused, GamePause);
    }

    public virtual void Dispose()
    {
        GameManager.GameState.Unsubscribe(GameStateController.GameState.GameStart, GameStart);
        GameManager.GameState.Unsubscribe(GameStateController.GameState.GameClear, GameClear);
        GameManager.GameState.Unsubscribe(GameStateController.GameState.GameOver, GameOver);
        GameManager.GameState.Unsubscribe(GameStateController.GameState.Resume, GameResume);
        GameManager.GameState.Unsubscribe(GameStateController.GameState.Paused, GamePause);
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

    public virtual void Restart()
    {
        InitializeGameComponents();
    }

    public virtual void ReturnToHome()
    {
        InitializeGameComponents();
    }

    public virtual void GameStart() { }
    public virtual void GamePause() { }
    public virtual void GameResume() { }
    public virtual void GameClear() { }
    public virtual void GameOver() { }

    public virtual void OnStartJoystickControll() { }
    public virtual void OnControllJoystick(Vector2 joyPos) { }

}