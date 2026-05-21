using UnityEngine;

[System.Serializable]
public enum WeaponType
{
    None = 0,
    TYPE_1,     // 단검
    TYPE_2,     // 검
    TYPE_3,     // 활
    TYPE_4,     // 지팡이
    TYPE_5,     // 도끼
}

[System.Serializable]
public struct WeaponData
{
    public WeaponType weaponType;
    public int level;

    /// <summary>무기 공격력 = 기본 공격력 * 레벨</summary>
    public float atk => weaponType == WeaponType.None ? 0f : BaseAtk(weaponType) * level;

    public bool IsEmpty => weaponType == WeaponType.None;


    /// <summary>머지 가능 여부 — 같은 종류 + 같은 레벨</summary>
    public bool CanMergeWith(WeaponData other)
    {
        if (other.weaponType == WeaponType.None || other.IsEmpty || IsEmpty) return false;
        return weaponType == other.weaponType && level == other.level;
    }

    /// <summary>무기 종류별 기본 공격력</summary>
    public static float BaseAtk(WeaponType type) => type switch
    {
        WeaponType.TYPE_1 => 5f,
        WeaponType.TYPE_2 => 10f,
        WeaponType.TYPE_3 => 8f,
        WeaponType.TYPE_4 => 12f,
        WeaponType.TYPE_5 => 15f,
        _ => 0f,
    };

    /// <summary>무기 표시 이름</summary>
    public string DisplayName => weaponType == WeaponType.None
        ? "없음"
        : $"{KorName(weaponType)} Lv.{level}";

    public static string KorName(WeaponType t) => t switch
    {
        WeaponType.TYPE_1 => "단검",
        WeaponType.TYPE_2 => "검",
        WeaponType.TYPE_3 => "활",
        WeaponType.TYPE_4 => "지팡이",
        WeaponType.TYPE_5 => "도끼",
        _ => "없음",
    };

    public Sprite GetSprite()
    {
        if (weaponType == WeaponType.None) return null;
        int spriteIndex = level > 3 ? 3 : level;
        string spriteName = $"Weapon{spriteIndex}";
        return ResourceManager.Instance.GetSprite(spriteName);
    }
}
