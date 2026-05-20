using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.AddressableAssets;
[Serializable]
public struct Monster_form
{
    public uint id;
    public string name_code;
    public float move_speed;
    public float atk_interval;
    public float atk;
    public float hp;
}
[Serializable]
public class MonsterTable : IDataManager<uint, Monster_form>
{
    private List<Monster_form> _data = new();
    private Dictionary<uint, Monster_form> _dataDic = new();
    public string TableName => "Monster";
    private static byte[] _encryptionKey = 
            Convert.FromBase64String("5/lEYNAr/m3rVNk5LKWHUsha+ssdLbHishaEkHsxilU=");
    public async Task LoadDataAsync(Action<float> onProgress)
    {
    TextAsset binAsset = await Addressables.LoadAssetAsync<TextAsset>("Tables/Monster.bytes").Task;
    byte[] encrypted = binAsset.bytes;
    byte[] decrypted = CryptoUtility.Decrypt(binAsset.bytes, _encryptionKey);
    string json = GZipCompression.Decompress(decrypted);
    _data = JsonConvert.DeserializeObject<List<Monster_form>>(json);
    BuildDictionary();
    onProgress?.Invoke(1);
    }
    private async Task LoadEditorData(string path, Action<float> onProgress)
    {
        try
        {
            await Task.Yield();
            byte[] encrypted = File.ReadAllBytes(path);
            byte[] decrypted = CryptoUtility.Decrypt(encrypted, _encryptionKey);
            string json = GZipCompression.Decompress(decrypted);
            _data = JsonConvert.DeserializeObject<List<Monster_form>>(json);
            BuildDictionary();
            onProgress?.Invoke(1f);
        }
        catch (Exception e)
        {
            TableErrorHandler.HandleError(TableName, e);
        }
    }
    private async Task LoadAndroidData(string path, Action<float> onProgress)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(path))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                onProgress?.Invoke(request.downloadProgress);
                await Task.Delay(100);
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] encrypted = request.downloadHandler.data;
                byte[] decrypted = CryptoUtility.Decrypt(encrypted, _encryptionKey);
                string json = GZipCompression.Decompress(decrypted);
                _data = JsonConvert.DeserializeObject<List<Monster_form>>(json);
                BuildDictionary();
                onProgress?.Invoke(1f);
            }
            else
            {
                TableErrorHandler.HandleError(TableName, 
                    new Exception($"Network error: {request.error}"));
            }
        }
    }
    private void BuildDictionary()
    {
        _dataDic.Clear();
        foreach (var item in _data)
        {
            try
            {
                _dataDic.Add(item.id, item);
            }
            catch (ArgumentException)
            {
                Debug.LogError($"중복 키 발견: Monster - item : id {item.id}");
            }
        }
    }
    public Monster_form GetData(uint key)
    {
        if (_dataDic.TryGetValue(key, out var value))
            return value;

        Debug.LogWarning($" Data X : {key}");
        return default;
    }
    public bool TryGetData(uint key, out Monster_form value)
    {
        return _dataDic.TryGetValue(key, out value);
    }
    
    public Dictionary<uint, Monster_form> GetDatas() => _dataDic;
}
