using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("Game Events")]
    public UnityEvent onGameStateChanged = new UnityEvent();

    [Header("System References")]
    [SerializeField] private EnhancedSafeArea enhancedSafeArea;
    [SerializeField] private GameBase currentGame;
    public EnhancedSafeArea EnhancedSafeArea => enhancedSafeArea;
    public GameBase CurrentGame => currentGame;
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RegisterGame(currentGame);

    }

    public void RegisterGame(GameBase game)
    {
        currentGame = game;

        if (currentGame == null)
        {
            Debug.LogError("[GameManager] currentGame is null.");
            return;
        }

        currentGame.Initialize(this);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.Initialize(currentGame);
        }

        currentGame.StartGame();

    }


    #region Public Interface (UI 버튼용)

    public void StartGame()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Playing);
        }

        currentGame?.StartGameplay();
    }

    public void PauseGame()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Paused);
        }

        currentGame?.PauseGameplay();
    }

    public void ResumeGame()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Playing);
        }

        currentGame?.ResumeGameplay();
    }

    public void RestartGame()
    {
        currentGame?.Restart();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Home);
        }
    }

    public void ReturnToHome()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Home);
        }
    }

    #endregion

    #region Legacy Request API (호환 유지)

    public void RequestStartGame() => StartGame();
    public void RequestPauseGame() => PauseGame();
    public void RequestResumeGame() => ResumeGame();

    public void RequestGameOver()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.GameOver);
        }
    }

    public void RequestRestart() => RestartGame();

    #endregion


    public void WebViewOnOff(int type)
    {
        // if (webViewManager == null)
        // {
        //     return;
        // }

        // webViewManager.ToggleWebView();
    }

}