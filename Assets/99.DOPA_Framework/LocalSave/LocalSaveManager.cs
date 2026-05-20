using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using UniRx;
using System.Threading;




#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class ILocalSave
{
}

#region DATA CLASS

public class LocalSaveCurrency : ILocalSave
{
    [JsonProperty] Dictionary<string, long> currencies = new();

    public bool HasCurrency(string id)
    {
        return currencies.ContainsKey(id);
    }

    public long GetCurrency(string id)
    {
        if (currencies.TryGetValue(id, out long count))
        {
            return count;
        }
        return 0;
    }

    public void SetCurrency(string id, long count)
    {
        if (currencies.ContainsKey(id))
        {
            currencies[id] = count;
        }
        else
        {
            currencies.Add(id, count);
        }

        LocalSaveManager.Instance.OnChangeData(this);
    }
}

public class LocalSavePurchase : ILocalSave
{
    [JsonProperty] Dictionary<string, uint> purchaseCounts = new();

    public uint GetPurchaseCount(string productID)
    {
        if (purchaseCounts.TryGetValue(productID, out uint count))
        {
            return count;
        }

        return 0;
    }

    public void SetPurchaseCount(string productID, uint count)
    {
        if (purchaseCounts.ContainsKey(productID))
        {
            purchaseCounts[productID] = count;
        }
        else
        {
            purchaseCounts.Add(productID, count);
        }

        LocalSaveManager.Instance.OnChangeData(this);
    }

    public void AddPurchaseCount(string productID)
    {
        if (purchaseCounts.ContainsKey(productID))
        {
            purchaseCounts[productID] += 1;
        }
        else
        {
            purchaseCounts.Add(productID, 1);
        }

        LocalSaveManager.Instance.OnChangeData(this);
    }
}
#endregion DATA CLASS

public class LocalSaveManager : BaseSystemManager<LocalSaveManager>
{
    private static readonly byte[] CRYPTO_KEY = Convert.FromBase64String("8/lEKNAr/w3rVNk5LKWhUsha+ssdLbHishaEkHsxilU=");

    private readonly Subject<Unit> _saveRequestSubject = new Subject<Unit>();
    private readonly Subject<Unit> _quitSubject = new Subject<Unit>();
    IDisposable saveDisposable;
    bool isSaving = false;
    bool pendingSave;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    HashSet<ILocalSave> changedValueClass = new HashSet<ILocalSave>();
    HashSet<ILocalSave> allDataClasses = new HashSet<ILocalSave>();

    private LocalSaveCurrency currency;
    public static LocalSaveCurrency Currency => Instance != null ? Instance.currency : null;
    private LocalSavePurchase purchase;
    public static LocalSavePurchase Purchase => Instance != null ? Instance.purchase : null;


    protected override void CompleteInitialization()
    {
        LoadAll().Forget();
    }

    private async UniTaskVoid LoadAll()
    {
        await LoadAll(false);

        base.CompleteInitialization();
    }

    /// <summary>
    /// DB 데이터로 로드 및 펜딩 데이터 탐색 후 있으면 데이터 선택.
    /// </summary>
    /// <returns></returns>
    public async Task LoadAll(bool dbFirst)
    {
        foreach (var file in Directory.GetFiles(Application.persistentDataPath, "*.tmp"))
        {
            File.Delete(file);
        }

        allDataClasses.Clear();

        purchase = await SetData<LocalSavePurchase>(dbFirst);

        //Pending Data Check
        // var pendingPlayerItem = await LoadAsync<LocalSavePlayerItem>(true);
        // if (pendingPlayerItem != null && File.Exists(GetFilePath<LocalSavePlayerItem>(true)))
        // {
        //     var cloudLastDateByte = await DbStoreManager.Instance.LoadDataAsync("LastSaveDate");
        //     if (cloudLastDateByte == null)
        //     {
        //         SetSaveTimer();
        //         return;
        //     }

        //     var cloudLastDate = Encoding.UTF8.GetString(cloudLastDateByte);

        //     bool isShow = true;

        //     PopupManager.Instance.ShowPopup<PopupSaveData>("", "", (popup) =>
        //     {
        //         popup.SetData(pendingPlayerItem.GetItemCount(GameDataManager.Money).ToCompact(),
        //             pendingPlayerItem.GetItemCount(GameDataManager.Coin).ToCompact(),
        //             cloudLastDate,
        //             playerItem.GetItemCount(GameDataManager.Money).ToCompact(),
        //             playerItem.GetItemCount(GameDataManager.Coin).ToCompact(), async (result) =>
        //             {
        //                 await SelectData(result);

        //                 isShow = false;
        //             });
        //     });

        //     await UniTask.WaitUntil(() => isShow == false);
        // }
        // else
        // {
        //     SetSaveTimer();
        // }
    }


