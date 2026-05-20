using TMPro;
using UnityEngine;

public class GaugeBarTextUI : GaugeBarBase
{
    [Header("Gauge Text Settings")]
    [SerializeField] protected TextMeshProUGUI maxText;
    [SerializeField] protected TextMeshProUGUI currentText;

    [SerializeField] protected GameObject maxEffect;

    public override void Init(float maxInitValue = 100)
    {
        Debug.Log("GaugeBarTextUI Init");

        base.Init(maxInitValue);

        maxEffect.SetActive(false);
        
        maxText.text = ((int)maxInitValue).ToString();
        currentText.text = ((int)currentValue).ToString();
    }

    public override void SetGauge(float value)
    {
        // Debug.Log("GaugeBarTextUI SetGauge");

        SetGaugeColorLevel();

        currentValue = value;

        if (currentValue > maxValue) 
        { 
            currentValue = maxValue; 
        }

        currentText.text = ((int)currentValue).ToString();

        UpdateGaugeUI();
        CheckGaugeEvents();
    }


    protected override void OnGaugeMaxEvent()
    {
        Debug.Log("GaugeBarTextUI OnGaugeMaxEvent");
        maxEffect.SetActive(true);

        SoundManager.Instance.PlayEffect("EFFECT_01");
        
        hasReachedMax = true;
        OnGaugeMaxReached?.Invoke();

        GaugeLevel++;

        if(isNested)
        {
            SetGaugeColorLevel();
        }
    }
}
