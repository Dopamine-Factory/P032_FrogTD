using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
using Unity.Services.Core;


#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

#if UNITY_IOS
using Apple.GameKit;
#endif

public class UgsAuthProvider : IAuthProvider
{
    private readonly UgsManager ugsManager;
    private string userId;

    private LoginOption LoginOption => AuthManager.Instance.loginOption;

    public string UserId => userId;

    private PlayerInfo playerInfo;

    public bool IsGoogleLinked
    {
        get
        {
#if UNITY_EDITOR
            return false;
#else
            var identities = AuthenticationService.Instance.PlayerInfo?.Identities;
            if (identities == null) return false;
            return identities.Any(id => id.TypeId == "google-play-games");
#endif
        }
    }

    public UgsAuthProvider(UgsManager ugsManager)
    {
        this.ugsManager = ugsManager;
    }

    public async Task<bool> InitializeAndLoginAsync()
    {
        if (ugsManager == null || !ugsManager.IsInitialized)
        {
            Debug.LogError("[UgsAuthProvider] UgsManager not initialized");
            return false;
        }

#if UNITY_EDITOR
        // userId = "UGS_EDITOR_USER_" + AuthManager.Instance.EDITOR_ID;
        userId = "r3cZJwFYCR3tmZnX9zXEhf2MF1fV";
        Debug.Log("[UgsAuthProvider] Editor dummy login");
        return true;
#else
        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                userId = AuthenticationService.Instance.PlayerId;
                Debug.Log($"[UgsAuthProvider] Already signed in: {userId}");
                return true;
            }
            
#if UNITY_ANDROID
            // Silent sign-in: try GPGS without user interaction.
            // Succeeds only if user previously linked their account → restores data after reinstall.
            if (LoginOption.UsePlayGames)
            {
                if (await TrySilentPlayGamesAsync())
                    return true;
            }
            Debug.Log("[UgsAuthProvider] GPGS silent sign-in failed → anonymous fallback");
#endif

#if UNITY_IOS
            if (LoginOption.UseGameCenter)
            {
                bool gcOk = await TryGameCenterWithUgsAsync();
                if (gcOk) return true;
                Debug.LogWarning("[UgsAuthProvider] Game Center + UGS login failed, fallback...");
            }
#endif

            if (LoginOption.UseAnonymous)
            {
                return await TryAnonymous();
            }

            Debug.LogWarning("[UgsAuthProvider] No valid login method succeeded");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UgsAuthProvider] Login error: {e.Message}");
            return false;
        }
