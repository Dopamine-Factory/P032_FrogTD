using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AuthManager : BaseSystemManager<AuthManager>
{
    [Header("Main Provider 설정")]
    [SerializeField] private MainProviderType mainProviderType = MainProviderType.Firebase;

    [Header("로그인 방식 선택")]
    public LoginOption loginOption = new LoginOption();

    [Header("SDK Managers")]
    [SerializeField] private FirebaseManager firebaseManager;
    [SerializeField] private UgsManager ugsManager;

    public bool IsLoggedIn { get; private set; }

    public string UserId { get; private set; }

    [Header("에디터에서 사용할 유저 아이디")]
    public string EDITOR_ID = "123";

    private IAuthProvider authProvider;

    public enum RESULT_CODE
    {
        NONE,
        SUCCESS,
        FAILURE,
        ALREADY_LOGGINED,
        ALREADY_LINKED,

        AUTH_NULL,
        UNKOWN_ERROR,

        PROVIDER_EMPTY
    }

    CancellationToken cancellationToken;

    public override void Initialize()
    {
        cancellationToken = this.GetCancellationTokenOnDestroy();

        base.Initialize();
    }

    protected override void CompleteInitialization()
    {
        StartCoroutine(SetupActiveStore());
    }

    private IEnumerator SetupActiveStore()
    {
        if (!IsEnabled)
        {
            yield return null;
        }

        var result = SiginIn();
        while (!result.IsCompleted)
        {
            yield return null;
        }

        PostInitializeWrapper();
    }

    private void SetProvider()
    {
        switch (mainProviderType)
        {
            case MainProviderType.Firebase:
                if (firebaseManager == null || !firebaseManager.IsEnabled || !firebaseManager.IsInitialized)
                {
                    authProvider = null;
                }

                break;

            case MainProviderType.Ugs:
                if (ugsManager == null || !ugsManager.IsEnabled || !ugsManager.IsInitialized)
                {
                    authProvider = null;
                }

                if (authProvider as UgsAuthProvider == null)
                    authProvider = new UgsAuthProvider(ugsManager);
                break;

            default:
                authProvider = null;
                break;
        }
    }

    public bool IsAnonymouse()
    {
        return authProvider == null || authProvider.IsAnonymouse();
    }

    public async Task<bool> SiginIn()
    {
        IsLoggedIn = false;
        UserId = string.Empty;

        SetProvider();

        if (authProvider == null)
        {
            return false;
        }

        bool result = await authProvider.InitializeAndLoginAsync();


        Debug.Log($"!!! [AuthManager] SiginIn result: {result}, UserId: {authProvider.UserId}");
        if (result)
        {
            IsLoggedIn = true;

            UserId = authProvider.UserId;

            return true;
        }
        else
        {
            return false;
        }
    }

    public async Task TryLinkWithPlatform()
    {
        var result = RESULT_CODE.NONE;

        SetProvider();

        if (authProvider == null)
        {
            result = RESULT_CODE.PROVIDER_EMPTY;
        }

        if (authProvider as UgsAuthProvider != null)
        {
            result = await (authProvider as UgsAuthProvider).LinkWithGooglePlayGamesAsync();
        }

        Debug.Log("[AuthManager] TryLinkWithPlatform " + result);

        if (result == RESULT_CODE.SUCCESS)
        {
            await LocalSaveManager.Instance.SaveAll(true, false);
        }
        else if (result == RESULT_CODE.ALREADY_LINKED)
        {
            // PopupManager.Instance.ShowPopup<PopupAlert>("Progressing", "Login Progressing...", (popup) =>
            // {
            //     popup.SetVisibleAllBtns(false);
            // });

            authProvider.LogOut();

            // 🔥 핵심: 상태 안정화 대기
            await UniTask.Delay(500, cancellationToken: cancellationToken);

            if (authProvider as UgsAuthProvider != null)
            {
                if (await (authProvider as UgsAuthProvider).TryPlatformAsync())
                {
                    await DataChange();
                }
            }
        }
        else
        {
            // PopupManager.Instance.ShowPopup<PopupAlert>("Error", "Login Failed", (popup) =>
            // {
            //     popup.SetButtons(null, null, "Confirm", popup.Close);
            // });
        }
    }

    private async UniTask DataChange()
    {
        bool isSuccess = false;

        StartCoroutine(DbStoreManager.Instance.InitializeFlow((isDone) =>
        {
            isSuccess = true;
        }));

        await UniTask.WaitUntil(() => isSuccess, cancellationToken: cancellationToken);
 
        //기존 데이터 Pending 저장.
        await LocalSaveManager.Instance.SaveAll(false, true);

        await LocalSaveManager.Instance.LoadAll(true);
    }


    public async Task<RESULT_CODE> SiginInWithPlatform()
    {
#if UNITY_ANDROID
        SetProvider();

        if (authProvider == null)
        {
            return RESULT_CODE.PROVIDER_EMPTY;
        }

        if (authProvider as UgsAuthProvider != null)
        {
            return await (authProvider as UgsAuthProvider).LinkWithGooglePlayGamesAsync();
        }
#elif UNITY_IOS

#endif
        return RESULT_CODE.UNKOWN_ERROR;
    }


    public MainProviderType GetCurrentProviderType()
    {
        return mainProviderType;
    }

    public void LogOut()
    {
        if (authProvider == null)
        {
            return;
        }

        authProvider.LogOut();
        IsLoggedIn = false;
        UserId = string.Empty;
    }
}

public interface IAuthProvider
{
    string UserId { get; }

    Task<bool> InitializeAndLoginAsync();
    Task<bool> TryPlatformAsync();
    bool IsAnonymouse();
    void LogOut();
}

[Serializable]
public class LoginOption
{
    public bool UsePlayGames = true;
    public bool UseGameCenter = true;
    public bool UseAnonymous = true;
}
