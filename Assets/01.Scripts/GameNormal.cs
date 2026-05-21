using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;

public class GameNormal : GameBase
{
    [SerializeField] Transform respawnPointTR;
    [SerializeField] Barricade barricade;

    IDisposable gameTimerDisposal;

    StageData currStageData;
    double gamePlayTime = 0;
    int timeIndex = 0;

    const int MaxHeroCount = 7;
    Hero[] heroes = new Hero[MaxHeroCount];
    List<Monster> monsters = new List<Monster>();
    public List<Monster> Monsters => monsters;

    

    // ── 초기화 ───────────────────────────────────────────────────────
    protected override void InitializeGameComponents()
    {
        GameInitialize().Forget();
    }

    private async UniTaskVoid GameInitialize()
    {
        await CreateMergeBoard();
        await CreateHeros();

        await SetCurrentStageData(1, 1);

        // 바리케이드 hp 설정 & DeadCallback
        barricade.HP.Value = 10;
        barricade.DeadCallback = OnBarricadeDead;


        gamePlayTime = 0;
        timeIndex = 0;

        GameManager.GameState.ChangeState(GameState.GameStart);
    }

    private async UniTask CreateMergeBoard()
    {
        var mergeBoardPrefab = await Addressables.LoadAssetAsync<GameObject>("Prefabs/MergeBoard").ToUniTask();
        var mergeBoard = Instantiate(mergeBoardPrefab, gameContainers[2]);
        mergeBoard.transform.localPosition = Vector3.zero;
    }

    private async UniTask CreateHeros()
    {
        for (int i = 0; i < 7; ++i)
        {
            Hero hero = ResourceManager.Instance.GetInstance<Hero>();
            if (hero != null)
            {
                LocalSaveBattle.EquippedSlotInfo equippedSlotInfo = LocalSaveManager.Battle.GetEquippedSlotInfo(i);
                
                var heroForm = Tables.GetTable<HeroTable>().GetData(equippedSlotInfo.HeroID);

                hero.Initialize(heroForm, monsters);
                hero.transform.SetParent(gameContainers[0]);
                hero.transform.position = gameContainers[0].GetChild(i).position;
                heroes[i] = hero;
            }
        }
    }

    // ── 게임 시작 ────────────────────────────────────────────────────
    public override void GameStart()
    {
        for(int i = 0; i < MaxHeroCount; ++i)
        {
            if (heroes[i] != null)
            {
                heroes[i].StartAttack();
            }
        }

        gameTimerDisposal?.Dispose();
        gameTimerDisposal = Observable.EveryUpdate()
            .Subscribe(TimeUpdate);
    }

    // ── 게임 오버 ────────────────────────────────────────────────────
    public override void GameOver()
    {
        for(int i = 0; i < MaxHeroCount; ++i)
        {
            if (heroes[i] != null)
            {
                heroes[i].StopAttack();
            }
        }
        gameTimerDisposal?.Dispose();
    }

    // ── 게임 클리어 ──────────────────────────────────────────────────
    public override void GameClear()
    {
        for(int i = 0; i < MaxHeroCount; ++i)
        {
            if (heroes[i] != null)
            {
                heroes[i].StopAttack();
            }
        }
        gameTimerDisposal?.Dispose();
    }

    // ── 스폰 타이머 ──────────────────────────────────────────────────
    private void TimeUpdate(long _)
    {
        gamePlayTime += Time.deltaTime;

        for (int i = timeIndex; i < currStageData.StageSpawnLength; ++i)
        {
            StageSpawnData spawnData = currStageData.StageSpawnDatas[i];
            if (spawnData.Time > gamePlayTime) break;

            ++timeIndex;

            for (int j = 0; j < spawnData.stageMonsterDatas.Length; ++j)
            {
                SpawnMonster(spawnData.stageMonsterDatas[j]);
            }
        }
    }

    private void SpawnMonster(StageMonsterData monsterInfo)
    {
        // 테이블에서 몬스터 데이터 가져오기
        if (!Tables.GetTable<MonsterTable>().TryGetData(monsterInfo.Id, out var monsterForm))
        {
            Debug.LogWarning($"[GameNormal] Monster data not found: id={monsterInfo.Id}");
            return;
        }

        Vector3 spawnPos = respawnPointTR.position;
        spawnPos.y = monsterInfo.PosY;

        var monster = ResourceManager.Instance.GetInstance<Monster>();
        if (monster == null) return;

        monster.transform.SetParent(gameContainers[1]);
        monster.transform.position = spawnPos;

        monsters.Add(monster);
        monster.Spawn(this, monsterForm, monsterInfo);
    }

    // ── 외부 콜백 ────────────────────────────────────────────────────
    public void AttackBarricade(float atk)
    {
        barricade.Hit(atk);
    }

    public void RemoveMonster(Monster monster)
    {
        monsters.Remove(monster);

        if (monsters.Count == 0 && timeIndex >= currStageData.StageSpawnLength)
        {
            GameManager.GameState.ChangeState(GameState.GameClear);
        }
    }

    private void OnBarricadeDead()
    {
        GameManager.GameState.ChangeState(GameState.GameOver);
    }

    // ── 스테이지 데이터 로드 ─────────────────────────────────────────
    private async UniTask SetCurrentStageData(int stage, int wave)
    {
        var handler = Addressables.LoadAssetAsync<TextAsset>($"StageData/{stage}-{wave}.csv");
        await handler;

        currStageData = new StageData();
        currStageData.Converter(handler.Result.text);

        Addressables.Release(handler);
    }
}
