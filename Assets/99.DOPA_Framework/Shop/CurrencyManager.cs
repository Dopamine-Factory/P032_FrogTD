using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CurrencyManager : Singleton<CurrencyManager>
{
    #region Events
    [System.Serializable]
    public class CurrencyEvent : UnityEvent<string, long> { }
    public CurrencyEvent OnCurrencyChanged = new CurrencyEvent();
    public UnityEvent<string> OnCurrencyMaxReached = new UnityEvent<string>();
    #endregion

    #region Data
    [Header("Currency Settings")]
    [SerializeField] private List<Currency> _currencies = new List<Currency>();
    #endregion

    #region Lifecycle

    protected override void Awake()
    {
        Debug.Log("==============CurrencyManager Awake====================");

        base.Awake();
    }

    protected override void OnSingletonInitialized()
    {
        BroadcastAllCurrencies();

        base.OnSingletonInitialized();
    }

    #endregion


    #region Currency Operations
    public bool HasCurrency(string currencyID)
    {
        return LocalSaveManager.Currency.HasCurrency(currencyID);
    }

    public bool EnoughCurrency(string currencyID, long requiredAmount)
    {
        long hasCount = GetCurrencyAmount(currencyID);

        return hasCount >= requiredAmount;
    }

    public bool SpendCurrency(string currencyID, long amount)
    {
        Currency currency = GetCurrency(currencyID);
        if (currency == null)
        {
            Debug.Log($"Currency not found: {currencyID}");
            return false;
        }

        if (!currency.canGoNegative && !EnoughCurrency(currencyID, amount))
        {
            Debug.Log($"Insufficient funds: {currencyID}");
            return false;
        }

        LocalSaveManager.Currency.SetCurrency(currencyID, GetCurrencyAmount(currencyID) - amount);

        OnCurrencyChanged?.Invoke(currencyID, GetCurrencyAmount(currencyID));

        return true;
    }

    public bool AddCurrency(string currencyID, long amount)
    {
        Currency currency = GetCurrency(currencyID);
        if (currency == null)
        {
            Debug.Log($"Currency not found: {currencyID}");
            return false;
        }

        long newAmount = Math.Min(GetCurrencyAmount(currencyID) + amount, currency.maxAmount);
        bool reachedMax = (newAmount >= currency.maxAmount);

        LocalSaveManager.Currency.SetCurrency(currencyID, newAmount);

        OnCurrencyChanged?.Invoke(currencyID, GetCurrencyAmount(currencyID));

        if (reachedMax) OnCurrencyMaxReached?.Invoke(currencyID);

        return true;
    }

    public long GetCurrencyAmount(string currencyID)
    {
        return LocalSaveManager.Currency.GetCurrency(currencyID);
    }

    public Currency GetCurrency(string currencyID)
    {
        return _currencies.Find(x => x.currencyID == currencyID);
    }
    #endregion

    #region Persistence

    private void BroadcastAllCurrencies()
    {
        LocalSaveCurrency saveCurrency = LocalSaveManager.Currency;
        if (saveCurrency == null) return;

        foreach (var kvp in _currencies)
        {
            OnCurrencyChanged?.Invoke(kvp.currencyID, saveCurrency.GetCurrency(kvp.currencyID));
        }
    }

    #endregion
}

[System.Serializable]
public class Currency
{
    [Header("Identification")]
    public string currencyID;
    public string displayName;
    public CurrencyType type;
    public Sprite icon;

    [Header("Values")]
    public long initialAmount = 0;
    public long maxAmount = long.MaxValue;
    public bool canGoNegative = false;

    [Header("UI Settings")]
    public string formatString = "N0";

    public Currency(string id, string name, CurrencyType currencyType)
    {
        currencyID = id;
        displayName = name;
        type = currencyType;
    }
}

public enum CurrencyType : ushort
{
    Free, Premium, Energy, Special
}