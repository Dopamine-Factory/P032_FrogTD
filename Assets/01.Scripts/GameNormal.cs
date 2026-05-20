using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class GameNormal : GameBase
{
    [SerializeField] Transform respawnPointTR;
    [SerializeField] Barricade barricade;


    IDisposable gameTimerDisposal;

    StageData currstageData;

    List<Monster> monsters = new List<Monster>();

    double gamePlayTime = 0;
    int timeIndex = 0;

    protected override void InitializeGameComponents()
    {
        GameInitialize().Forget();
    }

    private async UniTaskVoid GameInitialize()
    {
        await SetCurrentStageData(1, 1);

        barricade.hp = 1;
        gamePlayTime = 0;

        GameManager.GameState.ChangeState(GameStateController.GameState.GameStart);
    }

    public override void GameStart()
    {
        gameTimerDisposal?.Dispose();
        gameTimerDisposal = Observable.EveryUpdate().Subscribe(TimeUpdate);
    }

    private void TimeUpdate(long obj)
    {
        gamePlayTime += Time.deltaTime;

        for (int i = timeIndex; i < currstageData.StageSpawnLength; ++i)
        {
            StageSpawnData stageSpawnData = currstageData.StageSpawnDatas[i];
            if (stageSpawnData.Time <= gamePlayTime)
            {
                ++timeIndex;

                for (int j = stageSpawnData.stageMonsterDatas.Length - 1; j > -1; --j)
                {
                    Vector3 spawnPoint = respawnPointTR.position;
                    spawnPoint.y = stageSpawnData.stageMonsterDatas[j].PosY;

                    var monster = ResourceManager.Instance.GetInstance<Monster>();


                    monster.transform.SetParent(gameContainers[1]);
                    monster.transform.position = spawnPoint;
                    monsters.Add(monster);
                    monster.Spawn(this);
                }
            }
        }
    }

    private async UniTask SetCurrentStageData(int stage, int wave)
    {
        var handler = Addressables.LoadAssetAsync<TextAsset>($"StageData/{stage}-{wave}.csv");

        await handler;

        currstageData = new StageData();
        currstageData.Converter(handler.Result.text);

        Addressables.Release(handler);
    }


    public void AttackBarricade(float atk)
    {
        barricade.Hit(atk);
    }

    public void RemoveMonster(Monster monster)
    {
        monsters.Remove(monster);

        if (monsters.Count == 0)
        {
            GameManager.GameState.ChangeState(GameStateController.GameState.GameClear);
        }
    }
}
