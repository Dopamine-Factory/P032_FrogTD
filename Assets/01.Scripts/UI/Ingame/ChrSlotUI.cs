using UnityEngine;
using UnityEngine.UI;
using static LocalSaveBattle;

[RequireComponent(typeof(Image))]
public class ChrSlotUI : MergeSlotUI
{
    [SerializeField] Image heroImage;

    /// <summary>히어로 공격력 배율 (Hero_form.atk 기반, 백분율)</summary>
    public float HeroAtkMultiplier { get; set; } = 1.0f;

    /// <summary>최종 히어로 공격력 = 무기 atk * 배율</summary>
    public float FinalAtk => Data.atk * HeroAtkMultiplier;

    public void SetHero(uint heroID)
    {
        heroImage.sprite = ResourceManager.Instance.GetSprite($"Board_{heroID}");
    }

    public void Initialize(EquippedSlotInfo data)
    {
        IsLocked = data.IsUnlocked;
        Data = new WeaponData { weaponType = data.WeaponData.weaponType, level = data.WeaponData.level };
        
        SetHero(data.HeroID);

        ApplyLockVisual();
    }


    public override void SetData(WeaponType type, int level)
    {
        if (IsLocked) return;

        Data = new WeaponData { weaponType = type, level = level };
       
        LocalSaveManager.Battle.SetEquippedWeapon(-Index - 1, Data);
        
        Refresh();
    }

}
