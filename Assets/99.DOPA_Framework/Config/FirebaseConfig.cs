using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "FirebaseConfig", menuName = "SdkConfig/Firebase")]
public class FirebaseConfig : SdkConfig
{
#if UNITY_EDITOR
    public override void Initialize()
    {
        if (string.IsNullOrEmpty(DisplayName))
        {
            displayName = "Firebase";
        }

        base.Initialize();
    }

    [ContextMenu("Auto Assign Default Firebase Files")]
    private void AutoAssignDefaultFiles()
    {
        TryAddPathIfExists("Assets/StreamingAssets/google-services.json");
        TryAddPathIfExists("Assets/StreamingAssets/google-services-desktop.json");

        EditorUtility.SetDirty(this);
    }

    private void TryAddPathIfExists(string path)
    {
        if (System.IO.File.Exists(path))
        {
            AddAssetPath(path);
        }
        else
        {
            Debug.Log($"[FirebaseConfig] File not found for auto assign: {path}");
        }
    }

    public virtual void OnEnableSdk()
    {
        Debug.Log("[FirebaseConfig] Firebase SDK enabled");
    }

    public virtual void OnDisableSdk()
    {
        Debug.Log("[FirebaseConfig] Firebase SDK disabled");
    }
#endif
}