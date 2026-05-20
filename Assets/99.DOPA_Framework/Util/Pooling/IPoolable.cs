using UnityEngine;

public interface IPoolable
{
    GameObject GameObject { get; }

    void OnAcquire();

    void OnRelease();
}