using System;
using UniRx;
using UnityEngine;

public class MonsterAI
{
    Monster monster;

    IDisposable actionDisposal;

    public void SetMonster(Monster monster)
    {
        this.monster = monster;
    }

    public void MoveStart()
    {
        MoveStop();

        actionDisposal = Observable.EveryUpdate().Subscribe(Move).AddTo(monster);
    }

    protected void MoveStop()
    {
        if (actionDisposal != null)
        {
            actionDisposal?.Dispose();
            actionDisposal = null;
        }
    }

    protected virtual void Move(long obj)
    {
        monster.transform.Translate(monster.moveSpeed * Time.deltaTime * Vector3.left);
        if (monster.transform.position.x <= -1.8f)
        {
            MoveStop();

            monster.transform.position = new Vector3(-1.8f, monster.transform.position.y, monster.transform.position.z);
        }
    }

    protected void AttackStart()
    {
        actionDisposal?.Dispose();
        actionDisposal = Observable.Interval(TimeSpan.FromSeconds(monster.atkInterval)).Subscribe(Attack);
    }

    private void Attack(long obj)
    {
        
    }
}
