using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

public class GaugeBarBase : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] protected Image fillImage;

    [Header("Gauge Settings")]
    [SerializeField] protected float maxValue = 100f;
    protected float minValue = 0f;
    [SerializeField] protected float currentValue = 0f;



    public Color startColor;
    public Color endColor;


    [Header("Gauge Events")]
    public UnityEvent OnGaugeMaxReached;
    public UnityEvent OnGaugeMinReached;

    protected bool hasReachedMax = false;
    protected bool hasReachedMin = false;

    [SerializeField] Image icon;
    [SerializeField] protected bool isGaugeMaxChangeColor = false;
    [SerializeField] protected bool isNested = false;

    public Sprite Icon { get => icon.sprite; set => icon.sprite = value; }

    public Vector3 IconPos { get => icon.gameObject.transform.position; set => icon.gameObject.transform.position = value; }

    protected void Start()
    {
        UpdateGaugeUI();
    }

    public virtual void Init(float maxInitValue = 100.0f)
    {
        GaugeLevel = 0;
        SetGaugeColorLevel();
        SetGauge(0);
        currentValue = minValue;
        maxValue = maxInitValue;
    }

    public virtual void SetGauge(float value)
    {
        SetGaugeColorLevel();

        currentValue = value;
        if (currentValue > maxValue) { currentValue = maxValue; }

        //currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
        UpdateGaugeUI();
        CheckGaugeEvents();
    }

    public virtual void SetColorGradient(Color startC, Color endC)
    {
        startColor = startC;
        endColor = endC;
    }


    public void SetGaugePercent(float value = 0.0f)
    {
        Debug.Log($"SetGaugePercent : {value * 100}%");


        SetGauge(maxValue * value);
    }

    public void SetAddGauge(float value)
    {
        SetGauge(currentValue + value);
    }

    public void SetAddGaugePercent(float value = 0.1f)
    {
        SetGauge(currentValue + maxValue * value);
    }

    public float GetCurrentValue()
    {
        return fillImage.fillAmount;
    }



    public void LerpGauge(float targetValue, float duration)
    {
        StartCoroutine(LerpGaugeCoroutine(targetValue, duration));
    }

    protected IEnumerator LerpGaugeCoroutine(float targetValue, float duration)
    {
        float startValue = currentValue;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            SetGauge(Mathf.Lerp(startValue, targetValue, elapsedTime / duration));
            yield return null;
        }

        SetGauge(targetValue);
    }

    protected void UpdateGaugeUI()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = (currentValue - minValue) / (maxValue - minValue);

            if (isGaugeMaxChangeColor && colorList.Count > 0)
            {
                SetGaugeColorLevel();
            }
            else if (startColor != default(Color) && endColor != default(Color))
            {
                fillImage.color = Color.Lerp(startColor, endColor, currentValue / maxValue);
            }
        }
    }

    protected void CheckGaugeEvents()
    {
        if (currentValue >= maxValue)
        {
            if (!hasReachedMax)
            {
                OnGaugeMaxEvent();
            }
        }
        //else if (currentValue <= minValue)
        //{
        //    if (!hasReachedMin)
        //    {
        //        OnGaugeMinEvent();
        //    }
        //}
    }

    public void ResetGauge()
    {
        currentValue = minValue;
        hasReachedMax = false;
        hasReachedMin = false;

        UpdateGaugeUI();
    }

    [SerializeField] protected List<Color> colorList = new();
    protected int gaugeLevel = 0;

    public int GaugeLevel { get => gaugeLevel; set => gaugeLevel = value; }

    public void SetGaugeColor(Color color)
    {
        if (fillImage != null)
        {
            fillImage.color = color;
        }
    }

    public void SetGaugeColorLevel()
    {
        if (!isGaugeMaxChangeColor) return;

        if (fillImage != null)
        {
            int colorListCnt = colorList.Count;
            fillImage.color = GaugeLevel < colorListCnt ? colorList[GaugeLevel] : colorList[colorListCnt - 1];
        }
    }


    protected virtual void OnGaugeMaxEvent()
    {
        hasReachedMax = true;
        OnGaugeMaxReached?.Invoke();

        GaugeLevel++;

        if (isNested)
        {
            SetGaugeColorLevel();
            ResetGauge();
        }
    }

    protected void OnGaugeMinEvent()
    {
        hasReachedMin = true;

        OnGaugeMinReached?.Invoke();
    }
}
