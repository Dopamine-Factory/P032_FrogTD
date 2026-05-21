using System;
using UniRx;
using UnityEngine;

public class MonsterData
{
    public float moveSpeed   = 3.0f;
    public float atkInterval = 1.0f;
    public float atk         = 1.0f;
    public float hp          = 1f;
}

public class MonsterAI
{
    protected Monster monster;
    public MonsterData Data { get; protected set; }

    IDisposable actionDisposal;

    public Action<float>   ATKCallback;
    public Action<Monster> DeadCallback;

    public void SetMonster(Monster monster)
    {
        this.monster = monster;
    }

    /// <summary>테이블 데이터로 MonsterData 초기화</summary>
    public void SetData(Monster_form form, StageMonsterData stageMonsterData)
    {
        Data = new MonsterData
        {
            moveSpeed   = form.move_speed * (1 + stageMonsterData.Level * 0.1f),
            atkInterval = form.atk_interval,
            atk         = form.atk * (1 + stageMonsterData.Level * 0.3f),
            hp          = form.hp * (1 + stageMonsterData.Level * 2f),
        };
    }

    public void MoveStart()
    {
        ActionStop();
        actionDisposal = Observable.EveryUpdate()
            .Subscribe(Move)
            .AddTo(monster);
    }

    protected void ActionStop()
    {
        actionDisposal?.Dispose();
        actionDisposal = null;
    }

    protected virtual void Move(long _)
    {
        monster.transform.Translate(Data.moveSpeed * Time.deltaTime * Vector3.left);

        if (monster.transform.position.x <= -1.8f)
        {
            monster.transform.position = new Vector3(
                -1.8f,
                monster.transform.position.y,
                monster.transform.position.z);

            // 바리케이드 도달 → 이동 멈추고 공격 시작
            ActionStop();
            AttackStart();
        }
    }

    protected void AttackStart()
    {
        actionDisposal?.Dispose();
        actionDisposal = Observable
            .Interval(TimeSpan.FromSeconds(Data.atkInterval))
            .Subscribe(Attack)
            .AddTo(monster);
    }

    private void Attack(long _)
    {
        ATKCallback?.Invoke(Data.atk);
    }

    public void Hit(float damage)
    {
        if (Data == null || Data.hp <= 0) return;

        Data.hp -= damage;
        if (Data.hp <= 0)
        {
            ActionStop();
            DeadCallback?.Invoke(monster);
        }
    }
}
