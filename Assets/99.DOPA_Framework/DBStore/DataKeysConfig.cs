using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DataKeys", menuName = "Game/Data Keys Config")]
public class DataKeysConfig : ScriptableObject
{
    [System.Serializable]
    public class DataKeyEntry
    {
        public string KeyName;
        public string Description;
        [Tooltip("필수 데이터 (초기 로드)")]
        public bool IsRequired = false;
    }

    [SerializeField] private List<DataKeyEntry> allKeys = new()
    {
        new() { KeyName = "player_info", Description = "플레이어 기본 정보", IsRequired = true },
        new() { KeyName = "inventory", Description = "인벤토리", IsRequired = true },
        new() { KeyName = "progress", Description = "진행 상황", IsRequired = false },
        new() { KeyName = "mailbox_global", Description = "우체통(전역)", IsRequired = false },
        new() { KeyName = "mailbox_user", Description = "우체통(유저)", IsRequired = false }
    };

    /// <summary>필수 데이터만 초기 로드</summary>
    public HashSet<string> GetInitialKeySet() => 
        new(allKeys.FindAll(k => k.IsRequired).ConvertAll(k => k.KeyName));

    public HashSet<string> GetAllKeySet() => 
        new(allKeys.ConvertAll(k => k.KeyName));
}