using System.IO;
using UnityEngine;

[CreateAssetMenu(fileName = "AppleUnityPluginsConfig", menuName = "SdkConfig/AppleUnityPlugins")]
public class AppleUnityPluginsConfig : SdkConfig
{
    public override string DisplayName
    {
        get
        {
            return string.IsNullOrEmpty(base.DisplayName) ? "Apple Unity Plugins" : base.DisplayName;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        if (AssetPaths.Count == 0)
        {
            string corePath = "Packages/com.apple.unityplugin.core";
            string gameKitPath = "Packages/com.apple.unityplugin.gamekit";
            string coreHapticsPath = "Packages/com.apple.unityplugin.corehaptics";

            TryAddPath(corePath);
            TryAddPath(gameKitPath);
            TryAddPath(coreHapticsPath);
        }
    }

    private void TryAddPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (AssetPaths.Contains(path))
        {
            return;
        }

        AssetPaths.Add(path);
    }
}