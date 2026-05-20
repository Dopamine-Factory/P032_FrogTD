using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BuffUI : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private Transform buffContainer;
    [SerializeField] private GameObject buffIconPrefab;

    private Dictionary<BuffType, BuffIcon> activeIcons = new Dictionary<BuffType, BuffIcon>();

    private void Awake()
    {
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.OnBuffChanged -= OnBuffChanged;
            BuffManager.Instance.OnBuffChanged += OnBuffChanged;
        }
    }

    private void Update()
    {
        foreach (var pair in activeIcons)
        {
            var type = pair.Key;
            var icon = pair.Value;
            var data = BuffManager.Instance.GetBuffData(type);
            if (data != null)
                icon.UpdateStatus(data.RemainTime);
        }
    }

    private void OnBuffChanged(BuffType type, BuffData data)
    {
        if (data == null)
        {
            RemoveIcon(type);
            return;
        }

        if (!activeIcons.ContainsKey(type))
        {
            CreateIcon(type, data);
        }
        UpdateIcon(type, data);
    }

    private void CreateIcon(BuffType type, BuffData data)
    {
        var iconObj = Instantiate(buffIconPrefab, buffContainer);
        var icon = iconObj.GetComponent<BuffIcon>();
        icon.Initialize(
            icon: BuffManager.Instance.GetBuffIcon(type),
            maxDuration: data.MaxDuration
        );
        activeIcons.Add(type, icon);
    }

    private void UpdateIcon(BuffType type, BuffData data)
    {
        if (activeIcons.TryGetValue(type, out var icon))
        {
            icon.UpdateStatus(data.RemainTime);
        }
    }

    private void RemoveIcon(BuffType type)
    {
        if (activeIcons.TryGetValue(type, out var icon))
        {
            Destroy(icon.gameObject);
            activeIcons.Remove(type);
        }
    }


    private void OnDestroy()
    {
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.OnBuffChanged -= OnBuffChanged;
        }
    }
}