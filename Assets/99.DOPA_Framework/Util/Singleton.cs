using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new();
    private static bool _isApplicationQuitting = false;
    private static bool _isInitialized = false;
    private static bool _hasCalledOnSingletonInitialized = false;

    public static T Instance
    {
        get
        {
            if (_isApplicationQuitting)
            {
                Debug.Log($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ [Singleton] Instance '{typeof(T)}' already destroyed. Returning null.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    var instances = FindObjectsByType<T>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    );

                    if (instances.Length > 0)
                    {
                        _instance = instances[0];

                        if (instances.Length > 1)
                        {
                            Debug.Log("~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ [Singleton] Multiple instances found. Destroying extras.");
                            for (int i = 1; i < instances.Length; i++)
                            {
                                Destroy(instances[i].gameObject);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ '{typeof(T)}' [Singleton] instance Null.");

                        return _instance;

                        // GameObject singletonObj = new GameObject($"{typeof(T).Name} (Singleton)");
                        // _instance = singletonObj.AddComponent<T>();
                    }

                    _isInitialized = true;
                    // DontDestroyOnLoad(_instance.gameObject);

                    if (!_hasCalledOnSingletonInitialized)
                    {
                        _hasCalledOnSingletonInitialized = true;
                        (_instance as Singleton<T>)?.OnSingletonInitialized();
                    }
                }
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        lock (_lock)
        {
            if (_instance == null)
            {
                Debug.Log($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ [ {gameObject.name} ] [Singleton] instance Null.");

                _instance = this as T;
                _isInitialized = true;
                if (!IsDontDestroyOnLoad(gameObject))
                {
                    DontDestroyOnLoad(gameObject);
                    Debug.Log($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ [ {gameObject.name} ] [Singleton] DontDestroyOnLoad applied.");
                }
                else
                {
                    Debug.Log($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ [ {gameObject.name} ] [Singleton] Already DontDestroyOnLoad.");
                }

                if (!_hasCalledOnSingletonInitialized)
                {
                    Debug.Log($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ [ {gameObject.name} ] [Singleton] _hasCalledOnSingletonInitialized True.");

                    _hasCalledOnSingletonInitialized = true;
                    OnSingletonInitialized();
                }
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$  [ {gameObject.name} ] [Singleton] Duplicate instance destroyed.");
                Destroy(gameObject);
            }
            else if (!_hasCalledOnSingletonInitialized)
            {
                Debug.Log($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$  [ {gameObject.name} ] [Singleton] _hasCalledOnSingletonInitialized True.");

                _hasCalledOnSingletonInitialized = true;
                OnSingletonInitialized();
            }
        }
    }

    protected virtual void OnSingletonInitialized() { }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _isInitialized = false;
            _hasCalledOnSingletonInitialized = false;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
    }

    public static bool IsInitialized => _isInitialized && _instance != null;


    public virtual void PostInitialize() { }

    public virtual void ExecutePostInitialization()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning($"~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$~~~@$ [Singleton<{typeof(T).Name}>] Attempted to call InitializeSettings, but instance is not initialized.");
            return;
        }

        PostInitialize();
    }


    private static bool IsDontDestroyOnLoad(GameObject gameObject)
    {
        Transform current = gameObject.transform;
        while (current != null)
        {
            if (current.parent == null)
            {
                return current.gameObject.scene.name == "DontDestroyOnLoad";
            }
            current = current.parent;
        }
        return false;
    }
}