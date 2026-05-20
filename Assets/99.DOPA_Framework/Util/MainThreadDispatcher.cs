using System;
using System.Collections.Generic;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static void RunOnMainThread(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    private void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    private static MainThreadDispatcher _instance;
    public static MainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MainThreadDispatcher>();
                if (_instance == null)
                {
                    _instance = new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();
                }
            }
            return _instance;
        }
    }
}