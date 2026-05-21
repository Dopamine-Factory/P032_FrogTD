using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("System References")]
    [SerializeField] private GameBase currentGame;

    GameStateController gameState;
    public static GameStateController GameState => instance.gameState;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        gameState = new GameStateController();
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

        currentGame.Initialize();
    }


    public void WebViewOnOff(int type)
    {
        // if (webViewManager == null)
        // {
        //     return;
        // }

        // webViewManager.ToggleWebView();
    }

}