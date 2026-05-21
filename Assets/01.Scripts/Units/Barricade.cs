using System;
using UniRx;
using UnityEngine;

public class Barricade : MonoBehaviour
{
    public ReactiveProperty<float> HP = new ReactiveProperty<float>(10) { Value = 10 };

    public Action DeadCallback;

    public void Hit(float atk)
    {
        if (HP.Value <= 0) return;

        HP.Value -= atk;

        if (HP.Value <= 0)
        {
            HP.Value = 0;
            DeadCallback?.Invoke();
        }
    }
}
