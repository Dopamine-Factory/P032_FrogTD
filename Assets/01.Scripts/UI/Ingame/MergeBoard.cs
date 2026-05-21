using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static LocalSaveBattle;

public class MergeBoard : MonoBehaviour
{
    // ── 직렬화 ───────────────────────────────────────────────────
    [Header("슬롯 부모")]
    [SerializeField] Transform chrSlotBar;
    [SerializeField] Transform slotBar1;

    [Header("ProductionBtn > Btn")]
    [SerializeField] Button productionBtn;

    [SerializeField] DragMergeSlotUI dragMergeSlotUI;


    // ── 슬롯 목록 ────────────────────────────────────────────────
    readonly List<ChrSlotUI> _chrSlots = new();
    readonly List<MergeSlotUI> _mergeSlots = new();

    static readonly int[] UnlockSequence = BuildUnlockSequence();

    int _unlockedCount = 0;   // 현재까지 개방된 칸 수

    WeaponData _draggingData; // 드래그 중인 무기 데이터 (드래그 시작 시 설정, 드롭 시 클리어)
    int _dragSourceIndex;    // 드래그 시작 슬롯 인덱스 (MergeSlot이면 0~27, ChrSlot이면 -1~-3)


    // ── 초기화 ───────────────────────────────────────────────────
    public void Initialize()
    {
        dragMergeSlotUI.Board = this;

        CollectSlots();

        InitHeroBoard();
        InitMergeBoard();

        if (productionBtn != null)
            productionBtn.onClick.AddListener(OnProductionBtnClick);
    }

    void CollectSlots()
    {
        _chrSlots.Clear();
        _mergeSlots.Clear();

        int index = 0;

        if (chrSlotBar != null)
        {
            index = 0;
            foreach (Transform child in chrSlotBar)
            {
                var s = child.GetComponent<ChrSlotUI>();
                if (s != null)
                {
                    s.SetIndex(--index);
                    s.Board = this;
                    _chrSlots.Add(s);
                }
            }
        }
        if (slotBar1 != null)
        {
            index = 0;

            foreach (Transform child in slotBar1)
            {
                var s = child.GetComponent<MergeSlotUI>();
                if (s != null)
                {
                    s.SetIndex(index++);
                    s.Board = this;
                    _mergeSlots.Add(s);
                }
            }
        }
    }

    void InitHeroBoard()
    {
        for (int i = 0; i < _chrSlots.Count; i++)
        {
            _chrSlots[i].Lock();
        }

        LocalSaveBattle battle = LocalSaveManager.Battle;
        EquippedSlotInfo info;
        ChrSlotUI slot;
        for (int i = 0; i < _chrSlots.Count; i++)
        {
            info = battle.GetEquippedSlotInfo(i);
            if (info.IsUnlocked)
            {
                slot = _chrSlots[i];
                slot.Initialize(info);
            }
        }
    }

    void InitMergeBoard()
    {
        // 모든 슬롯 잠금
        foreach (var s in _mergeSlots) s.Lock();

        LocalSaveBattle battle = LocalSaveManager.Battle;
        if (battle.GetMergeBoardInfo(GetUnlockSlotIndex(0)).IsUnlocked)
        {
            MergeBoardInfo info;
            _unlockedCount = 0;

            for (int i = 0; i < 28; i++)
            {
                info = battle.GetMergeBoardInfo(i);

                if (info.IsUnlocked) ++_unlockedCount;

                _mergeSlots[i].Initialize(info);
            }
        }
        else
        {
            for (int i = 0; i < Mathf.Min(UnlockSequence.Length, 6); i++)
                UnlockAt(i);
            _unlockedCount = 6;
        }
    }

    int GetUnlockSlotIndex(int seqIndex)
    {
        if (seqIndex < 0 || seqIndex >= UnlockSequence.Length) return -1;
        return UnlockSequence[seqIndex];
    }

