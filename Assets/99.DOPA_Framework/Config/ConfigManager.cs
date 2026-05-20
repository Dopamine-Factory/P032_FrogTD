using System;
using System.Collections.Generic;
using UnityEngine;

public class ConfigManager : Singleton<ConfigManager>
{
    public enum ConfigKey
    {
        HeartFull,
        HeartChargeSec,
        TutorialLobbySkipStage,
        AdRemoveItemType,
        InterstitialAdOn,
        IntistialShowSecond,
        IntistialShowLevel,
        IntistialIntervalSecond,
        IntistialIntervalLevel
    }

    private struct ConfigValueInfo
    {
        public Type ValueType;
        public object DefaultValue;
        public string Description;

        public ConfigValueInfo(Type type, object defaultVal, string desc = "")
        {
            ValueType = type;
            DefaultValue = defaultVal;
            Description = desc;
        }
    }

    private readonly Dictionary<ConfigKey, ConfigValueInfo> configSchema = new()
    {
        { ConfigKey.HeartFull, new ConfigValueInfo(typeof(ushort), (ushort)5, "최대 하트 수") },
        { ConfigKey.HeartChargeSec, new ConfigValueInfo(typeof(ushort), (ushort)1800, "하트 충전 시간(초)") },
        { ConfigKey.TutorialLobbySkipStage, new ConfigValueInfo(typeof(ushort), (ushort)2, "튜토리얼 로비 스킵 스테이지 번호") },
        { ConfigKey.AdRemoveItemType, new ConfigValueInfo(typeof(int), 0, "광고제거 상품 타입") },
        { ConfigKey.InterstitialAdOn, new ConfigValueInfo(typeof(bool), true, "전면 광고 온오프") },
        { ConfigKey.IntistialShowSecond, new ConfigValueInfo(typeof(int), 10, "전면 광고 오픈 시간(초)") },
        { ConfigKey.IntistialShowLevel, new ConfigValueInfo(typeof(int), 2, "전면 광고 오픈 레벨") },
        { ConfigKey.IntistialIntervalSecond, new ConfigValueInfo(typeof(int), 5, "전면 광고 인터벌 시간(초)") },
        { ConfigKey.IntistialIntervalLevel, new ConfigValueInfo(typeof(int), 1, "전면 광고 오픈 레벨") },
    };

    private readonly Dictionary<ConfigKey, object> configData = new();

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();
        LoadDefaults();
    }

    private void LoadDefaults()
    {
        foreach (var kvp in configSchema)
            configData[kvp.Key] = kvp.Value.DefaultValue;
    }

    public T Get<T>(ConfigKey key)
    {
        if (!configSchema.ContainsKey(key))
            throw new ArgumentException($"Unknown config key {key}");
        if (configSchema[key].ValueType != typeof(T))
            throw new InvalidCastException($"Config key {key} expects type {configSchema[key].ValueType}, but requested type was {typeof(T)}");

        if (configData.TryGetValue(key, out var val))
            return (T)val;
        else
            return (T)configSchema[key].DefaultValue;
    }

    public void Set<T>(ConfigKey key, T value)
    {
        if (!configSchema.ContainsKey(key))
            throw new ArgumentException($"Unknown config key {key}");
        if (configSchema[key].ValueType != typeof(T))
            throw new InvalidCastException($"Config key {key} expects type {configSchema[key].ValueType}, but given type was {typeof(T)}");

        configData[key] = value;
    }

    public class ConfigProperty<T>
    {
        private readonly ConfigKey key;

        public ConfigProperty(ConfigKey key)
        {
            this.key = key;
        }

        public T Value
        {
            get => Instance.Get<T>(key);
            set => Instance.Set(key, value);
        }
    }

    public static readonly ConfigProperty<ushort> HeartFull = new(ConfigKey.HeartFull);
    public static readonly ConfigProperty<ushort> HeartChargeSec = new(ConfigKey.HeartChargeSec);
    public static readonly ConfigProperty<ushort> TutorialLobbySkipStage = new(ConfigKey.TutorialLobbySkipStage);
    public static readonly ConfigProperty<int> AdRemoveItemType = new(ConfigKey.AdRemoveItemType);
    public static readonly ConfigProperty<bool> InterstitialAdOn = new(ConfigKey.InterstitialAdOn);
    public static readonly ConfigProperty<int> IntistialShowSecond = new(ConfigKey.IntistialShowSecond);
    public static readonly ConfigProperty<int> IntistialShowLevel = new(ConfigKey.IntistialShowLevel);
    public static readonly ConfigProperty<int> IntistialIntervalSecond = new(ConfigKey.IntistialIntervalSecond);
    public static readonly ConfigProperty<int> IntistialIntervalLevel = new(ConfigKey.IntistialIntervalLevel);
}