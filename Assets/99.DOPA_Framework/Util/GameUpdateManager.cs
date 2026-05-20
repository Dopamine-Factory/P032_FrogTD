using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class GameUpdateManager : BaseSystemManager<GameUpdateManager>
{
    private bool updateNeeded;
    public bool UpdateNeeded { get => updateNeeded; set => updateNeeded = value; }

    public override void PostInitialize()
    {
#if UNITY_EDITOR

#else
        // CheckUpdate();
#endif
    }

    public bool CheckUpdate()
    {
        Debug.Log("[GameUpdateManager] CheckUpdate start.");

        if (VersionManager.Instance == null || RemoteConfigManager.Instance == null)
        {
            Debug.LogError("[GameUpdateManager] not initialized properly.");
            updateNeeded = false;
            return updateNeeded;
        }

        if (!RemoteConfigManager.Instance.HasValidRemoteConfig)
        {
            Debug.LogWarning("[GameUpdateManager] RemoteConfig is not valid. Skip force update check.");
            updateNeeded = false;
            return updateNeeded;
        }

        bool forceUpdateRequired = RemoteConfigManager.Instance.IsForceUpdate;
        string minAppVersion = RemoteConfigManager.Instance.minAppVersion;
        int minBundleVersion = RemoteConfigManager.Instance.minBundleVersion;
        string LatestVersionString = RemoteConfigManager.Instance.LatestVersionString;

        updateNeeded = IsUpdateRequired(minAppVersion, minBundleVersion, LatestVersionString, forceUpdateRequired);

        if (updateNeeded)
        {
            Debug.Log("[GameUpdateManager] Force update required. Showing update popup.");
            ShowUpdatePopup();
        }
        else
        {
            Debug.Log("[GameUpdateManager] No forced update needed.");
        }

        return updateNeeded;
    }

    private void ShowUpdatePopup()
    {
        Debug.Log($"[GameUpdateManager] ShowUpdatePopup");
        
        PopupManager.Instance.ShowPopup<PopupUpdate>();
    }


    [Header("Store Connect Option")]
    [Tooltip("iOS Appstore ID (Number). Null -> Auto Check")]
    [SerializeField] private string iOSAppId;


    public void OpenStorePage()
    {
#if UNITY_ANDROID
        OpenAndroidStore();
#elif UNITY_IOS
        OpeniOSStore();
#else

#endif
    }

    private void OpenAndroidStore()
    {
        string packageName = VersionManager.Instance.PackageName;
        string playStoreUri = $"market://details?id={packageName}";
        string webUri = $"https://play.google.com/store/apps/details?id={packageName}";
        try
        {
            Application.OpenURL(playStoreUri);
        }
        catch
        {
            Application.OpenURL(webUri);
        }
    }

    private void OpeniOSStore()
    {
        if (!string.IsNullOrEmpty(iOSAppId))
        {
            string appStoreUri = $"itms-apps://itunes.apple.com/app/id{iOSAppId}";
            Application.OpenURL(appStoreUri);
        }
        else
        {
            StartCoroutine(OpenAppStoreByBundleId());
        }
    }

    private IEnumerator OpenAppStoreByBundleId()
    {
        string bundleId = VersionManager.Instance.PackageName;
        string url = $"https://itunes.apple.com/lookup?bundleId={bundleId}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                string trackId = ParseTrackId(json);
                if (!string.IsNullOrEmpty(trackId))
                {
                    string appStoreUri = $"itms-apps://itunes.apple.com/app/id{trackId}";
                    Application.OpenURL(appStoreUri);
                }
                else
                {
                    Debug.LogError("[GameUpdateManager] 앱스토어 ID를 찾을 수 없습니다.");
                }
            }
            else
            {
                Debug.LogError("[GameUpdateManager] 앱스토어 ID 조회 실패: " + www.error);
            }
        }
    }

    private string ParseTrackId(string json)
    {
        string key = "\"trackId\":";
        int idx = json.IndexOf(key);
        if (idx >= 0)
        {
            int start = idx + key.Length;
            int end = json.IndexOf(",", start);
            if (end > start)
            {
                return json.Substring(start, end - start).Trim();
            }
        }
        return null;
    }


    public bool IsUpdateRequired(string minAppVersion, int minBundleVersion, string latestAppVersion, bool forceUpdateRequired)
    {
        Debug.Log($"[GameUpdateManager] IsUpdateRequired forceUpdateRequired : {forceUpdateRequired} , minAppVersion : {minAppVersion} , minBundleVersion : {minBundleVersion} , latestAppVersion : {latestAppVersion}");

        return forceUpdateRequired && VersionManager.Instance.IsBelowMinVersion(minAppVersion, minBundleVersion);

    }

}