    private void SetSaveTimer()
    {
        saveDisposable?.Dispose();

        // 1. Local Save (10초)
        var autoSaveStream = _saveRequestSubject
            .ThrottleFirst(TimeSpan.FromSeconds(10))
            .Select(_ => false);

        // 2. Cloud Save (60초)
        var cloudSaveStream = _saveRequestSubject
            .ThrottleFirst(TimeSpan.FromSeconds(60))
            .Select(_ => true);

        // 3. Pause → 무조건 Cloud
        var pauseSaveStream = Observable.EveryApplicationPause()
            .Where(isPaused => isPaused)
            .Select(_ => false);

        // 4. Quit → 무조건 Cloud
        var quitSaveStream = _quitSubject
            .Select(_ => false);

        // 5. Merge + async 처리
        saveDisposable = Observable.Merge(
                autoSaveStream,
                cloudSaveStream,
                pauseSaveStream,
                quitSaveStream
            )
            .Subscribe(saveToCloud =>
            {
                ExecuteSave(saveToCloud).Forget();
            });
    }

    public async Task ReloadGame()
    {
        Dispose();

        var handler = SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);

        await handler;

        InitializeManager.Instance.StartBootstrap();
    }

    public void Dispose()
    {
        saveDisposable?.Dispose();
        saveDisposable = null;
    }

    public void SaveRequest(bool forceImmediate = false)
    {
        if (forceImmediate)
        {
            ExecuteSave(true).Forget(); // 즉시 실행 (cloud 포함)
        }
        else
        {
            _saveRequestSubject.OnNext(Unit.Default);
        }
    }

    // 유니티 기본 메시지로 종료 감지 (가장 확실함)
    // 모바일 pause/focus 시에도 호출됨
    public void OnExcuteQuit()
    {
        _quitSubject.OnNext(Unit.Default);
    }

    private async UniTask ExecuteSave(bool saveToCloud)
    {
        if (isSaving)
        {
            pendingSave = true;
            return;
        }

        isSaving = true;

        try
        {
            await SaveChangedDataAsync(saveToCloud);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            isSaving = false;

            bool needCloud = saveToCloud;

            while (pendingSave)
            {
                pendingSave = false;
                needCloud = true;
                await SaveChangedDataAsync(needCloud);
            }
        }
    }


    public async UniTask SelectData(bool dbData)
    {
        try
        {
            if (dbData)
            {
                foreach (var c in allDataClasses)
                {
                    string path = GetFilePath(c.GetType(), true);
                    if (File.Exists(path))
                        File.Delete(path);
                }

                // PopupManager.Instance.ClosePopup<PopupAlert>();

                try
                {
                    await SaveAll(false, false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);

                    // PopupManager.Instance.ShowPopup<PopupAlert>("Error", "Data Save Failure", (popup) =>
                    //    {
                    //        popup.SetButtons("Confirm", popup.Close, null, null);
                    //    });
                    return;
                }
            }
            else
            {
                try
                {
                    allDataClasses.Clear();

                    allDataClasses.Add(purchase = await LoadAsync<LocalSavePurchase>(true));
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);

                    // PopupManager.Instance.ShowPopup<PopupAlert>("Error", "Data Change Failure", (popup) =>
                    // {
                    //     popup.SetButtons("Confirm", popup.Close, null, null);
                    // });
                    return;
                }

                // PopupManager.Instance.ClosePopup<PopupAlert>();

                try
                {
                    await SaveAll(true, false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);

                    // PopupManager.Instance.ShowPopup<PopupAlert>("Error", "Data Save Failure", (popup) =>
                    //    {
                    //        popup.SetButtons("Confirm", popup.Close, null, null);
                    //    });
                    return;
                }

                foreach (var c in allDataClasses)
                {
                    string path = GetFilePath(c.GetType(), true);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }

            await ReloadGame();
        }
        catch (Exception e)
        {
            Debug.Log($"[LocalSaveManager] SelectData " + e.Message);
        }
    }

    public async Task<T> SetData<T>(bool dbFirst) where T : ILocalSave, new()
    {
        T data;

        if (dbFirst)
        {
            data = await LoadDBStoreData<T>(typeof(T).Name);
            data ??= await LoadAsync<T>();
        }
        else
        {
            data = await LoadAsync<T>();
            if (data == default)
            {
                data = await LoadDBStoreData<T>(typeof(T).Name);
            }
        }

        data ??= new T();

        allDataClasses.Add(data);

        return data;
    }


    public async Task<T> LoadDBStoreData<T>(string fileName) where T : ILocalSave
    {
#if !UCS_NO
        byte[] ucs = await DbStoreManager.Instance.LoadDataAsync(fileName);
        if (ucs != null)
        {
            byte[] decryptedBytes = CryptoUtility.Decrypt(ucs, CRYPTO_KEY);

            string str = Encoding.UTF8.GetString(decryptedBytes);

            return ConvertToData<T>(str);
        }
#endif

        return null;
    }

    public void OnChangeData<T>(T save) where T : ILocalSave
    {
        lock (changedValueClass)
        {
            changedValueClass.Add(save);
        }

        SaveRequest(true);
    }

    public async Task SaveAll(bool dbSave, bool pendingData)
    {
        foreach (var c in allDataClasses)
        {
            await SaveAsync(c, dbSave, pendingData);
        }
    }

    public async UniTask SaveChangedDataAsync(bool saveDB)
    {
        await SaveAll(saveDB, false);
    }
    public async Task<T> LoadAsync<T>(bool pendingData = false) where T : ILocalSave, new()
    {
        string path = GetFilePath<T>(pendingData);
        string backupPath = path + ".bak";

        // 1. 정상 파일 시도
        if (File.Exists(path))
        {
            try
            {
                byte[] encryptedBytes = await File.ReadAllBytesAsync(path);
                byte[] decryptedBytes = CryptoUtility.Decrypt(encryptedBytes, CRYPTO_KEY);

                string data = Encoding.UTF8.GetString(decryptedBytes);
                var result = ConvertToData<T>(data);
                if (result != null)
                    return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Main load failed: {e.Message}");
            }
        }

        // 2. backup 복구 시도
        if (File.Exists(backupPath))
        {
            try
            {
                byte[] encryptedBytes = await File.ReadAllBytesAsync(backupPath);
                byte[] decryptedBytes = CryptoUtility.Decrypt(encryptedBytes, CRYPTO_KEY);

                string data = Encoding.UTF8.GetString(decryptedBytes);
                var result = ConvertToData<T>(data);
                if (result != null)
                {
                    Debug.LogWarning("Backup data restored");
                    return result;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Backup load failed: {e.Message}");
            }
        }

        return default;
    }

    public T ConvertToData<T>(string utf8Data) where T : ILocalSave
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(utf8Data);
        }
        catch (Exception e)
        {
            Debug.LogError("ConvertToData e:" + e.Message);
        }

        return default;
    }

    public void Delete<T>() where T : ILocalSave, new()
    {
        if (Directory.Exists(Application.persistentDataPath))
        {
            string json = JsonConvert.SerializeObject(new T());

            FileStream fs = new FileStream(GetFilePath<T>(), FileMode.Create);
            byte[] data = Encoding.UTF8.GetBytes(json);
            fs.Write(data, 0, data.Length);
            fs.Close();
        }
    }

    public async UniTask SaveAsync<T>(T data, bool dbSave = false, bool pendingData = false) where T : ILocalSave
    {
        await _saveLock.WaitAsync();

        try
        {
            string path = GetFilePath(data.GetType(), pendingData);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[Save] 실패: path가 null이거나 비어있음 (Application.persistentDataPath 무효 상태일 수 있음)");
                return;
            }
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

            string directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            byte[] dataBytes = await UniTask.RunOnThreadPool(() => GetSaveDataToByte(data));
            byte[] encrypted = await UniTask.RunOnThreadPool(() => CryptoUtility.Encrypt(dataBytes, CRYPTO_KEY));

            // 1. tmp 파일에 먼저 저장
            await File.WriteAllBytesAsync(tempPath, encrypted);

            // 2. 기존 파일 백업
            if (File.Exists(path))
            {
                File.Copy(path, backupPath, true);
                File.Delete(path);
            }

            // 3. tmp → 실제 파일 교체 (원자적)
            File.Move(tempPath, path);

            // 4. backup 삭제 (선택)
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            // 5. DB 저장
            if (dbSave)
            {
#if !UCS_NO
                DateTime dateTime = DateTime.UtcNow;
                string date = $"{dateTime.Year}-{dateTime.Month}-{dateTime.Day}";

                await DbStoreManager.Instance.SaveDataAsync(data.GetType().Name, encrypted);
                await DbStoreManager.Instance.SaveDataAsync("LastSaveDate", Encoding.UTF8.GetBytes(date));
#endif
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] 실패: {e.Message}");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    static public byte[] GetSaveDataToByte<T>(T data) where T : ILocalSave
    {
        string json = JsonConvert.SerializeObject(data);

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return bytes;
    }

    static public string GetFilePath<T>(bool pendingData = false) where T : ILocalSave
    {
        return GetFilePath(typeof(T), pendingData);
    }

    static public string GetFilePath(Type type, bool pendingData = false)
    {
        if (!typeof(ILocalSave).IsAssignableFrom(type))
        {
            Debug.LogError($"{type.Name}은 ILocalSave를 상속받지 않았습니다.");
            return null;
        }

        string tail = pendingData ? "_PENDING" : "";

        return Path.Combine(Application.persistentDataPath, $"{type.Name}{tail}.bin");
    }


#if UNITY_EDITOR
    [MenuItem("Custom/Delete Data")]
    public static void DeleteAll()
    {
        PlayerPrefs.DeleteAll();

        Directory.Delete($"{Application.persistentDataPath}", true);
    }
#endif
}

[Serializable]
public struct ItemQuantity
{
    public uint id;
    public uint count;

    public ItemQuantity(uint id, uint count)
    {
        this.id = id;
        this.count = count;
    }
}

