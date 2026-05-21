using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using static LocalSaveBattle;
using UnityEngine.SocialPlatforms;

/// <summary>
/// 하단 28칸 머지 그리드 슬롯 하나.
/// 잠금(locked)/개방(unlocked) 상태를 가지며
/// 잠금 상태에서는 드래그/드롭 불가.
/// </summary>
[RequireComponent(typeof(Image))]
public class MergeSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── UI 참조 ──────────────────────────────────────────────────
    [SerializeField] protected GameObject weaponSlot;   // 자식 오브젝트: 무기 아이콘 + 레벨 텍스트
    [SerializeField] protected Image weaponIcon;
    [SerializeField] protected TextMeshProUGUI lvTxt;
    [SerializeField] protected GameObject lockOverlay;

    // ── 상태 ────────────────────────────────────────────────────
    public int Index { get; protected set; } = -1; // 그리드 내 인덱스 (0~27)
    public WeaponData Data { get; protected set; } = new WeaponData { weaponType = WeaponType.None, level = 0 };
    public bool IsLocked { get; protected set; } = true;

    // ── 보드 참조 ────────────────────────────────────────────────
    public MergeBoard Board { get; set; }


    void Awake()
    {
        // 기본값: 잠금
        ApplyLockVisual();
    }

    // ── 잠금/개방 ────────────────────────────────────────────────
    public void Unlock()
    {
        IsLocked = false;
        ApplyLockVisual();
    }

    public void Lock()
    {
        IsLocked = true;
        ApplyLockVisual();
    }

    protected void ApplyLockVisual()
    {
        lockOverlay.SetActive(IsLocked);
        weaponSlot.SetActive(!IsLocked);

        if (!IsLocked) Refresh();
    }

    // ── 데이터 갱신 ──────────────────────────────────────────────
    public void SetIndex(int idx)
    {
        Index = idx;
    }

    public void Initialize(MergeBoardInfo data)
    {
        IsLocked = data.IsUnlocked;
        Data = new WeaponData { weaponType = data.WeaponData.weaponType, level = data.WeaponData.level };

        ApplyLockVisual();
    }

    public virtual void SetData(WeaponType type, int level)
    {
        if (IsLocked) return;

        Data = new WeaponData { weaponType = type, level = level };
       
        LocalSaveManager.Battle.SetMergeBoardWeapon(Index, Data);
        
        Refresh();
    }

    public void Clear()
    {
        Data = new WeaponData { weaponType = WeaponType.None, level = 0 };

        Refresh();
    }

    protected virtual void Refresh()
    {
        bool has = !Data.IsEmpty;

        if (weaponSlot != null) weaponSlot.SetActive(has);
        if (has)
        {
            int spriteIndex = Data.level > 3 ? 3 : Data.level;
            string spriteName = $"Weapon{spriteIndex}";
            var sprite = ResourceManager.Instance.GetSprite(spriteName);

            if (weaponIcon != null) weaponIcon.sprite = sprite;
            if (lvTxt != null) lvTxt.text = $"Lv.{Data.level}";
        }
    }

    // ── 드래그 ──────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (IsLocked || Data.IsEmpty) return;

        weaponSlot.SetActive(false);

        Board.OnMergeSlotBeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 드래그 중인 마우스 데이터를 보드로 전달합니다.
        Board.OnMergeSlotDrag(eventData);
    }

    // 마우스를 뗄 때 발생합니다.
    public void OnEndDrag(PointerEventData eventData)
    {
        // 보드에게 드롭 로직을 처리하라고 알립니다.
        Board.DropDragMergeSlot(Data, eventData);
    }
}
