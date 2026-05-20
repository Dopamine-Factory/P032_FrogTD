using System;
using UnityEngine;

public class Barricade : MonoBehaviour
{
    public double hp = 10;

    public Action DeadCallback;

    public void Hit(float atk)
    {
        hp -= atk;
        if(hp <= 0)
        {
            DeadCallback?.Invoke();
        }
    }
}
