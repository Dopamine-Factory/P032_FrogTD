using System;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Auth;
using Firebase.Extensions;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

#if UNITY_IOS
using Apple.GameKit;
#endif

public class FirebaseAuthProvider : IAuthProvider
{
    private readonly FirebaseManager firebaseManager;
    private FirebaseAuth auth;
    private LoginOption LoginOption => AuthManager.Instance.loginOption;

    public string UserId { get; private set; }

    public FirebaseAuthProvider(FirebaseManager firebaseManager)
    {
        this.firebaseManager = firebaseManager;
        auth = FirebaseAuth.DefaultInstance;
    }

    public async Task<bool> InitializeAndLoginAsync()
    {
        if (firebaseManager == null || !firebaseManager.IsInitialized || auth == null)
        {
            Debug.LogError("[FirebaseAuthProvider] FirebaseManager not initialized");
            return false;
        }

#if UNITY_EDITOR
        UserId = "EDITOR_USER_123";
        Debug.Log("[FirebaseAuthProvider] Editor dummy login");
        return true;
#else
        return await PlatformLoginFlowAsync();
#endif
    }

    public void LogOut()
    {
        if (auth == null) return;
        auth.SignOut();
        UserId = string.Empty;
    }

    private async Task<bool> PlatformLoginFlowAsync()
    {
#if UNITY_ANDROID
        if (LoginOption.UsePlayGames)
        {
            return await PlayGamesLoginAsync();
        }
        else if (LoginOption.UseAnonymous)
        {
            return await SignInAnonymouslyAsync();
        }
        else
        {
            Debug.LogWarning("[FirebaseAuthProvider] No login method selected (Android)");
            return false;
        }

#elif UNITY_IOS
        if (LoginOption.UseGameCenter)
        {
            return await GameCenterLoginAsync();
        }
        else if (LoginOption.UseAnonymous)
        {
            return await SignInAnonymouslyAsync();
        }
        else
        {
            Debug.LogWarning("[FirebaseAuthProvider] No login method selected (iOS)");
            return false;
        }

#else
        if (LoginOption.UseAnonymous)
        {
            return await SignInAnonymouslyAsync();
        }
        else
        {
            Debug.LogWarning("[FirebaseAuthProvider] No login method selected (Editor/Other)");
            return false;
        }
#endif
    }

#if UNITY_ANDROID
    private async Task<bool> PlayGamesLoginAsync()
    {
        try
        {
            if (!PlayGamesPlatform.Instance.IsAuthenticated())
            {
                PlayGamesPlatform.DebugLogEnabled = true;
                PlayGamesPlatform.Activate();

                var tcsAuth = new TaskCompletionSource<SignInStatus>();
                PlayGamesPlatform.Instance.Authenticate(status =>
                {
                    tcsAuth.SetResult(status);
                });

                SignInStatus status = await tcsAuth.Task;
                if (status != SignInStatus.Success)
                {
                    Debug.LogWarning($"[FirebaseAuthProvider] PlayGames auth failed: {status}");
                    return await SignInAnonymouslyAsync();
                }
            }

            var tcsCode = new TaskCompletionSource<string>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(
                false,
                authCode =>
                {
                    tcsCode.SetResult(authCode);
                });

            string code = await tcsCode.Task;
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("[FirebaseAuthProvider] PlayGames authCode is empty");
                return await SignInAnonymouslyAsync();
            }

            // PlayGamesAuthProvider 직접 사용 (있으면)
            var credential = PlayGamesAuthProvider.GetCredential(code);
            var result = await auth.SignInWithCredentialAsync(credential);

            UserId = result.UserId;
            Debug.Log($"[FirebaseAuthProvider] PlayGames+Firebase login success: {UserId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FirebaseAuthProvider] PlayGamesLogin error: {ex.Message}");
            return await SignInAnonymouslyAsync();
        }
    }
#endif


#if UNITY_IOS
    private async Task<bool> GameCenterLoginAsync()
    {
        try
        {
            if (!GKLocalPlayer.Local.IsAuthenticated)
            {
                GKLocalPlayer player = await GKLocalPlayer.Authenticate();
                if (player == null || !player.IsAuthenticated)
                {
                    Debug.LogWarning("[FirebaseAuthProvider] GameCenter auth failed");
                    return await SignInAnonymouslyAsync();
                }
            }

            var verificationItems = await GKLocalPlayer.Local.FetchItemsForIdentityVerificationSignature();
            if (verificationItems == null)
            {
                Debug.LogWarning("[FirebaseAuthProvider] FetchItemsForIdentityVerificationSignature failed");
                return await SignInAnonymouslyAsync();
            }

            byte[] signatureBytes = verificationItems.GetSignature();
            string serverAuthCode = Convert.ToBase64String(signatureBytes);

            if (string.IsNullOrEmpty(serverAuthCode))
            {
                Debug.LogWarning("[FirebaseAuthProvider] serverAuthCode is empty");
                return await SignInAnonymouslyAsync();
            }

            var credential = OAuthProvider.GetCredential("apple.com", serverAuthCode, null, null);
            var result = await auth.SignInWithCredentialAsync(credential);

            UserId = result.UserId;
            Debug.Log($"[FirebaseAuthProvider] GameCenter+Firebase login success: {UserId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FirebaseAuthProvider] GameCenterLogin error: {ex.Message}");
            return await SignInAnonymouslyAsync();
        }
    }
#endif

    private async Task<bool> SignInAnonymouslyAsync()
    {
        try
        {
            if (auth.CurrentUser != null)
            {
                UserId = auth.CurrentUser.UserId;
                Debug.Log($"[FirebaseAuthProvider] Already signed in: {UserId}");
                return true;
            }

            var result = await auth.SignInAnonymouslyAsync();
            UserId = result.User.UserId;
            Debug.Log($"[FirebaseAuthProvider] Anonymous login success: {UserId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FirebaseAuthProvider] Anonymous login failed: {ex.Message}");
            return false;
        }
    }

    public Task<bool> TryPlatformAsync()
    {
        throw new NotImplementedException();
    }

    public bool IsAnonymouse()
    {
        throw new NotImplementedException();
    }
}
