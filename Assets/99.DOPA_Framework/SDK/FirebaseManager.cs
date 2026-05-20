using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;

public class FirebaseManager : BaseSDKManager
{
    public bool IsInitialized { get; private set; }
    public event Action OnInitialized;

    private readonly List<Parameter> cachedParameterList = new List<Parameter>(10);

    public override IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete)
    {
        Debug.Log("[FirebaseManager] Initialize Start");

        if (!IsEnabled)
        {
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(true);
            yield break;
        }

        bool isSuccess = false;
        progressCallback?.Invoke(0f);

#if UNITY_EDITOR
        // Skip actual Firebase init in Editor to prevent editor lockups
        isSuccess = true;
        IsInitialized = true;
        progressCallback?.Invoke(1f);
        onComplete?.Invoke(true);
        yield break;
#else
        var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();

        // 1. Wait safely for the Task to complete without callback hell or deadlocks
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        progressCallback?.Invoke(0.5f);

        // 2. Exception and Cancellation handling
        if (dependencyTask.IsFaulted)
        {
            Debug.LogError($"[FirebaseManager] Dependency check faulted: {dependencyTask.Exception}");
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(false);
            yield break;
        }
        else if (dependencyTask.IsCanceled)
        {
            Debug.LogWarning("[FirebaseManager] Dependency check was canceled.");
            progressCallback?.Invoke(1f);
            onComplete?.Invoke(false);
            yield break;
        }

        var dependencyStatus = dependencyTask.Result;

        if (dependencyStatus == DependencyStatus.Available)
        {
            try
            {
                IsInitialized = true;
                isSuccess = true;
                Debug.Log("[FirebaseManager] Firebase Initialize Success.");
                OnInitialized?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[FirebaseManager] Post-initialization error: {e.Message}");
                isSuccess = false;
            }
        }
        else
        {
            Debug.LogError($"[FirebaseManager] Could not resolve all Firebase dependencies: {dependencyStatus}");
            isSuccess = false;
        }

        progressCallback?.Invoke(1f);
        onComplete?.Invoke(isSuccess);
#endif
    }

    /// <summary>
    /// Log an event with parameters, utilizing a cached list to prevent GC Spikes.
    /// </summary>
    public void Log(string eventName, Parameter[] parameters = null)
    {
#if !UNITY_EDITOR
        if (!IsEnabled || !IsInitialized) return;

        if (parameters == null || parameters.Length == 0)
        {
            FirebaseAnalytics.LogEvent(eventName);
        }
        else
        {
            FirebaseAnalytics.LogEvent(eventName, parameters);
        }
#endif
    }

    /// <summary>
    /// Tracks In-App Purchases with minimized memory allocation.
    /// </summary>
    public void TrackIAPPurchase(bool isSuccess, string productId, double price, string currency, string transactionId)
    {
#if !UNITY_EDITOR
        if (!IsEnabled || !IsInitialized)
        {
            return;
        }

        if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(currency) || string.IsNullOrEmpty(transactionId))
        {
            Debug.LogError("[FirebaseManager] TrackIAPPurchase missing required parameters.");
            return;
        }

        var items = new[]
        {
            new Dictionary<string, object>
            {
                { "item_id", productId },
                { "price", price },
                { "quantity", 1 }
            }
        };

        cachedParameterList.Clear();
        cachedParameterList.Add(new Parameter("value", price));
        cachedParameterList.Add(new Parameter("currency", currency));
        cachedParameterList.Add(new Parameter("transaction_id", transactionId ?? string.Empty));
        cachedParameterList.Add(new Parameter("items", items));

        string eventName = isSuccess ? FirebaseAnalytics.EventPurchase : "purchase_cancel";

        FirebaseAnalytics.LogEvent(eventName, cachedParameterList.ToArray());

        Debug.Log($"[FirebaseManager] TrackIAPPurchase sent: {eventName}, revenue: {price}");
#endif
    }

}