using System;
using System.Collections.Generic;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }
    public BuffUI buffUI;

    [System.Serializable]
    public class BuffIconMapping
    {
        public BuffType Type;
        public Sprite Icon;
    }

    [Header("Settings")]
    [SerializeField] private List<BuffIconMapping> buffIcons = new List<BuffIconMapping>();

    private Dictionary<BuffType, BuffData> activeBuffs = new Dictionary<BuffType, BuffData>();

    public event Action<BuffType, BuffData> OnBuffChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        buffUI?.gameObject.SetActive(true);
    }

    private void Update()
    {
        var expired = new List<BuffType>();
        foreach (var pair in activeBuffs)
        {
            pair.Value.RemainTime -= Time.deltaTime;
            if (pair.Value.RemainTime <= 0) expired.Add(pair.Key);
        }
        foreach (var type in expired) RemoveBuff(type);
    }

    public void AddBuff(BuffType type, float duration)
    {
        Debug.Log("#@#@#@#@#@#@#@#@#@#@#@# BuffManager AddBuff");

        if (activeBuffs.TryGetValue(type, out var existing))
        {
            existing.RemainTime = duration; // 기존 버프 갱신
            existing.MaxDuration = duration;
        }
        else
        {
            activeBuffs[type] = new BuffData
            {
                MaxDuration = duration,
                RemainTime = duration
            };
        }
        OnBuffChanged?.Invoke(type, activeBuffs[type]);
    }

    public void RemoveBuff(BuffType type)
    {
        if (activeBuffs.ContainsKey(type))
        {
            activeBuffs.Remove(type);
            OnBuffChanged?.Invoke(type, null);
        }
    }

    public Sprite GetBuffIcon(BuffType type)
    {
        foreach (var mapping in buffIcons)
            if (mapping.Type == type) return mapping.Icon;
        return null;
    }

    public bool HasBuff(BuffType type) => activeBuffs.ContainsKey(type);
    public BuffData GetBuffData(BuffType type) => activeBuffs.TryGetValue(type, out var data) ? data : null;
    public IEnumerable<KeyValuePair<BuffType, BuffData>> GetActiveBuffs() => activeBuffs;
}

[System.Serializable]
public class BuffData
{
    public float MaxDuration;
    public float RemainTime;
}

public enum BuffType
{
    ScoreDouble,
    AttackIncrease,
    DefenseIncrease,
    RageResistance,
    IronSkin
}
