using System;
using System.Collections.Generic;
using UnityEngine;

public class UserDataManager : Singleton<UserDataManager>
{
    public enum DataKey
    {
        IsFirstOpen,
        IsAdsRemoved,
        CurrentLevel,
        CurrentScore,
        HighestScore,
        SoundEffectVolume,
        BGMVolume,
        IsHapticOn,

        GameTry
    }

    private readonly Dictionary<DataKey, object> fixedDataCache = new Dictionary<DataKey, object>();
    private readonly Dictionary<DataKey, KeyInfo> fixedKeyInfos = new Dictionary<DataKey, KeyInfo>()
    {
        { DataKey.IsFirstOpen, new KeyInfo(typeof(bool), true) },
        { DataKey.IsAdsRemoved, new KeyInfo(typeof(bool), false) },
        { DataKey.CurrentLevel, new KeyInfo(typeof(int), 1) },
        { DataKey.CurrentScore, new KeyInfo(typeof(int), 0) },
        { DataKey.HighestScore, new KeyInfo(typeof(int), 0) },
        { DataKey.SoundEffectVolume, new KeyInfo(typeof(float), 1f) },
        { DataKey.BGMVolume, new KeyInfo(typeof(float), 1f) },
        { DataKey.IsHapticOn, new KeyInfo(typeof(bool), true) },
        { DataKey.GameTry, new KeyInfo(typeof(int), 0) }
    };

    private readonly Dictionary<string, object> dynamicDataCache = new Dictionary<string, object>();
    private readonly Dictionary<string, Type> dynamicKeyTypes = new Dictionary<string, Type>();

