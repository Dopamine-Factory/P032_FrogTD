using System;
using System.Collections;
using System.Collections.Generic;
using Facebook.Unity;
using UnityEngine;

public class FacebookManager : BaseSDKManager
{
    /// <summary>
    /// Facebook SDK Init
    /// </summary>
    public override IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        if (!IsEnabled)
        {
            onComplete?.Invoke(true);
            yield break;
        }

        // 진행률 0% 알림
        progressCallback?.Invoke(0f);

        if (!FB.IsInitialized)
        {
            Debug.Log("Facebook IsInitialized === false");

            bool isDone = false;
            bool success = false;

            FB.Init(() =>
            {
                Debug.Log("Facebook Init callback");

                if (FB.IsInitialized)
                {
                    Debug.Log("Facebook Init === IsInitialized === true");
                    FB.ActivateApp();

                    TrackAppInstall();
                    CheckFacebookAttribution();

                    success = true;
                }
                else
                {
                    Debug.LogWarning("Facebook Init === IsInitialized === false");
                    success = false;
                }

                isDone = true;
            });

            // 초기화 완료 대기
            while (!isDone)
            {
                yield return null;
            }

            // 진행률 100%
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(success);
        }
        else
        {
            Debug.Log("Facebook IsInitialized === true");

            FB.ActivateApp();

            TrackAppInstall();
            CheckFacebookAttribution();

            yield return null; // 한프레임 대기

            // 진행률 100%
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(true);
        }
    }

    /// <summary>
    /// Facebook Login
    /// </summary>
    public void Login()
    {
        var permissions = new List<string> { "public_profile", "email" };

        FB.LogInWithReadPermissions(permissions, result =>
        {
            if (FB.IsLoggedIn)
            {
                // Debug.Log("Facebook Login : " + FB.AccessToken.UserId);
                Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook Login === " + AccessToken.CurrentAccessToken.UserId);
            }
            else
            {
                Debug.LogError("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook Login Fail");
            }
        });
    }

    /// <summary>
    /// Facebook Logout
    /// </summary>
    public void Logout()
    {
        FB.LogOut();
        Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook Logout ===");
    }

    /// <summary>
    /// Facebook Tracking App Install
    /// </summary>
    public void TrackAppInstall()
    {
        FB.Mobile.SetAdvertiserTrackingEnabled(true);
        FB.LogAppEvent(AppEventName.ActivatedApp);
        Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook AppInstall Event Send ===");
    }

    /// <summary>
    /// Facebook Tracking Purchase
    /// </summary>
    public void TrackPurchase(float amount, string currency = "USD")
    {
        FB.LogPurchase(amount, currency);
        Debug.Log($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook Purchase Event Sent === Amount : {amount}, Currency : {currency}");
    }

    /// <summary>
    /// Facebook Tracking Level Completed
    /// </summary>
    public void TrackLevelCompleted(int level)
    {
        var parameters = new Dictionary<string, object> { { "level", level } };
        FB.LogAppEvent("LevelCompleted", null, parameters);
        Debug.Log($"$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook Level Completed Event Sent === Level : {level}");
    }

    void CheckFacebookAttribution()
    {
#if !DEBUG
        FB.GetAppLink((result) =>
        {
            if (!string.IsNullOrEmpty(result.Url))
            {
                Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook Install Attribution URL: " + result.Url);
                //if (result.Url.Contains("fbad_id"))
                //{
                //    Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ User installed from a Facebook Ad!");
                //}
            }
            else
            {
                Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ No install attribution data found.");
            }
        });

        FB.Mobile.FetchDeferredAppLinkData((result) =>
        {
            if (!string.IsNullOrEmpty(result.Url))
            {
                Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ Facebook Install Attribution URL: " + result.Url);
                //if (result.Url.Contains("fbad_id"))
                //{
                //    Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ User installed from a Facebook Ad!");
                //}
            }
            else
            {
                Debug.Log("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$ No install attribution data found.");
            }
        });
#endif
    }
}

