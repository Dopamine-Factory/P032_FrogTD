using UnityEngine;

public class Monster : MonoBehaviour
{
    public MonsterAI AI { get; private set; }

    public void Spawn(GameNormal game, Monster_form data, StageMonsterData stageMonsterData)
    {
        AI    ??= new MonsterAI();
        AI.SetData(data, stageMonsterData);
        AI.DeadCallback = OnDead;
        AI.ATKCallback  = game.AttackBarricade;
        AI.SetMonster(this);
        AI.MoveStart();

        void OnDead(Monster m)
        {
            game.RemoveMonster(m);
            ResourceManager.Instance.ReleaseInstance(m);
        }
    }

    /// <summary>히어로의 투사체가 명중 시 호출</summary>
    public void Hit(float damage)
    {
        AI?.Hit(damage);
    }
}
