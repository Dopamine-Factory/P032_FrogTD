using System;
using UniRx;
using UnityEngine;

public class MonsterData
{
    public float moveSpeed = 3.0f;
    public float atkInterval = 1.0f;
    public float atk = 1.0f;
    public float hp = 1;

}

public class MonsterAI
{
    Monster monster;
    public MonsterData Data {get; protected set;}

    IDisposable actionDisposal;

    public Action<float> ATKCallback;
    public Action<Monster> DeadCallback;

    public void SetMonster(Monster monster)
    {
        this.monster = monster;
    }

    public void MoveStart()
    {
        ActionStop();

        actionDisposal = Observable.EveryUpdate().Subscribe(Move).AddTo(monster);
    }

    protected void ActionStop()
    {
        if (actionDisposal != null)
        {
            actionDisposal?.Dispose();
            actionDisposal = null;
        }
    }

    protected virtual void Move(long obj)
    {
        monster.transform.Translate(Data.moveSpeed * Time.deltaTime * Vector3.left);
        if (monster.transform.position.x <= -1.8f)
        {
            ActionStop();

            monster.transform.position = new Vector3(-1.8f, monster.transform.position.y, monster.transform.position.z);
        }
    }

    protected void AttackStart()
    {
        actionDisposal?.Dispose();
        actionDisposal = Observable.Interval(TimeSpan.FromSeconds(Data.atkInterval)).Subscribe(Attack);
    }

    private void Attack(long obj)
    {
        ATKCallback?.Invoke(Data.atk);
    }

    public void Hit(float hp)
    {
        if (Data.hp <= 0)
            return;

        Data.hp -= hp;
        if (Data.hp <= 0)
        {
            ActionStop();

            DeadCallback?.Invoke(monster);
        }
    }
}
