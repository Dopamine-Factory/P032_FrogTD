using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 머지 그리드 UI
/// 4행 x 7열 슬롯, 캐논 배치/이동/머지 처리
/// </summary>
public class MergeGridUI : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int rows = 4;
    [SerializeField] private int cols = 7;

    [Header("References")]
    [SerializeField] private Transform gridParent;       // GridLayoutGroup이 붙은 부모
    [SerializeField] private GameObject slotPrefab;      // MergeSlotUI 프리팹
    [SerializeField] private GameObject cannonPrefab;    // CannonItemUI 프리팹

    // 잠긴 슬롯 인덱스 (row * cols + col)
    private static readonly HashSet<int> LockedSlots = new HashSet<int>
    {
        // Row 0: col 2~6 잠금
        2, 3, 4, 5, 6,
        // Row 1: 전체 잠금
        7, 8, 9, 10, 11, 12, 13,
        // Row 2: col 3~6 잠금
        17, 18, 19, 20,
        // Row 3: 전체 잠금
        21, 22, 23, 24, 25, 26, 27
    };

    private MergeSlotUI[] slots;
    private int selectedIndex = -1;

    // 슬롯 데이터 (0 = 비어있음, 양수 = 캐논 레벨)
    private int[] gridData;

    void Awake()
    {
        gridData = new int[rows * cols];
        // 초기 캐논 배치
        gridData[0] = 1;  // (0,0)
        gridData[8] = 2;  // (1,1) -> row1은 잠금이지만 데이터 시연용
        gridData[9] = 3;
    }

    void Start()
    {
        BuildGrid();
        RefreshGrid();
    }

    void BuildGrid()
    {
        slots = new MergeSlotUI[rows * cols];
        for (int i = 0; i < rows * cols; i++)
        {
            var slotGO = Instantiate(slotPrefab, gridParent);
            slotGO.name = $"Slot_{i / cols}_{i % cols}";
            var slot = slotGO.GetComponent<MergeSlotUI>();
            int capturedIndex = i;
            slot.Init(capturedIndex, LockedSlots.Contains(capturedIndex), () => OnSlotClicked(capturedIndex));
            slots[i] = slot;
        }
    }

    public void RefreshGrid()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            bool isSelected = (i == selectedIndex);
            slots[i].Refresh(gridData[i], isSelected);
        }
    }

    void OnSlotClicked(int index)
    {
        if (LockedSlots.Contains(index)) return;

        // 선택된 슬롯이 없을 때
        if (selectedIndex == -1)
        {
            if (gridData[index] > 0)
            {
                selectedIndex = index;
                RefreshGrid();
            }
            return;
        }

        // 같은 슬롯 재클릭 → 선택 해제
        if (selectedIndex == index)
        {
            selectedIndex = -1;
            RefreshGrid();
            return;
        }

        int fromLevel = gridData[selectedIndex];
        int toLevel = gridData[index];

        // 머지: 같은 레벨끼리
        if (fromLevel > 0 && toLevel == fromLevel)
        {
            gridData[index] = fromLevel + 1;
            gridData[selectedIndex] = 0;
            selectedIndex = -1;
            RefreshGrid();
            OnMergeSuccess(index, gridData[index]);
            return;
        }

        // 이동: 빈 슬롯으로
        if (toLevel == 0)
        {
            gridData[index] = fromLevel;
            gridData[selectedIndex] = 0;
            selectedIndex = -1;
            RefreshGrid();
            return;
        }

        // 스왑: 다른 캐논과
        if (fromLevel > 0 && toLevel > 0)
        {
            gridData[index] = fromLevel;
            gridData[selectedIndex] = toLevel;
            selectedIndex = -1;
            RefreshGrid();
            return;
        }

        selectedIndex = -1;
        RefreshGrid();
    }

    void OnMergeSuccess(int index, int newLevel)
    {
        // 머지 성공 이펙트 (슬롯에 펄스 애니메이션 요청)
        slots[index].PlayMergeEffect();
    }

    // 외부에서 캐논 스폰 (빈 슬롯에 레벨 1 캐논 추가)
    public bool SpawnCannon()
    {
        for (int i = 0; i < gridData.Length; i++)
        {
            if (gridData[i] == 0 && !LockedSlots.Contains(i))
            {
                gridData[i] = 1;
                RefreshGrid();
                return true;
            }
        }
        return false; // 빈 슬롯 없음
    }

    // 오토 머지: 같은 레벨 쌍 하나 자동 머지
    public bool AutoMergeOnce()
    {
        for (int i = 0; i < gridData.Length; i++)
        {
            if (gridData[i] == 0 || LockedSlots.Contains(i)) continue;
            for (int j = i + 1; j < gridData.Length; j++)
            {
                if (LockedSlots.Contains(j)) continue;
                if (gridData[j] == gridData[i])
                {
                    gridData[j]++;
                    gridData[i] = 0;
                    RefreshGrid();
                    slots[j].PlayMergeEffect();
                    return true;
                }
            }
        }
        return false;
    }
}
