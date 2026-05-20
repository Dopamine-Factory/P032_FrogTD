using UnityEngine;

public class Monster : MonoBehaviour
{
    public MonsterAI AI { get; private set; }


    public void Spawn(GameNormal game)
    {
        AI ??= new MonsterAI();
        AI.DeadCallback = game.RemoveMonster;
        AI.ATKCallback = game.AttackBarricade;
        AI.SetMonster(this);
        AI.MoveStart();
    }


}
