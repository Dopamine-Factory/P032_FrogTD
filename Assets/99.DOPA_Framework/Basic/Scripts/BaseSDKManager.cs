using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseSDKManager : MonoBehaviour
{
    [SerializeField] protected bool isEnabled = true;
    [SerializeField] protected float initDelay = 0f; // 초기화 전 딜레이 (초)

    public List<BaseSDKManager> sdkManagers = new List<BaseSDKManager>();
    public List<string> Dependencies = new();

    private HashSet<string> initializedDependencies = new();
    private bool _isFullyInitialized = false;

    public bool IsEnabled => isEnabled;
    public bool IsFullyInitialized => _isFullyInitialized;
    

    public event Action<string> OnFullyInitialized;

    public abstract IEnumerator Initialize(Action<float> progressCallback, Action<bool> onComplete);

    public IEnumerator InitializeHierarchy(Action<float> overallProgress, Action<bool> overallComplete)
    {
        if (!IsEnabled)
        {
            Debug.LogWarning($"[BaseSDKManager] InitializeHierarchy {name} IsEnabled False");
            overallComplete?.Invoke(true);
            yield break;
        }

        // 0. 의존성 처리(상위 종속 매니저들 트리상에서)
        if (Dependencies.Count > 0)
        {
            int readyCount = 0;
            foreach (var depId in Dependencies)
            {
                var depManager = ManagerRegistry.GetManager(depId);
                if (depManager == null)
                {
                    Debug.LogError($"[BaseSDKManager] InitializeHierarchy {depId}: Dependency not found for {name}");
                    continue;
                }
                if (!depManager.IsFullyInitialized)
                {
                    depManager.OnFullyInitialized += id =>
                    {
                        readyCount++;
                        if (readyCount >= Dependencies.Count)
                        {
                            // 의존성 모두 준비 완료시 초기화 진행
                            StartCoroutine(InternalInitialize(overallProgress, overallComplete));
                        }
                    };
                }
                else
                {
                    readyCount++;
                }
            }
            // 최상위의존이 전부 완료시에만 실제 초기화
            if (readyCount >= Dependencies.Count)
            {
                yield return StartCoroutine(InternalInitialize(overallProgress, overallComplete));
            }
        }
        else
        {
            yield return StartCoroutine(InternalInitialize(overallProgress, overallComplete));
        }
    }

    private IEnumerator InternalInitialize(Action<float> overallProgress, Action<bool> overallComplete)
    {
        Debug.Log($"[BaseSDKManager] InternalInitialize {name} Start");

        bool parentSuccess = false;
        float parentProgress = 0f;

        yield return StartCoroutine(Initialize(
            progress =>
            {
                parentProgress = progress;
                overallProgress?.Invoke(parentProgress * ((sdkManagers.Count + 1) / (sdkManagers.Count + 1)));
            },
            success =>
            {
                parentSuccess = success;
            })
        );

        if (!parentSuccess)
        {
            Debug.LogError($"[BaseSDKManager] InternalInitialize {name} Fail");
            overallComplete?.Invoke(false);
            yield break;
        }

        _isFullyInitialized = true;
        OnFullyInitialized?.Invoke(name);

        // 자식 SDK들 순차 초기화
        int count = sdkManagers.Count;
        for (int i = 0; i < count; i++)
        {
            bool childSuccess = false;
            yield return StartCoroutine(
                sdkManagers[i].InitializeHierarchy(
                    childProgress =>
                    {
                        float totalProgress = (1f + i + childProgress) / (count + 1);
                        overallProgress?.Invoke(totalProgress);
                    },
                    result => { childSuccess = result; }
                )
            );
            if (!childSuccess)
            {
                overallComplete?.Invoke(false);
                yield break;
            }
        }

        overallProgress?.Invoke(1f); // 100% 완료 신호
        overallComplete?.Invoke(true);
    }
}