using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ObjectPoolManager : MonoBehaviour
{
    [Serializable]
    private class PoolConfig
    {
        public string poolKey;
        public MonoBehaviour prefab;
        public int defaultCapacity = 50;
        public int maxSize = 200;
        public bool warmUpOnAwake = true;
    }

    private sealed class PooledObjectTag : MonoBehaviour
    {
        [SerializeField] private string poolKey;

        public string PoolKey => poolKey;

        public void SetPoolKey(string key)
        {
            poolKey = key;
        }
    }

    public static ObjectPoolManager Instance { get; private set; }

    public event Action<IPoolable> OnAcquired;
    public event Action<IPoolable> OnReleased;

    [Header("Pools (Inspector)")]
    [SerializeField] private List<PoolConfig> poolConfigs = new();

    [Header("Debug")]
    [SerializeField] private bool enableCollectionCheck = true;

    private readonly Dictionary<string, ObjectPool<IPoolable>> poolsByKey = new();
    private readonly Dictionary<Type, string> poolKeyByType = new();

    private Transform poolParent;
    public Transform PoolParent { get => poolParent; set => poolParent = value; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        poolParent = new GameObject("=== Pooled Objects ===").transform;
        poolParent.SetParent(transform, false);

        RegisterPoolsFromInspector();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ClearPools();
            Instance = null;
        }
    }

    private void RegisterPoolsFromInspector()
    {
        for (int i = 0; i < poolConfigs.Count; i++)
        {
            var config = poolConfigs[i];
            if (config == null) continue;

            if (string.IsNullOrEmpty(config.poolKey))
            {
                Debug.LogError("[ObjectPoolManager] poolKey is null or empty.");
                continue;
            }

            if (config.prefab == null)
            {
                Debug.LogError($"[ObjectPoolManager] Prefab is null. poolKey={config.poolKey}");
                continue;
            }

            var prefabPoolable = config.prefab as IPoolable;
            if (prefabPoolable == null)
            {
                Debug.LogError($"[ObjectPoolManager] Prefab must implement IPoolable. poolKey={config.poolKey}, prefab={config.prefab.name}");
                continue;
            }

            RegisterPoolInternal(config.poolKey, prefabPoolable, config.defaultCapacity, config.maxSize, config.warmUpOnAwake);
        }
    }

    private void RegisterPoolInternal(string poolKey, IPoolable prefab, int defaultCapacity, int maxSize, bool warmUp)
    {
        if (poolsByKey.ContainsKey(poolKey))
        {
            Debug.LogWarning($"[ObjectPoolManager] Pool already registered. poolKey={poolKey}");
            return;
        }

        var prefabType = prefab.GetType();
        if (!poolKeyByType.ContainsKey(prefabType))
        {
            poolKeyByType[prefabType] = poolKey;
        }

        ObjectPool<IPoolable> pool = null;

        pool = new ObjectPool<IPoolable>(
            createFunc: () => CreateInstance(poolKey, prefab),
            actionOnGet: obj =>
            {
                obj.GameObject.SetActive(true);
                obj.OnAcquire();
                OnAcquired?.Invoke(obj);
            },
            actionOnRelease: obj =>
            {
                obj.OnRelease();
                obj.GameObject.SetActive(false);
                OnReleased?.Invoke(obj);
            },
            actionOnDestroy: obj =>
            {
                if (obj != null && obj.GameObject != null)
                {
                    Destroy(obj.GameObject);
                }
            },
            collectionCheck: enableCollectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );

        poolsByKey[poolKey] = pool;

        if (warmUp)
        {
            for (int i = 0; i < defaultCapacity; i++)
            {
                var obj = pool.Get();
                pool.Release(obj);
            }
        }

        Debug.Log($"[ObjectPoolManager] Pool registered. poolKey={poolKey}, cap={defaultCapacity}/{maxSize}");
    }

    private IPoolable CreateInstance(string poolKey, IPoolable prefab)
    {
        var instanceGo = Instantiate(prefab.GameObject);
        instanceGo.SetActive(false);
        instanceGo.transform.SetParent(poolParent, false);

        var tag = instanceGo.GetComponent<PooledObjectTag>();
        if (tag == null)
        {
            tag = instanceGo.AddComponent<PooledObjectTag>();
        }
        tag.SetPoolKey(poolKey);

        var poolable = instanceGo.GetComponent<IPoolable>();
        if (poolable == null)
        {
            Debug.LogError($"[ObjectPoolManager] Instantiated object missing IPoolable. poolKey={poolKey}, name={instanceGo.name}");
        }

        return poolable;
    }

    public T Acquire<T>(Action<T> onAcquire = null) where T : MonoBehaviour, IPoolable
    {
        var poolKey = GetPoolKeyByType(typeof(T));
        if (string.IsNullOrEmpty(poolKey)) return null;

        var obj = Acquire(poolKey);
        if (obj == null) return null;

        var component = obj.GetComponent<T>();
        if (component == null)
        {
            Debug.LogError($"[ObjectPoolManager] Acquired object missing component {typeof(T).Name}. poolKey={poolKey}");
            Release(obj, poolKey);
            return null;
        }

        onAcquire?.Invoke(component);
        return component;
    }

    public GameObject Acquire(string poolKey)
    {
        if (!poolsByKey.TryGetValue(poolKey, out var pool))
        {
            Debug.LogError($"[ObjectPoolManager] Pool not found. poolKey={poolKey}");
            return null;
        }

        var poolable = pool.Get();
        return poolable.GameObject;
    }

    public void Release<T>(T obj) where T : MonoBehaviour, IPoolable
    {
        if (obj == null) return;

        var tag = obj.GameObject.GetComponent<PooledObjectTag>();
        if (tag != null)
        {
            Release(obj.GameObject, tag.PoolKey);
            return;
        }

        var poolKey = GetPoolKeyByType(typeof(T));
        if (string.IsNullOrEmpty(poolKey))
        {
            Destroy(obj.GameObject);
            return;
        }

        Release(obj.GameObject, poolKey);
    }

    public void Release(GameObject obj, string poolKey)
    {
        if (obj == null) return;

        if (!poolsByKey.TryGetValue(poolKey, out var pool))
        {
            Debug.LogError($"[ObjectPoolManager] Pool not found on Release. poolKey={poolKey}");
            Destroy(obj);
            return;
        }

        var poolable = obj.GetComponent<IPoolable>();
        if (poolable == null)
        {
            Debug.LogError($"[ObjectPoolManager] Released object missing IPoolable. poolKey={poolKey}, name={obj.name}");
            Destroy(obj);
            return;
        }

        pool.Release(poolable);
    }

    public void Release(GameObject obj)
    {
        if (obj == null) return;

        var tag = obj.GetComponent<PooledObjectTag>();
        if (tag == null)
        {
            Debug.LogError("[ObjectPoolManager] PooledObjectTag missing. Use Release(obj, poolKey) or acquire from this manager.");
            Destroy(obj);
            return;
        }

        Release(obj, tag.PoolKey);
    }

    private string GetPoolKeyByType(Type type)
    {
        if (!poolKeyByType.TryGetValue(type, out var poolKey))
        {
            Debug.LogError($"[ObjectPoolManager] Pool key not mapped for type {type.Name}. Add it in Inspector pools list.");
            return string.Empty;
        }

        return poolKey;
    }

    public void ClearPools()
    {
        foreach (var kvp in poolsByKey)
        {
            kvp.Value.Clear();
        }

        poolsByKey.Clear();
        poolKeyByType.Clear();
    }
}