    void UnlockAt(int seqIndex)
    {
        if (seqIndex < 0 || seqIndex >= UnlockSequence.Length) return;

        int slotIdx = UnlockSequence[seqIndex];

        if (slotIdx < 0 || slotIdx >= _mergeSlots.Count) return;

        _mergeSlots[slotIdx].Unlock();

        LocalSaveManager.Battle.UnlockMergeBoard(slotIdx);
    }

    // ── ProductionBtn 클릭 ───────────────────────────────────────
    void OnProductionBtnClick()
    {
        productionBtn.GetComponent<Animator>()?.SetTrigger("Select");

        // 개방된 슬롯 중 비어있는 슬롯 탐색
        var emptySlots = new List<MergeSlotUI>();
        for (int i = 0; i < _unlockedCount && i < UnlockSequence.Length; i++)
        {
            int idx = UnlockSequence[i];
            if (idx >= 0 && idx < _mergeSlots.Count && _mergeSlots[idx].Data.IsEmpty)
                emptySlots.Add(_mergeSlots[idx]);
        }

        if (emptySlots.Count == 0)
        {
            return;
        }

        // 랜덤 빈 슬롯에 Lv1 무기 배치
        var target = emptySlots[Random.Range(0, emptySlots.Count)];
        target.SetData(WeaponType.TYPE_1, 1);
    }

    // ── 다음 슬롯 개방 (외부에서 호출 가능) ─────────────────────
    public bool UnlockNextSlot()
    {
        if (_unlockedCount >= UnlockSequence.Length) return false;
        UnlockAt(_unlockedCount);
        _unlockedCount++;
        return true;
    }

    // ── 드래그&드롭 처리 ────────────────────────────────────────
    public void OnMergeSlotBeginDrag(MergeSlotUI slot)
    {
        if (slot.Data.IsEmpty) return;

        _draggingData = slot.Data;

        _dragSourceIndex = slot.Index;

        dragMergeSlotUI.Show(slot.Data.weaponType, slot.Data.level);
    }

    // 🌟 새로 추가: 슬롯에서 드래그 중일 때 호출됨
    public void OnMergeSlotDrag(PointerEventData eventData)
    {
        // DragMergeSlotUI의 위치를 마우스 위치로 업데이트
        dragMergeSlotUI.UpdatePosition(eventData);
    }

    public void DropDragMergeSlot(WeaponData data, PointerEventData eventData)
    {
        _draggingData = default;
        dragMergeSlotUI.Clear();

        // 1. 드롭된 위치 아래에 있는 모든 UI 요소를 검사합니다.
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        MergeSlotUI targetMergeSlot = null;
        ChrSlotUI targetChrSlot = null;

        // 2. 검사 결과를 순회하며 원하는 슬롯 컴포넌트를 찾습니다.
        foreach (var result in results)
        {
            // 드래그 중인 자기 자신(DragMergeSlotUI)은 판단에서 제외
            if (result.gameObject == dragMergeSlotUI.gameObject) continue;

            // 계층 구조를 거슬러 올라가며 MergeSlotUI나 ChrSlotUI가 있는지 확인
            targetMergeSlot = result.gameObject.GetComponentInParent<MergeSlotUI>();
            if (targetMergeSlot != null && targetMergeSlot.Data.IsEmpty) break; // 찾았으면 순회 중단

            targetChrSlot = result.gameObject.GetComponentInParent<ChrSlotUI>();
            if (targetChrSlot != null) break;   // 찾았으면 순회 중단
        }

        // 3. 찾은 슬롯에 따라 알맞은 로직을 실행합니다.
        if (targetMergeSlot != null)
        {
            OnMergeSlotDrop(data, targetMergeSlot);
        }
        else if (targetChrSlot != null)
        {
            OnMergeToHeroDrop(data, targetChrSlot);
        }
        else
        {
            GetMergeSlotUI(_dragSourceIndex).SetData(data.weaponType, data.level);
        }
    }

    MergeSlotUI GetMergeSlotUI(int index)
    {
        if (index >= 0) return _mergeSlots[index];
        else return _chrSlots[(-index) + 1] as MergeSlotUI;
    }