    private struct KeyInfo
    {
        public Type type;
        public object defaultValue;
        public KeyInfo(Type t, object def) { type = t; defaultValue = def; }
    }

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();
        LoadAllFixed();
    }

    // ========== 고정 키 API
    public void SetFixedValue<T>(DataKey key, T value)
    {
        var keyInfo = fixedKeyInfos[key];
        if (value?.GetType() != keyInfo.type)
            throw new Exception($"Type mismatch for {key}: expected {keyInfo.type}, got {value?.GetType()}");

        SaveFixed(key, value);
    }

    public T GetFixedValue<T>(DataKey key)
    {
        if (typeof(T) != fixedKeyInfos[key].type)
            throw new Exception($"Type mismatch for {key}: expected {fixedKeyInfos[key].type}, got {typeof(T)}");

        if (fixedDataCache.TryGetValue(key, out var cached)) return (T)cached;

        var loaded = LoadFixed(key);
        fixedDataCache[key] = loaded;
        return (T)loaded;
    }

    // ========== 동적 키 API
    public void SetDynamicValue<T>(string key, T value)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty");

        if (dynamicKeyTypes.TryGetValue(key, out var existingType) &&
            existingType != typeof(T))
        {
            throw new InvalidOperationException($"Type mismatch for {key}: existing {existingType}, new {typeof(T)}");
        }

        dynamicDataCache[key] = value;
        dynamicKeyTypes[key] = typeof(T);
        SaveDynamic(key, value);

        Debug.Log($"[UserData] Dynamic saved: {key} = {value}");
    }

    public T GetDynamicValue<T>(string key, T defaultValue = default(T))
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;

        // 1. 캐시 확인
        if (dynamicDataCache.TryGetValue(key, out var cached) && cached is T typed)
            return typed;

        // 2. PlayerPrefs에서 로드
        if (PlayerPrefs.HasKey($"Dynamic_{key}"))
        {
            var loaded = LoadDynamic(key, typeof(T), defaultValue);
            dynamicDataCache[key] = loaded;
            dynamicKeyTypes[key] = typeof(T);
            return (T)loaded;
        }

        // 3. 기본값 반환 및 캐시
        dynamicDataCache[key] = defaultValue;
        dynamicKeyTypes[key] = typeof(T);
        return defaultValue;
    }

    // 동적 키 삭제
    public void RemoveDynamicKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        string prefixedKey = $"Dynamic_{key}";
        dynamicDataCache.Remove(key);
        dynamicKeyTypes.Remove(key);
        PlayerPrefs.DeleteKey(prefixedKey);
        PlayerPrefs.Save();
        Debug.Log($"[UserData] Dynamic key removed: {key}");
    }

    public enum Operation { Add, Subtract, Multiply, Divide, Max, Min }
    public void OperateDynamic(string key, object operand, Operation op = Operation.Add)
    {
        if (operand is int intOperand)
            OperateDynamicInt(key, intOperand, op);
        else if (operand is long longOperand)
            OperateDynamicLong(key, longOperand, op);
        else if (operand is float floatOperand)
            OperateDynamicFloat(key, floatOperand, op);
        else
            Debug.LogError($"Unsupported operand type for {key}: {operand?.GetType()}");
    }

    private void OperateDynamicInt(string key, int operand, Operation op)
    {
        int current = GetDynamicValue<int>(key, 0);
        int result = op switch
        {
            Operation.Add => current + operand,
            Operation.Subtract => current - operand,
            Operation.Multiply => current * operand,
            Operation.Divide => operand != 0 ? current / operand : current,
            Operation.Max => Mathf.Max(current, operand),
            Operation.Min => Mathf.Min(current, operand),
            _ => current
        };
        SetDynamicValue(key, result);
    }

    private void OperateDynamicFloat(string key, float operand, Operation op)
    {
        float current = GetDynamicValue<float>(key, 0f);
        float result = op switch
        {
            Operation.Add => current + operand,
            Operation.Subtract => current - operand,
            Operation.Multiply => current * operand,
            Operation.Divide => operand != 0f ? current / operand : current,
            Operation.Max => Mathf.Max(current, operand),
            Operation.Min => Mathf.Min(current, operand),
            _ => current
        };
        SetDynamicValue(key, result);
    }

    private void OperateDynamicLong(string key, long operand, Operation op)
    {
        long current = GetDynamicValue<long>(key, 0);
        long result = op switch
        {
            Operation.Add => current + operand,
            Operation.Subtract => current - operand,
            Operation.Max => Math.Max(current, operand),
            Operation.Min => Math.Min(current, operand),
            _ => current  // Multiply/Divide는 long에서 피함
        };
        SetDynamicValue(key, result);
    }

    private void SaveFixed(DataKey key, object value)
    {
        string keyStr = key.ToString();
        var keyInfo = fixedKeyInfos[key];

        try
        {
            switch (keyInfo.type.Name)
            {
                case "Int32": PlayerPrefs.SetInt(keyStr, (int)value); break;
                case "Boolean": PlayerPrefs.SetInt(keyStr, (bool)value ? 1 : 0); break;
                case "Single": PlayerPrefs.SetFloat(keyStr, (float)value); break;
                case "String": PlayerPrefs.SetString(keyStr, (string)value); break;
                default: throw new Exception($"Unsupported fixed type: {keyInfo.type}");
            }
            PlayerPrefs.Save();
            fixedDataCache[key] = value;
        }
        catch (Exception ex) { Debug.LogError($"Fixed save failed: {key} - {ex.Message}"); }
    }

    private object LoadFixed(DataKey key)
    {
        string keyStr = key.ToString();
        var keyInfo = fixedKeyInfos[key];

        if (!PlayerPrefs.HasKey(keyStr)) return keyInfo.defaultValue;

        try
        {
            return keyInfo.type.Name switch
            {
                "Int32" => PlayerPrefs.GetInt(keyStr, (int)keyInfo.defaultValue),
                "Boolean" => PlayerPrefs.GetInt(keyStr, (bool)keyInfo.defaultValue ? 1 : 0) == 1,
                "Single" => PlayerPrefs.GetFloat(keyStr, (float)keyInfo.defaultValue),
                "String" => PlayerPrefs.GetString(keyStr, (string)keyInfo.defaultValue),
                _ => keyInfo.defaultValue
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Fixed load failed: {key} - {ex.Message}");
            return keyInfo.defaultValue;
        }
    }

    private void SaveDynamic(string key, object value)
    {
        string prefixedKey = $"Dynamic_{key}";
        Type type = value.GetType();

        try
        {
            switch (type.Name)
            {
                case "Int32": PlayerPrefs.SetInt(prefixedKey, (int)value); break;
                case "Int64": PlayerPrefs.SetString(prefixedKey, value.ToString()); break;
                case "Boolean": PlayerPrefs.SetInt(prefixedKey, (bool)value ? 1 : 0); break;
                case "Single": PlayerPrefs.SetFloat(prefixedKey, (float)value); break;
                case "String": PlayerPrefs.SetString(prefixedKey, (string)value); break;
                default: throw new Exception($"Unsupported dynamic type: {type}");
            }
            PlayerPrefs.Save();
        }
        catch (Exception ex) { Debug.LogError($"Dynamic save failed: {key} - {ex.Message}"); }
    }

    private object LoadDynamic(string key, Type expectedType, object defaultValue)
    {
        string prefixedKey = $"Dynamic_{key}";
        if (!PlayerPrefs.HasKey(prefixedKey)) return defaultValue;

        try
        {
            return expectedType.Name switch
            {
                "Int32" => PlayerPrefs.GetInt(prefixedKey, (int)defaultValue),
                "Int64" => GetLongFromPrefs(prefixedKey,(long)defaultValue),
                "Boolean" => PlayerPrefs.GetInt(prefixedKey, (bool)defaultValue ? 1 : 0) == 1,
                "Single" => PlayerPrefs.GetFloat(prefixedKey, (float)defaultValue),
                "String" => PlayerPrefs.GetString(prefixedKey, (string)defaultValue),
                _ => defaultValue
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Dynamic load failed: {key} - {ex.Message}");
            return defaultValue;
        }
    }

    private long GetLongFromPrefs(string prefixedKey, object defaultValue)
    {
        string savedStr = PlayerPrefs.GetString(prefixedKey, defaultValue.ToString());
        return long.TryParse(savedStr, out long parsedLong) ? parsedLong : (long)defaultValue;
    }

    private bool isLoadAll = false;
    public bool IsLoadAll { get => isLoadAll; set => isLoadAll = value; }

    private void LoadAllFixed()
    {
        foreach (var key in fixedKeyInfos.Keys)
            fixedDataCache[key] = LoadFixed(key);

        isLoadAll = true;
    }

    // 프로퍼티 래퍼 (고정키용)
    public class UserDataProperty<T>
    {
        private readonly DataKey key;

        public UserDataProperty(DataKey k) => key = k;

        public T Value
        {
            get => Instance.GetFixedValue<T>(key);
            set
            {
                T current = Instance.GetFixedValue<T>(key);
                if (EqualityComparer<T>.Default.Equals(current, value))
                    return;

                Instance.SetFixedValue(key, value);
                OnValueChanged?.Invoke(value);
            }
        }
        public event Action<T> OnValueChanged;
    }

    public UserDataProperty<bool> IsFirstOpen { get; } = new(DataKey.IsFirstOpen);
    public UserDataProperty<bool> IsAdsRemoved { get; } = new(DataKey.IsAdsRemoved);
    public UserDataProperty<int> CurrentLevel { get; } = new(DataKey.CurrentLevel);

    public UserDataProperty<int> CurrentScore { get; } = new(DataKey.CurrentScore);
    public UserDataProperty<int> HighestScore { get; } = new(DataKey.HighestScore);

    public UserDataProperty<float> SoundEffectVolume { get; } = new(DataKey.SoundEffectVolume);
    public UserDataProperty<float> BGMVolume { get; } = new(DataKey.BGMVolume);
    public UserDataProperty<bool> IsHapticOn { get; } = new(DataKey.IsHapticOn);
    public UserDataProperty<int> GameTry { get; } = new(DataKey.GameTry);

    public void PrintAllData()
    {
        Debug.Log("=== Fixed Data ===");
        foreach (var kvp in fixedDataCache)
            Debug.Log($"{kvp.Key}: {kvp.Value}");

        Debug.Log("=== Dynamic Data Cache ===");
        foreach (var kvp in dynamicDataCache)
            Debug.Log($"{kvp.Key}: {kvp.Value}");
    }

    public void ClearAllData()
    {
        foreach (var key in fixedKeyInfos.Keys)
            PlayerPrefs.DeleteKey(key.ToString());

        foreach (var key in dynamicDataCache.Keys)
            PlayerPrefs.DeleteKey($"Dynamic_{key}");

        fixedDataCache.Clear();
        dynamicDataCache.Clear();
        dynamicKeyTypes.Clear();

        LoadAllFixed();

        PlayerPrefs.Save();
        Debug.Log("[UserData] All data cleared successfully");
    }

}