#endif
    }

    public void LogOut()
    {
        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(true);
                Debug.Log("[UgsAuthProvider] Signed out successfully");
            }

            userId = string.Empty;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UgsAuthProvider] Logout error: {e.Message}");
        }
    }

    // Links the current anonymous account to Google Play Games for data persistence across reinstalls.
    public async Task<AuthManager.RESULT_CODE> LinkWithGooglePlayGamesAsync()
    {
        if(Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("[UgsAuthProvider] No internet connection, cannot link with Google Play Games");
            return AuthManager.RESULT_CODE.FAILURE;
        }

#if UNITY_ANDROID
        Debug.Log($"[UgsAuthProvider] LinkWithGooglePlayGamesAsync start. IsGoogleLinked={IsGoogleLinked}");
        try
        {
            if (IsGoogleLinked)
            {
                return AuthManager.RESULT_CODE.ALREADY_LOGGINED;
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                // manualAuth=true → ManuallyAuthenticate (shows UI popup for user to select account)
                string authCode = await GetPlayGamesAuthCodeAsync(manualAuth: true);
                if (string.IsNullOrEmpty(authCode))
                {
                    return AuthManager.RESULT_CODE.AUTH_NULL;
                }

                await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(authCode);

                playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();

                return AuthManager.RESULT_CODE.SUCCESS;
            }
            else
            {
                return AuthManager.RESULT_CODE.ALREADY_LINKED;
            }
        }
        catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
        {
            return AuthManager.RESULT_CODE.ALREADY_LINKED;
        }
        catch (AuthenticationException ex)
        {
            // Compare error code to AuthenticationErrorCodes
            // Notify the player with the proper error message
            Debug.LogException(ex);

            return AuthManager.RESULT_CODE.UNKOWN_ERROR;
        }
        catch (RequestFailedException ex)
        {
            // Compare error code to CommonErrorCodes
            // Notify the player with the proper error message
            Debug.LogException(ex);

            return AuthManager.RESULT_CODE.UNKOWN_ERROR;
        }
#else
        Debug.LogWarning("[UgsAuthProvider] LinkWithGooglePlayGamesAsync is Android only");

        return AuthManager.RESULT_CODE.UNKOWN_ERROR;
#endif
    }

    #region Login Methods

    public bool IsAnonymouse()
    {
        var identities = AuthenticationService.Instance.PlayerInfo?.Identities;

        if (identities == null || identities.Count == 0)
        {
            return true;
        }

        return identities.Any(i => i.TypeId == "anonymous");
    }

    private async Task<bool> TryAnonymous()
    {
        if (AuthenticationService.Instance.IsSignedIn)
            return true;

        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            userId = AuthenticationService.Instance.PlayerId;
            playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();
            return true;
        }
        catch (AuthenticationException e)
        {
            if (e.Message.Contains("already signing in"))
            {
                Debug.LogWarning("Already signing in, wait...");

                // 잠깐 기다리고 상태 확인
                await Task.Delay(500);

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    userId = AuthenticationService.Instance.PlayerId;

                    playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();

                    return true;
                }
            }

            Debug.LogError(e);
            return false;
        }
    }

#if UNITY_ANDROID
    // Attempts GPGS login without user interaction.
    // Succeeds only if the user previously authenticated → safe to use on every app start.
    private async Task<bool> TrySilentPlayGamesAsync()
    {
        try
        {
            Debug.Log("[UgsAuthProvider] TrySilentPlayGamesAsync start");

            var tcs = new TaskCompletionSource<SignInStatus>();
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                Debug.Log($"[UgsAuthProvider] Silent GPGS callback: status={status}");
                tcs.SetResult(status);
            });

            SignInStatus status = await tcs.Task;
            if (status != SignInStatus.Success)
            {
                Debug.Log($"[UgsAuthProvider] Silent GPGS failed: {status} → no previous link");
                return false;
            }

            string authCode = await GetPlayGamesAuthCodeAsync();
            if (string.IsNullOrEmpty(authCode)) return false;

            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[UgsAuthProvider] Already signed in before GPGS sign-in");
            }
            else
            {
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode);
            }

            userId = AuthenticationService.Instance.PlayerId;

            playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();

            Debug.Log($"[UgsAuthProvider] Silent GPGS sign-in success. PlayerId={userId}");
            return true;
        }
        catch (AuthenticationException e)
        {
            Debug.LogWarning($"[UgsAuthProvider] Silent GPGS auth failed: {e.ErrorCode} - {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UgsAuthProvider] Silent GPGS unexpected error: {e.Message}");
            return false;
        }
    }


    // Gets GPGS server-side auth code, authenticating first if needed.
    private async Task<string> GetPlayGamesAuthCodeAsync(bool manualAuth = false)
    {
        Debug.Log($"[UgsAuthProvider] GetPlayGamesAuthCodeAsync start. IsAuthenticated={PlayGamesPlatform.Instance.IsAuthenticated()}");

        if (!PlayGamesPlatform.Instance.IsAuthenticated())
        {
            bool manual = manualAuth;
            Debug.Log($"[UgsAuthProvider] GPGS not authenticated, starting {(manual ? "ManuallyAuthenticate" : "Authenticate")}...");

            var tcsAuth = new TaskCompletionSource<SignInStatus>();
            if (manual)
                PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
                {
                    Debug.Log($"[UgsAuthProvider] GPGS Authenticate callback: status={status}");
                    tcsAuth.SetResult(status);
                });
            else
                PlayGamesPlatform.Instance.Authenticate(status =>
                {
                    Debug.Log($"[UgsAuthProvider] GPGS Authenticate callback: status={status}");
                    tcsAuth.SetResult(status);
                });

            SignInStatus status = await tcsAuth.Task;
            if (status != SignInStatus.Success)
            {
                Debug.LogWarning($"[UgsAuthProvider] Play Games auth fail: {status}");
                return null;
            }

            Debug.Log("[UgsAuthProvider] GPGS Authenticate success");
        }

        Debug.Log("[UgsAuthProvider] Requesting server-side access code...");
        var tcsCode = new TaskCompletionSource<string>();
        PlayGamesPlatform.Instance.RequestServerSideAccess(true, code =>
        {
            Debug.Log($"[UgsAuthProvider] RequestServerSideAccess callback: code={(string.IsNullOrEmpty(code) ? "NULL/EMPTY" : "received")}");
            tcsCode.SetResult(code);
        });

        string authCode = await tcsCode.Task;
        if (string.IsNullOrEmpty(authCode))
        {
            Debug.LogError("[UgsAuthProvider] Play Games authCode is null or empty");
            return null;
        }

        Debug.Log("[UgsAuthProvider] authCode acquired successfully");
        return authCode;
    }
