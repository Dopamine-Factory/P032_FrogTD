using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;

public class ResourceManager : BaseSystemManager<ResourceManager>
{
    SpriteAtlas atlas;
    readonly Dictionary<Type, Component> prefabDic = new();
    readonly Dictionary<Type, object> poolDic = new();
    readonly Dictionary<Type, HashSet<Component>> poolActiveComponentDic = new();
    readonly Dictionary<Type, Action<Component>> releaseComponentDic = new();

    readonly Dictionary<uint, ResourceData> resourcePrefabDic = new();
    readonly Dictionary<uint, ObjectPool<ResourceData>> resourceDic = new();
    readonly Dictionary<uint, HashSet<ResourceData>> resourceActiveDic = new();
    readonly Dictionary<uint, Action<ResourceData>> resourcereleaseDic = new();



    int totalQueue = 0;
    int completeQueue = 0;

    #region  Component
    private async UniTask Load<T>(int preMakeCount = 10) where T : Component
    {
        Type type = typeof(T);

        var handle = Addressables.LoadAssetAsync<GameObject>(type.Name);
        var prefab = await handle.Task;

        if (prefab != null)
        {
            prefabDic[type] = prefab.GetComponent<T>();

            var pool = new ObjectPool<T>(InstantiateT<T>);

            poolDic[type] = pool;

            releaseComponentDic[type] = (comp) => pool.Release((T)comp);

            for (int i = 0; i < preMakeCount; ++i)
            {
                var obj = pool.Get();
                obj.transform.SetParent(transform);
                obj.gameObject.SetActive(false);

                pool.Release(obj);
            }
        }

        OnLoaded();
    }

    private T InstantiateT<T>() where T : Component
    {
        var prefab = GetPrefab<T>();
        if (prefab != null)
        {
            var instance = Instantiate(prefab.gameObject, transform);
            if (instance != null) return instance.GetComponent<T>();
        }

        return null;
    }

    private T GetPrefab<T>() where T : Component
    {
        if (prefabDic.TryGetValue(typeof(T), out var prefab))
            return prefab as T;

        return null;
    }

    public ObjectPool<T> GetPool<T>() where T : Component
    {
        if (poolDic.TryGetValue(typeof(T), out var pool))
            return pool as ObjectPool<T>;

        Debug.LogError($"Pool not found: {typeof(T)}");
        return null;
    }

    public T GetInstance<T>() where T : Component
    {
        var obj = GetPool<T>().Get();
        if (obj == null) return null;

        if (obj.transform.parent != transform)
        {
            obj.transform.SetParent(transform);
        }

        obj.gameObject.SetActive(true);

        Type type = typeof(T);

        if (poolActiveComponentDic.ContainsKey(type))
        {
            poolActiveComponentDic[type].Add(obj);
        }
        else
        {
            poolActiveComponentDic.Add(type, new() { obj });
        }

        return obj;
    }

    public void ReleaseInstance<T>(T instance) where T : Component
    {
        Type type = typeof(T);

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(transform);

        if (poolActiveComponentDic.ContainsKey(type))
        {
            poolActiveComponentDic[type].Remove(instance);
        }

        var pool = GetPool<T>();
        if (pool != null)
        {
            try
            {
                pool.Release(instance);

            }
            catch (Exception e)
            {
                Debug.LogError(e.Message + ", " + instance.name);
            }
        }
        else
        {
            Destroy(instance.gameObject);
        }
    }

    public void ReleaseAll()
    {
        foreach (var kv in poolActiveComponentDic)
        {
            var type = kv.Key;
            var list = kv.Value;

            if (releaseComponentDic.TryGetValue(type, out var release))
            {
                foreach (var obj in list)
                {
                    release?.Invoke(obj);
                }
            }
            else
            {
                foreach (var obj in list)
                {
                    Destroy(obj.gameObject);
                }
            }

            list.Clear();
        }
    }