    public void OnMergeSlotDrop(WeaponData data, MergeSlotUI to)
    {
        if (!to.IsLocked && to.Data.IsEmpty)
        {
            to.SetData(data.weaponType, data.level);
            GetMergeSlotUI(_dragSourceIndex).Clear();
        }
        else if (!to.IsLocked && data.CanMergeWith(to.Data))
        {
            to.SetData(to.Data.weaponType, to.Data.level + 1);
            GetMergeSlotUI(_dragSourceIndex).Clear();
            OnMergeComplete();
        }
        else
        {
            GetMergeSlotUI(_dragSourceIndex).SetData(data.weaponType, data.level);
        }
    }

    public void OnMergeToHeroDrop(WeaponData data, ChrSlotUI to)
    {
        if (!to.IsLocked && to.Data.IsEmpty)
        {
            to.SetData(data.weaponType, data.level);
            GetMergeSlotUI(_dragSourceIndex).Clear();
        }
        else if (!to.IsLocked && data.CanMergeWith(to.Data))
        {
            to.SetData(to.Data.weaponType, to.Data.level + 1);
            GetMergeSlotUI(_dragSourceIndex).Clear();
            OnMergeComplete();
        }
        else
        {
            GetMergeSlotUI(_dragSourceIndex).SetData(to.Data.weaponType, to.Data.level);
            to.SetData(data.weaponType, data.level);
        }

        NotifyHeroAtkChanged();
    }

    // ── 유틸 ────────────────────────────────────────────────────
    void OnMergeComplete()
    {
        NotifyHeroAtkChanged();
    }

    void NotifyHeroAtkChanged()
    {
        for (int i = 0; i < _chrSlots.Count; i++)
            Debug.Log($"[MergeBoard] Hero[{i}] ATK={_chrSlots[i].FinalAtk:F1}");
    }

    // ── 외부 API ─────────────────────────────────────────────────
    public void SetChrSlot(int i, WeaponData d)
    {
        if (i >= 0 && i < _chrSlots.Count) _chrSlots[i].SetData(d.weaponType, d.level);
    }
    public void SetHeroMultiplier(int i, float m)
    {
        if (i >= 0 && i < _chrSlots.Count) _chrSlots[i].HeroAtkMultiplier = m;
    }
    public int ChrSlotCount => _chrSlots.Count;
    public int MergeSlotCount => _mergeSlots.Count;
    public ChrSlotUI GetChrSlot(int i) => _chrSlots[i];
    public MergeSlotUI GetMergeSlot(int i) => _mergeSlots[i];

    // ── 개방 순서 생성 ───────────────────────────────────────────
    static int[] BuildUnlockSequence()
    {
        // row 1-4, col 1-7, index = (row-1)*7 + (col-1)
        int Idx(int r, int c) => (r * 7) + c;

        var seq = new List<int>();

        seq.Add(Idx(1, 2)); seq.Add(Idx(2, 2));
        seq.Add(Idx(1, 3)); seq.Add(Idx(2, 3));
        seq.Add(Idx(1, 4)); seq.Add(Idx(2, 4));

        seq.Add(Idx(1, 5)); seq.Add(Idx(2, 5));
        seq.Add(Idx(3, 2)); seq.Add(Idx(3, 3)); seq.Add(Idx(3, 4));
        seq.Add(Idx(1, 1)); seq.Add(Idx(2, 1));
        seq.Add(Idx(0, 2)); seq.Add(Idx(0, 3)); seq.Add(Idx(0, 4));
        seq.Add(Idx(0, 5)); seq.Add(Idx(3, 5)); seq.Add(Idx(3, 1)); seq.Add(Idx(0, 1));
        seq.Add(Idx(1, 6)); seq.Add(Idx(2, 6));
        seq.Add(Idx(1, 0)); seq.Add(Idx(2, 0));
        seq.Add(Idx(0, 6)); seq.Add(Idx(3, 6)); seq.Add(Idx(3, 0)); seq.Add(Idx(0, 0));


        return seq.ToArray();
    }
}
