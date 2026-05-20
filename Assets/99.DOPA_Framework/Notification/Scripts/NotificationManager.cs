using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
using UnityEngine.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

public class NotificationManager : BaseSystemManager<NotificationManager>
{
    [Header("Android Settings")]
    [SerializeField] private string androidChannelId = "daily_notification";

    [Header("iOS Settings")]
    [SerializeField] private string iosCategoryId = "DAILY_REMINDER";

    private const string k_FirstInstallKey = "FirstInstallProcessed";
    private const string k_PermissionTimeKey = "NotificationPermissionRequestTime";
    private readonly TimeSpan k_PermissionCooldown = TimeSpan.FromDays(7);

    public override void Initialize()
    {
        base.Initialize();

        StartCoroutine(InitializeNotificationRoutine());
        ProcessAppAccess();
        ScheduleNextDayNotification(20, 59);
    }

    private IEnumerator InitializeNotificationRoutine()
    {
#if UNITY_ANDROID
        var channel = new AndroidNotificationChannel()
        {
            Id = androidChannelId,
            Name = "Daily Reminder",
            Importance = Importance.High,
            Description = "Daily game notification"
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);

        if (CheckAndroidSdkVersion(33))
        {
            bool hasPermission = Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS");
            if (hasPermission == false && CanRequestPermission())
            {
                Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                RecordPermissionRequestTime();
            }
        }
#elif UNITY_IOS
            var authorizationOption = AuthorizationOption.Alert |
                                      AuthorizationOption.Sound |
                                      AuthorizationOption.Badge;

            using (var req = new AuthorizationRequest(authorizationOption, true))
            {
                while (req.IsFinished == false)
                {
                    yield return null;
                }

                string response = $"finished=true, granted={req.Granted}, error={req.Error}";
                Debug.Log($"[Notification] iOS Permission: {response}");
            }
#endif
        yield return null;
    }

    private void ProcessAppAccess()
    {
        bool hasProcessedFirstInstall = PlayerPrefs.GetInt(k_FirstInstallKey, 0) == 1;

        if (hasProcessedFirstInstall)
        {
            // Intended design: Clear all previously scheduled notifications on fresh launch
            CancelAllNotifications();
        }
        else
        {
            SendAccessNotification();
            PlayerPrefs.SetInt(k_FirstInstallKey, 1);
            PlayerPrefs.Save();
        }
    }

    private void SendAccessNotification()
    {
        DateTime fireTime = DateTime.Now.AddMinutes(1);
        SendNotification("welcome_back_id", "Welcome back!", "Thanks for logging in!", fireTime);
    }

    public void SendDynamicAccessNotification()
    {
        bool isPeak = IsPeakTime();
        int delayMinutes = isPeak ? 3 : 1;
        DateTime fireTime = DateTime.Now.AddMinutes(delayMinutes);

        SendNotification("dynamic_event_id", "Event Open!", $"{delayMinutes} Exclusive Item Open!", fireTime);
    }

    private bool IsPeakTime()
    {
        int currentHour = DateTime.Now.Hour;
        return currentHour >= 18 && currentHour <= 23;
    }

    public void ScheduleNextDayNotification(int hour, int minute)
    {
        DateTime fireTime = DateTime.Today.AddDays(1).AddHours(hour).AddMinutes(minute);
        SendNotification("next_day_login_id", "A new adventure awaits!", "Don't miss out—log in now!", fireTime);
    }

    public void SendNotification(string notificationId, string title, string body, DateTime fireTime)
    {
        TimeSpan delay = fireTime - DateTime.Now;

        // Defensive code: Prevent scheduling past notifications (Fixes iOS crash)
        if (delay.TotalSeconds <= 0)
        {
            Debug.LogWarning($"[Notification] Fire time is in the past for ID: {notificationId}. Skipping.");
            return;
        }

        // Always cancel existing scheduled notification with the same ID before creating a new one
        CancelNotification(notificationId);

#if UNITY_ANDROID
        var notification = new AndroidNotification()
        {
            Title = title,
            Text = body,
            FireTime = fireTime,
            SmallIcon = "icon_a_s",
            LargeIcon = "icon_a_l"
        };

        int generatedId = AndroidNotificationCenter.SendNotification(notification, androidChannelId);

        // Map the custom ID to Android's internal integer ID
        PlayerPrefs.SetInt(notificationId, generatedId);
        PlayerPrefs.Save();
#elif UNITY_IOS
            var timeTrigger = new iOSNotificationTimeIntervalTrigger()
            {
                TimeInterval = delay,
                Repeats = false
            };

            var notification = new iOSNotification()
            {
                Identifier = notificationId, // Explicitly use our unique ID instead of PlayerPrefs
                Title = title,
                Body = body,
                Trigger = timeTrigger,
                CategoryIdentifier = iosCategoryId
            };

            iOSNotificationCenter.ScheduleNotification(notification);
#endif
    }

    public void CancelNotification(string notificationId)
    {
#if UNITY_ANDROID
        if (PlayerPrefs.HasKey(notificationId))
        {
            int platformId = PlayerPrefs.GetInt(notificationId);
            AndroidNotificationCenter.CancelNotification(platformId);
            PlayerPrefs.DeleteKey(notificationId);
        }
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(notificationId);
#endif
    }

    public void CancelAllNotifications()
    {
#if UNITY_ANDROID
        AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
    }

#if UNITY_ANDROID
    private bool CheckAndroidSdkVersion(int targetVersion)
    {
#if !UNITY_EDITOR
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                int sdkInt = version.GetStatic<int>("SDK_INT");
                return sdkInt >= targetVersion;
            }
#else
        return false;
#endif
    }
#endif

    private bool CanRequestPermission()
    {
        if (PlayerPrefs.HasKey(k_PermissionTimeKey) == false)
        {
            return true;
        }

        long lastRequestTicks = long.Parse(PlayerPrefs.GetString(k_PermissionTimeKey));
        DateTime lastRequestTime = new DateTime(lastRequestTicks);

        return (DateTime.Now - lastRequestTime) > k_PermissionCooldown;
    }

    private void RecordPermissionRequestTime()
    {
        PlayerPrefs.SetString(k_PermissionTimeKey, DateTime.Now.Ticks.ToString());
        PlayerPrefs.Save();
    }
}