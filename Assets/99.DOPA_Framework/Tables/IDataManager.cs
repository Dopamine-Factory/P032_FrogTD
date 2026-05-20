
using System;
using System.Threading.Tasks;

public interface IDataManager
{
    string TableName { get; }
    Task LoadDataAsync(Action<float> onProgress);
}

public interface IDataManager<TKey, TValue> : IDataManager
{
    TValue GetData(TKey key);
    bool TryGetData(TKey key, out TValue value);
}