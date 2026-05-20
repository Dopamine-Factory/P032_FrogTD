using UnityEngine;
using UnityEngine.Events;

public enum GameState { Home, Playing, Paused, GameOver }
[System.Serializable] public class GameStateEvent : UnityEvent<GameState> { }

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    public GameStateEvent OnStateChanged = new();
    public GameState CurrentState { get; private set; } = GameState.Home;
    private GameBase currentGame;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Initialize(GameBase game)
    {
        currentGame = game;
        Debug.Log("[GameStateManager] Initialized with " + game.name);
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        Debug.Log($"[GameStateManager] State {CurrentState} → {newState}");

        CurrentState = newState;
        OnStateChanged.Invoke(newState);

        switch (newState)
        {
            case GameState.Home:    HandleHome();    break;
            case GameState.Playing: HandlePlaying(); break;
            case GameState.Paused:  HandlePaused();  break;
            case GameState.GameOver:HandleGameOver();break;
        }
    }

    private void HandleHome()
    {
        // AdvertisingRootManager.Instance?.ShowInterstitial();

        currentGame?.ReturnToHome();
    }

    private void HandlePlaying()
    {
        Time.timeScale = 1f;
    }

    private void HandlePaused()
    {
        Debug.Log("[GameStateManager] HandlePaused");
        
        Time.timeScale = 0f;
    }

    private void HandleGameOver()
    {
        if (currentGame == null) return;

        

        // UIManager.Instance.GameOverPopupOnOff(true, isWin, score.ToString());
    }
}