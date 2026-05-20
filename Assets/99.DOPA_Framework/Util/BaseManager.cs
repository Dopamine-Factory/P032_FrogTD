using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseSystemManager<T> : Singleton<T>, IBaseManager where T : BaseSystemManager<T>
{
    public bool IsEnabled => isEnabled;
    [SerializeField] private bool isEnabled = true;
    public virtual string Id => gameObject.name;
    [Tooltip("이 매니저가 초기화/실행 전에 반드시 완료돼야 하는 다른 매니저의 ID 리스트")]
    public List<string> Dependencies = new();

    private HashSet<string> initializedDependencies = new();
    private bool _areDependenciesInitialized = false;
    private bool _isFullyInitialized = false;
    private List<IBaseManager> subscribedManagers = new List<IBaseManager>();
    public bool IsSingletonInitialized => Singleton<T>.IsInitialized;
    public bool AreDependenciesInitialized => _areDependenciesInitialized;
    public bool IsFullyInitialized => _isFullyInitialized;

    public event Action<string> OnDependenciesInitialized;
    public event Action<string> OnFullyInitialized;
    public Action<float> OnInitializeProgressCallback;

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();
        
        ManagerRegistry.RegisterManager(Id, this);

        if (!isEnabled)
        {
            return;
        }
    }

    public virtual void Initialize()
    {
        if (!IsSingletonInitialized || !isEnabled)
        {
            return;
        }

        if (Dependencies.Count == 0)
        {
            _areDependenciesInitialized = true;
            CompleteInitialization();
            return;
        }

        foreach (var depId in Dependencies)
        {
            var depManager = ManagerRegistry.GetManager(depId);
            if (depManager == null)
            {
                continue;
            }

            // 이벤트 중복 구독 방지하기 위해 체크
            bool alreadyInitialized = initializedDependencies.Contains(depId);
            if (!depManager.IsFullyInitialized && !alreadyInitialized)
            {
                depManager.OnFullyInitialized += OnDependencyFullyInitialized;

                // Track the subscription for safe cleanup later
                if (!subscribedManagers.Contains(depManager))
                {
                    subscribedManagers.Add(depManager);
                }
            }
            else if (!alreadyInitialized)
            {
                initializedDependencies.Add(depId);
            }
        }

        TryInitialize();
    }

    private void OnDependencyFullyInitialized(string depId)
    {
        if (initializedDependencies.Contains(depId)) return;
        initializedDependencies.Add(depId);
        TryInitialize();
    }

    private void TryInitialize()
    {
        if (_isFullyInitialized || !isEnabled) return;
        if (initializedDependencies.Count < Dependencies.Count) return;

        _areDependenciesInitialized = true;
        CompleteInitialization();
        OnDependenciesInitialized?.Invoke(Id);
    }

    protected virtual void CompleteInitialization()
    {
        PostInitializeWrapper();
    }

    protected void PostInitializeWrapper()
    {
        if (_isFullyInitialized) return;
        if (!isEnabled) return;

        ExecutePostInitialization();
        _isFullyInitialized = true;
        OnInitializeProgressCallback?.Invoke(1);
        OnFullyInitialized?.Invoke(Id);
    }

    public override void ExecutePostInitialization()
    {
        if (!IsSingletonInitialized || !AreDependenciesInitialized)
        {
            return;
        }
        base.ExecutePostInitialization();
    }


    protected override void OnDestroy()
    {
        if (subscribedManagers != null)
        {
            foreach (var depManager in subscribedManagers)
            {
                if (depManager != null)
                {
                    depManager.OnFullyInitialized -= OnDependencyFullyInitialized;
                }
            }
            subscribedManagers.Clear();
        }

        OnDependenciesInitialized = null;
        OnFullyInitialized = null;

        base.OnDestroy();
    }
}
