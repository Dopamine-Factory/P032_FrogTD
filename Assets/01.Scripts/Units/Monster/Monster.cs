using UnityEngine;

public class Monster : MonoBehaviour
{
    public float moveSpeed = 3.0f;
    public float atkInterval = 1.0f;
    public float atk = 1.0f;
    public float hp = 1;


    MonsterAI ai;


    public void Spawn()
    {
        ai ??= new MonsterAI();
        ai.SetMonster(this);
        ai.MoveStart();
    }


}
