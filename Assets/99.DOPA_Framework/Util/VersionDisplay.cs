using UnityEngine;
using TMPro;
using System;

[RequireComponent(typeof(TextMeshProUGUI))]
public class VersionDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI versionTxt;

    void Start()
    {
        versionTxt = versionTxt != null ? versionTxt : GetComponent<TextMeshProUGUI>();
        
        versionTxt.text = "";
        
        SubscribeToVersionReady();
        
        TrySetVersionImmediate();
    }

    private void SubscribeToVersionReady()
    {
        if (VersionManager.Instance != null)
        {
            VersionManager.Instance.OnGameVersionReady += OnVersionReady;
            Debug.Log("[VersionDisplay] Subscribed to VersionManager.OnGameVersionReady");
        }
        else
        {
            StartCoroutine(WaitForVersionManagerSubscription());
        }
    }

    private System.Collections.IEnumerator WaitForVersionManagerSubscription()
    {
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.5f);
            if (VersionManager.Instance != null)
            {
                VersionManager.Instance.OnGameVersionReady += OnVersionReady;
                Debug.Log("[VersionDisplay] Subscribed to VersionManager after wait");
                yield break;
            }
        }
        Debug.LogWarning("[VersionDisplay] Failed to subscribe to VersionManager after timeout");
    }

    private void OnVersionReady(string gameVersion)
    {
        SetVersionText(gameVersion);
        UnsubscribeFromVersionReady();
    }

    private void TrySetVersionImmediate()
    {
        if (VersionManager.Instance != null && !string.IsNullOrEmpty(VersionManager.Instance.GameVersion))
        {
            SetVersionText(VersionManager.Instance.GameVersion);
        }
    }

    private void SetVersionText(string gameVersion)
    {
        if (versionTxt != null)
        {
            versionTxt.text = gameVersion;
            Debug.Log($"[VersionDisplay] Version set via callback/immediate: {gameVersion}");
        }
    }

    private void UnsubscribeFromVersionReady()
    {
        if (VersionManager.Instance != null)
        {
            VersionManager.Instance.OnGameVersionReady -= OnVersionReady;
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromVersionReady();
    }
}