#endif

    public async Task<bool> TryPlatformAsync()
    {
#if UNITY_ANDROID

        Debug.Log("[UgsAuthProvider] TryPlatformAsync start");
        try
        {
            string code = await GetPlayGamesAuthCodeAsync();
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("[UgsAuthProvider] TryPlatformAsync: authCode is null, aborting");
                return false;
            }

            Debug.Log("[UgsAuthProvider] Calling SignInWithGooglePlayGamesAsync...");
            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code);

            userId = AuthenticationService.Instance.PlayerId;

            playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();

            Debug.Log($"[UgsAuthProvider] SignInWithGooglePlayGamesAsync success. PlayerId={userId}");
            return true;
        }
        catch (AuthenticationException e)
        {
            Debug.LogError($"[UgsAuthProvider] GPGS + UGS auth failed. ErrorCode={e.ErrorCode} Message={e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UgsAuthProvider] TryPlatformAsync unexpected error: {e}");
            return false;
        }
#endif
    }

    private async Task<bool> TryGameCenterWithUgsAsync() // 나중에 네이티브 ios 써드파티에서 받아서 처리 #구현
    {
#if UNITY_IOS
        try
        {
            Debug.Log("[UgsAuthProvider] TryGameCenterWithUgsAsync start");

            IdentityVerificationSignatureData sigData =
                await GameKitAuth.GenerateIdentityVerificationSignatureAsync();

            if (string.IsNullOrEmpty(sigData.signature) ||
                string.IsNullOrEmpty(sigData.teamPlayerId) ||
                string.IsNullOrEmpty(sigData.publicKeyUrl) ||
                string.IsNullOrEmpty(sigData.salt) ||
                sigData.timestamp == 0)
            {
                Debug.LogError("[UgsAuthProvider] Invalid Game Center identity signature data");
                return false;
            }

            await AuthenticationService.Instance.SignInWithAppleGameCenterAsync(
                sigData.signature,
                sigData.teamPlayerId,
                sigData.publicKeyUrl,
                sigData.salt,
                sigData.timestamp
            );

            userId = AuthenticationService.Instance.PlayerId;
            Debug.Log($"[UgsAuthProvider] Game Center + UGS login success: {userId}");
            return true;
        }
        catch (AuthenticationException e)
        {
            Debug.LogError($"[UgsAuthProvider] Game Center + UGS auth failed: {e.ErrorCode} - {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UgsAuthProvider] Game Center + UGS unexpected error: {e.Message}");
            return false;
        }
#else
        Debug.LogWarning("[UgsAuthProvider] Game Center only supported on iOS");
        return false;
#endif
    }

    #endregion
}