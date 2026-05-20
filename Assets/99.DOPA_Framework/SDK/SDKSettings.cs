using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SDKSettings", menuName = "SDK/Settings")]
public class SDKSettings : ScriptableObject
{
    [SerializeField] private List<SdkConfig> sdkConfigs = new();

    private Dictionary<string, SdkConfig> configByTypeNameCache;

    public bool TryGetConfigByTypeName(string configTypeName, out SdkConfig config)
    {
        EnsureCache();
        return configByTypeNameCache.TryGetValue(configTypeName, out config);
    }

    private void EnsureCache()
    {
        if (configByTypeNameCache != null)
        {
            return;
        }

        configByTypeNameCache = new Dictionary<string, SdkConfig>();

        for (int i = 0; i < sdkConfigs.Count; i++)
        {
            SdkConfig config = sdkConfigs[i];
            if (config == null)
            {
                continue;
            }

            string typeName = config.GetType().Name;
            if (configByTypeNameCache.ContainsKey(typeName))
            {
                continue;
            }

            configByTypeNameCache.Add(typeName, config);
        }
    }
}