using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public abstract class SdkConfig : ScriptableObject
{
    [SerializeField] private bool isEnabled = true;
    [SerializeField] protected string displayName = "New SdkConfig";

    [SerializeField] private List<string> assetPaths = new List<string>();

    public bool IsEnabled => isEnabled;
    public virtual string DisplayName => displayName;

    public List<string> AssetPaths => assetPaths;


    public virtual void Initialize()
    {
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = name;
        }

        if (assetPaths == null)
        {
            assetPaths = new List<string>();
        }
    }


    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
    }


    public void AddAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (assetPaths == null)
        {
            assetPaths = new List<string>();
        }

        if (!assetPaths.Contains(path))
        {
            assetPaths.Add(path);
        }
    }


    public void RemoveAssetAt(int index)
    {
        if (assetPaths == null)
        {
            return;
        }

        if (index < 0 || index >= assetPaths.Count)
        {
            return;
        }

        assetPaths.RemoveAt(index);
    }


    public static string ToDisabledPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (path.Contains("-disabled"))
        {
            return path;
        }

        int lastSlash = path.LastIndexOf('/');
        string dir;
        string fileName;

        if (lastSlash < 0)
        {
            dir = string.Empty;
            fileName = path;
        }
        else
        {
            dir = path.Substring(0, lastSlash + 1);
            fileName = path.Substring(lastSlash + 1);
        }

        int dotIndex = fileName.LastIndexOf('.');
        if (dotIndex < 0)
        {
            // no extension
            return dir + fileName + "-disabled";
        }

        string nameWithoutExt = fileName.Substring(0, dotIndex);
        string ext = fileName.Substring(dotIndex); // includes '.'

        return dir + nameWithoutExt + "-disabled" + ext;
    }



    public void ApplyToggle()
    {
#if UNITY_EDITOR
        if (assetPaths == null || assetPaths.Count == 0)
        {
            return;
        }

        foreach (string storedPath in assetPaths)
        {
            TogglePath(storedPath, isEnabled);
        }

        UnityEditor.AssetDatabase.Refresh();
#endif
    }


    private void TogglePath(string storedPath, bool enable)
    {
        if (string.IsNullOrEmpty(storedPath))
        {
            return;
        }

        string projectRoot = Directory.GetCurrentDirectory();

        string activeRelative = storedPath.Replace("\\", "/");
        string disabledRelative = ToDisabledPath(activeRelative);

        string activeFull = Path.Combine(projectRoot, activeRelative);
        string disabledFull = Path.Combine(projectRoot, disabledRelative);

        try
        {
            if (enable)
            {
                if (Directory.Exists(disabledFull) && !Directory.Exists(activeFull))
                {
                    Directory.Move(disabledFull, activeFull);
                    Debug.Log($"[SdkConfig] Enabled folder: {activeRelative}");
                }
                else if (File.Exists(disabledFull) && !File.Exists(activeFull))
                {
                    File.Move(disabledFull, activeFull);
                    Debug.Log($"[SdkConfig] Enabled file: {activeRelative}");
                }
            }
            else
            {
                if (Directory.Exists(activeFull) && !Directory.Exists(disabledFull))
                {
                    Directory.Move(activeFull, disabledFull);
                    Debug.Log($"[SdkConfig] Disabled folder: {activeRelative}");
                }
                else if (File.Exists(activeFull) && !File.Exists(disabledFull))
                {
                    File.Move(activeFull, disabledFull);
                    Debug.Log($"[SdkConfig] Disabled file: {activeRelative}");
                }
            }
        }
        catch (IOException e)
        {
            Debug.LogError($"[SdkConfig] Failed to toggle path {storedPath}: {e}");
        }
    }
}