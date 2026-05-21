using UnityEngine;

public abstract class GameBase : MonoBehaviour
{
    protected GameManager gameManager;

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

        GameManager.GameState.Subscribe(GameState.GameStart, GameStart);
        GameManager.GameState.Subscribe(GameState.GameClear, GameClear);
        GameManager.GameState.Subscribe(GameState.GameOver, GameOver);
        GameManager.GameState.Subscribe(GameState.Resume, GameResume);
        GameManager.GameState.Subscribe(GameState.Paused, GamePause);
    }

    public virtual void Dispose()
    {
        GameManager.GameState.Unsubscribe(GameState.GameStart, GameStart);
        GameManager.GameState.Unsubscribe(GameState.GameClear, GameClear);
        GameManager.GameState.Unsubscribe(GameState.GameOver, GameOver);
        GameManager.GameState.Unsubscribe(GameState.Resume, GameResume);
        GameManager.GameState.Unsubscribe(GameState.Paused, GamePause);
    }

    protected virtual void InitializeGameComponents()
    {

    }

    protected virtual void SetupUI()
    {

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