    // private void ReleaseToPool(Type type, Component obj)
    // {
    //     if (poolDic.TryGetValue(type, out var poolObj))
    //     {
    //         var pool = poolObj as dynamic;
    //         pool.Release((dynamic)obj);
    //     }
    //     else
    //     {
    //         Destroy(obj.gameObject);
    //     }
    // }

    #endregion Component

    #region  Resource
    private async UniTask LoadLabel(string labelName, int preMakeCount = 10)
    {
        var locationHandler = Addressables.LoadResourceLocationsAsync(labelName);
        await locationHandler.Task;
        IList<IResourceLocation> locations = locationHandler.Result;

        foreach (var location in locations)
        {
            var handler = Addressables.LoadAssetAsync<GameObject>(location);
            var result = await handler.Task;

            uint id = uint.Parse(location.PrimaryKey);

            var resourceObject = result.GetOrAddComponent<ResourceData>();
            resourceObject.ID = id;

            resourcePrefabDic[id] = resourceObject;

            var pool = new ObjectPool<ResourceData>(() => Instantiate(id));

            resourceDic[id] = pool;

            resourcereleaseDic[id] = (comp) => pool.Release(comp);

            for (int i = 0; i < preMakeCount; ++i)
            {
                var obj = pool.Get();
                obj.transform.SetParent(transform);
                obj.gameObject.SetActive(false);

                pool.Release(obj);
            }
        }

        OnLoaded();
    }

    private ResourceData Instantiate(uint id)
    {
        var prefab = GetPrefab(id);
        if (prefab != null)
        {
            var instance = Instantiate(prefab);
            instance.ID = id;
            return instance;
        }
        return null;
    }

    private ResourceData GetPrefab(uint id)
    {
        if (resourcePrefabDic.TryGetValue(id, out var prefab))
            return prefab;

        return null;
    }

    public ResourceData GetInstance(uint id)
    {
        var pool = GetPool(id);
        if (pool == null)
        {
            Debug.LogError($"ResourceManager.GetInstance: pool not found for id={id}");
            return null;
        }

        var obj = pool.Get();
        if (obj == null) return null;

        obj.ID = id;
        obj.gameObject.SetActive(true);

        if (resourceActiveDic.ContainsKey(id))
            resourceActiveDic[id].Add(obj);
        else
            resourceActiveDic.Add(id, new() { obj });

        return obj;
    }

    public ObjectPool<ResourceData> GetPool(uint id)
    {
        if (resourceDic.TryGetValue(id, out var pool))
            return pool;

        return null;
    }

    public void ReleaseInstance(ResourceData instance)
    {
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(transform);

        if (resourceActiveDic.ContainsKey(instance.ID))
        {
            resourceActiveDic[instance.ID].Remove(instance);

            GetPool(instance.ID)?.Release(instance);
        }
        else
        {
            Destroy(instance.gameObject);
        }
    }

    #endregion  GameObject


    protected override void CompleteInitialization()
    {
        LoadResources().Forget();
    }

    public async UniTask LoadResources()
    {
        var taskQueue = new Queue<UniTask>();

        taskQueue.Enqueue(LoadAtlas());

        // ex:
        taskQueue.Enqueue(Load<Hero>());
        taskQueue.Enqueue(Load<Monster>());
        taskQueue.Enqueue(Load<Projectile>());
        taskQueue.Enqueue(LoadLabel("Heros", 1));
        taskQueue.Enqueue(LoadLabel("Projectiles", 3));

        totalQueue = taskQueue.Count;
        completeQueue = 0;

        await UniTask.WhenAll(taskQueue);
    }

    private void OnLoaded()
    {
        ++completeQueue;

        float value = 1f * completeQueue / totalQueue;
        OnInitializeProgressCallback?.Invoke(value);

        if (value == 1)
        {
            base.CompleteInitialization();
        }
    }

    private async UniTask LoadAtlas()
    {
        atlas = await Addressables.LoadAssetAsync<SpriteAtlas>("Atlas");

        OnLoaded();
    }

    public Sprite GetSprite(string imgName)
    {
        return atlas.GetSprite(imgName);
    }

}

