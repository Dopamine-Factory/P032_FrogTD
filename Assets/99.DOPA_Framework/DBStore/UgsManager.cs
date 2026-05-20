using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication; // 추가!

public class UgsManager : BaseSDKManager
{
    public bool IsInitialized { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public override IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[UgsManager] Initialize start");
        if (!IsEnabled)
        {
            onComplete?.Invoke(true);
            yield break;
        }

        bool done = false;
        bool success = false;
        progressCallback?.Invoke(1f);

        InitializeUgsAsync().ContinueWith(t =>
        {
            success = !t.IsFaulted && !t.IsCanceled && t.Result;
            done = true;
        });

        while (!done) yield return null;

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(success);
    }

    private async Task<bool> InitializeUgsAsync()
    {
        try
        {
            // 1. Core Services 초기화 (30% 진행)
            await UnityServices.InitializeAsync();
            Debug.Log("[UgsManager] UnityServices.InitializeAsync success");

            // 2. Authentication 필수! (70% 진행)
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                IsAuthenticated = true;
                Debug.Log($"[UgsManager] Auth success. PlayerId: {AuthenticationService.Instance.PlayerId}");
            }

            // 3. 완전 초기화
            IsInitialized = true;
            Debug.Log("[UgsManager] UGS fully initialized");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UgsManager] Initialize failed: {e}");
            IsInitialized = false;
            return false;
        }
    }
}
