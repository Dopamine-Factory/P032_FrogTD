
using System;
using System.Collections.Generic;

public static class ManagerRegistry
{
    private static Dictionary<string, IBaseManager> allManagers = new();

    public static void RegisterManager(string id, IBaseManager manager)
    {
        if (!allManagers.ContainsKey(id))
        {
            allManagers[id] = manager;
        }
    }

    public static IBaseManager GetManager(string id)
    {
        allManagers.TryGetValue(id, out var mgr);
        return mgr;
    }

    public static IEnumerable<IBaseManager> AllManagers => allManagers.Values;
}
public interface IBaseManager
{
    string Id { get; }
    bool IsFullyInitialized { get; }
    bool IsEnabled { get; }

    event Action<string> OnFullyInitialized;
}

public enum MainProviderType
{
    None = 0,
    Firebase = 1,
    Ugs = 2
}