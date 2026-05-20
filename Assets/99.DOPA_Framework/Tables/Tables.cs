using System.Collections.Generic;
using System.Threading.Tasks;
using System;
public static class Tables
{
    private static List<IDataManager> _managers = new List<IDataManager>();
    private static readonly Dictionary<Type, IDataManager> _managerLookup = new();
    static Tables()
    {
        Register(new HeroTable());
        Register(new MonsterTable());
    }
    private static void Register(IDataManager manager)
    {
        _managers.Add(manager);
        _managerLookup[manager.GetType()] = manager;
    }
    public static async Task LoadAllAsync(System.Action<float> onProgress)
    {
        int totalManagers        = _managers.Count;
        int completedManagers    = 0;

        foreach (var manager in _managers)
        {
            await manager.LoadDataAsync(progress =>
            {
                float totalProgress = (completedManagers + progress) / totalManagers;
                onProgress?.Invoke(totalProgress);
            });

            completedManagers++;
        }
    }
    public static T GetTable<T>() where T : class, IDataManager
    {
        if (_managerLookup.TryGetValue(typeof(T), out var manager))
        {
            return manager as T;
        }
    
        throw new InvalidOperationException($"Fail : {typeof(T).Name}");
    }

    public static bool GetTable<T>(out T table) where T : class, IDataManager
    {
        if (_managerLookup.TryGetValue(typeof(T), out var manager))
        {
            table = manager as T;
    
            return true;
        }
    
        table = null;
    
        return false;
    }
}
