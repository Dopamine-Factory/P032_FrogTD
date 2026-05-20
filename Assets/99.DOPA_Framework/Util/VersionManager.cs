using System.Text;
using System.IO;
using System;
using UnityEngine;
using System.Security.Cryptography;
using UnityEditor;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif

public class VersionManager : BaseSystemManager<VersionManager>
{
    public event System.Action<string> OnGameVersionReady;
    public string PackageName { get; private set; }
    public string GameVersion { get; private set; }
    public string VersionString { get; private set; }
    public string BundleVersionCode { get; private set; }
    public int BundleVersionInt { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        InitializeVersionData();
    }

    private void InitializeVersionData()
    {
        Debug.Log("[VersionManager] InitializeVersionData start.");

        PackageName = Application.identifier;
        VersionString = Application.version;
        BundleVersionCode = GetBundleVersionCode();
        BundleVersionInt = ParseBundleVersion(BundleVersionCode);


        if (Debug.isDebugBuild)
        {
            GameVersion = $"v{VersionString}({BundleVersionCode})";
        }
        else
        {
            string hash = Encrypt($"{VersionString}_{BundleVersionCode}").Substring(0, 8);
            GameVersion = $"v{VersionString}_{hash}";
        }

        OnGameVersionReady?.Invoke(GameVersion);
        
        Debug.Log($"~#~#~#~#~#~#~#~#~#~#~#~# [VersionManager] InitializeVersionData Initialized Version: {GameVersion}");
    }



    private bool IsNewerVersion(string versionA, string versionB)
    {
        Debug.Log($"[VersionManager] IsNewerVersion versionA : {versionA} , versionB : {versionB}");

        return CompareVersions(versionA, versionB) > 0;
    }

    private int CompareVersions(string versionA, string versionB)
    {
        Debug.Log($"[VersionManager] CompareVersions Start versionA == {versionA} , versionB == {versionB}");

        if (string.IsNullOrEmpty(versionA)) versionA = "0";
        if (string.IsNullOrEmpty(versionB)) versionB = "0";

        var partsA = versionA.Split('.');
        var partsB = versionB.Split('.');
        int length = Mathf.Max(partsA.Length, partsB.Length);

        Debug.Log($"[VersionManager] CompareVersions partsA : {partsA} , partsB : {partsB}");


        for (int i = 0; i < length; i++)
        {
            int a = i < partsA.Length ? int.Parse(partsA[i]) : 0;
            int b = i < partsB.Length ? int.Parse(partsB[i]) : 0;

            if (a != b)
                return a.CompareTo(b);
        }
        return 0;
    }

    private int ParseBundleVersion(string bundleVersion)
    {
        if (int.TryParse(bundleVersion, out int result))
            return result;

        Debug.LogWarning($"[VersionManager] ParseBundleVersion parse fail : {bundleVersion}");
        return 0;
    }
    public bool IsBelowMinVersion(string minAppVersion, int minBundleVersion)
    {
        Debug.Log($"[VersionManager] IsBelowMinVersion minAppVersion : {minAppVersion} , minBundleVersion : {minBundleVersion}");

        int versionComparison = CompareVersions(VersionString, minAppVersion);
        if (versionComparison < 0) return true; // 현재 버전 < 최소 버전
        if (versionComparison > 0) return false; // 현재 버전 > 최소 버전

        return BundleVersionInt < minBundleVersion;
    }

    private string GetBundleVersionCode()
    {
#if UNITY_EDITOR
#if UNITY_ANDROID
        return PlayerSettings.Android.bundleVersionCode.ToString();
#elif UNITY_IOS
        return PlayerSettings.iOS.buildNumber;
#else
        return "N/A";
#endif
#elif UNITY_ANDROID
        return GetAndroidBundleVersionCode();
#elif UNITY_IOS
        return GetiOSBuildNumber();
#else
        return "N/A";
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetiOSBuildNumberNative();
#endif

    private string GetiOSBuildNumber()
    {
#if UNITY_IOS && !UNITY_EDITOR
    return GetiOSBuildNumberNative();
#else
        return "N/A";
#endif
    }

    private string GetAndroidBundleVersionCode()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
            using (var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0))
            {
                return packageInfo.Get<int>("versionCode").ToString();
            }
        }
        catch
        {
            return "N/A";
        }
    }

    private string Encrypt(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(
                (VersionString + VersionString).PadRight(32).Substring(0, 32)
            );
            aes.IV = new byte[16];

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
                cs.Write(inputBytes, 0, inputBytes.Length);
                cs.FlushFinalBlock();
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RequestTrackingAuthorization();
#endif

    public void RequestUserTrackingPermission()
    {
#if UNITY_IOS && !UNITY_EDITOR
        RequestTrackingAuthorization();
#endif
    }

    public void OpenStorePage()
    {
        GameUpdateManager.Instance.OpenStorePage();
    }
}