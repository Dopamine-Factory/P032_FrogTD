using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Random = UnityEngine.Random;

public class SoundManager : MonoBehaviour
{
    #region Singleton
    public static SoundManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Configuration
    [Header("Pool Settings")]
    [SerializeField] private int effectPoolSize = 20;

    [Header("Volume")]
    [SerializeField] private float masterVolume = 1f;

    [Header("Addressables Labels")]
    [SerializeField] private string sfxLabel = "SoundSFX";
    [SerializeField] private string bgmLabel = "SoundBGM";

    [Header("Alias Collision Policy")]
    [SerializeField] private bool throwOnAliasCollisionInDevelopmentBuild = true;

    [Header("Preload")]
    [SerializeField] private bool preloadCommonSFX = true;
    [SerializeField] private string[] preloadSFXKeys = { "Click", "Select" };

    [Header("Sound Info (Optional Override)")]
    [SerializeField] private SoundInfo[] soundInfos;
    #endregion

    #region Data Structures
    [Serializable]
    public class SoundInfo
    {
        public SoundType type;
        public string category = "SFX";
        public string[] soundKeys;
        [Range(0f, 2f)] public float baseVolume = 1f;
        [Range(0.5f, 2f)] public float pitchRange = 0.2f;
        public int maxConcurrent = 3;
    }

    public enum SoundType
    {
        Click,
        Select,
        GainCoin
    }

    [Serializable]
    public struct PlayOption
    {
        [Range(0f, 2f)] public float volume;
        [Range(0.5f, 2f)] public float pitch;
        [Range(0f, 1f)] public float pitchRange;
        public float delay;
        public bool is3D;
        public Transform followTarget;

        public static PlayOption Default => new PlayOption
        {
            volume = 1f,
            pitch = 1f,
            pitchRange = 0.1f,
            delay = 0f,
            is3D = false,
            followTarget = null
        };

        public static PlayOption Quiet => new PlayOption
        {
            volume = 0.3f,
            pitch = 1f,
            pitchRange = 0.1f,
            delay = 0f,
            is3D = false,
            followTarget = null
        };

        public static PlayOption Loud => new PlayOption
        {
            volume = 1.4f,
            pitch = 1f,
            pitchRange = 0.15f,
            delay = 0f,
            is3D = false,
            followTarget = null
        };
    }

    private sealed class RuntimeSoundInfo
    {
        public SoundType Type
        {
            get;
        }

        public float BaseVolume
        {
            get;
        }

        public float PitchRange
        {
            get;
        }

        public int MaxConcurrent
        {
            get;
        }

        public IReadOnlyList<string> PrimaryKeys
        {
            get;
        }

        public RuntimeSoundInfo(
            SoundType type,
            float baseVolume,
            float pitchRange,
            int maxConcurrent,
            List<string> primaryKeys)
        {
            Type = type;
            BaseVolume = baseVolume;
            PitchRange = pitchRange;
            MaxConcurrent = maxConcurrent;
            PrimaryKeys = primaryKeys;
        }
    }

    private sealed class ClipRequest
    {
        public string primaryKey;
        public List<Action<AudioClip>> onLoadedList;
        public List<Action> onFailedList;
    }

    #endregion

    #region Fields

    public float EffectVolume
    {
        get;
        private set;
    } = 1f;

    public float BGMVolume
    {
        get;
        private set;
    } = 1f;

    private readonly object poolLock = new object();

    private Transform sfxPoolRoot;
    private Transform bgmRoot;
    private readonly Stack<AudioSource> sfxPool = new Stack<AudioSource>();
    private readonly HashSet<AudioSource> allSfxSources = new HashSet<AudioSource>();
    private AudioSource bgmSource;

    private int activePlayingCount = 0;
    private int peakPlayingCount = 0;

    private readonly Dictionary<string, string> sfxAliasToPrimaryKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> bgmAliasToPrimaryKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<SoundType, string> sfxTypeToPrimaryKey =
        new Dictionary<SoundType, string>();

    private readonly Dictionary<string, SoundType> sfxPrimaryKeyToType =
        new Dictionary<string, SoundType>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<SoundType, RuntimeSoundInfo> runtimeSoundInfoDict =
        new Dictionary<SoundType, RuntimeSoundInfo>();

    private readonly Dictionary<SoundType, int> playingCountDict =
        new Dictionary<SoundType, int>();

    private readonly Dictionary<AudioSource, Coroutine> followCoroutineDict =
        new Dictionary<AudioSource, Coroutine>();

    private bool isCatalogReady = false;

    private readonly Dictionary<string, AudioClip> clipResultCache =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ClipRequest> inFlightClipRequests =
        new Dictionary<string, ClipRequest>(StringComparer.OrdinalIgnoreCase);

    private string currentBgmPrimaryKey = string.Empty;

    #endregion

    private void Initialize()
    {
        EnsureAddressablesAssetManagerExists();
        InitializePools();
        StartCoroutine(InitializeSoundCatalogCoroutine());
    }

    private void EnsureAddressablesAssetManagerExists()
    {
        if (AddressablesAssetManager.Instance == null)
        {
            return;
        }
    }

    private void InitializePools()
    {
        if (sfxPoolRoot == null)
        {
            GameObject sfxRootObject = new GameObject("SFX_Pool");
            sfxRootObject.transform.SetParent(transform, false);
            sfxPoolRoot = sfxRootObject.transform;
        }

        if (bgmRoot == null)
        {
            GameObject bgmRootObject = new GameObject("BGM_Root");
            bgmRootObject.transform.SetParent(transform, false);
            bgmRoot = bgmRootObject.transform;
        }

        sfxPool.Clear();
        allSfxSources.Clear();

        for (int i = 0; i < effectPoolSize; i++)
        {
            GameObject sourceObject = new GameObject($"SFX_Source_{i:00}");
            sourceObject.transform.SetParent(sfxPoolRoot, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.ignoreListenerPause = true;
            source.spatialBlend = 0f;

            allSfxSources.Add(source);
            sfxPool.Push(source);
        }

        if (bgmSource == null)
        {
            GameObject bgmObject = new GameObject("BGM_Source");
            bgmObject.transform.SetParent(bgmRoot, false);

            bgmSource = bgmObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.ignoreListenerPause = true;
            bgmSource.spatialBlend = 0f;
        }
    }

    private IEnumerator InitializeSoundCatalogCoroutine()
    {
        Task task = InitializeSoundCatalogAsync();

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.Exception != null)
        {
            Debug.LogError($"[SoundManager] InitializeSoundCatalogAsync failed: {task.Exception}");
            yield break;
        }

        isCatalogReady = true;

        TryPlayPendingBgm();

        if (preloadCommonSFX)
        {
            PreloadSfxAliases();
        }

        Debug.Log($"[SoundManager] Sound catalog ready. SFX:{sfxAliasToPrimaryKey.Count}, BGM:{bgmAliasToPrimaryKey.Count}, RuntimeTypes:{runtimeSoundInfoDict.Count}");
    }

    private async Task InitializeSoundCatalogAsync()
    {
        await BuildSfxCatalogFromLabelAsync();
        await BuildBgmCatalogFromLabelAsync();

        LoadUserVolumes();
        BuildRuntimeSoundInfos();
        ApplyBgmVolume();
    }

    private void LoadUserVolumes()
    {
        if (UserDataManager.Instance == null)
        {
            return;
        }

        EffectVolume = Mathf.Clamp01(UserDataManager.Instance.SoundEffectVolume.Value);
        BGMVolume = Mathf.Clamp01(UserDataManager.Instance.BGMVolume.Value);
    }

    private void ApplyBgmVolume()
    {
        if (bgmSource == null)
        {
            return;
        }

        bgmSource.volume = masterVolume * BGMVolume;
    }

    private void BuildRuntimeSoundInfos()
    {
        runtimeSoundInfoDict.Clear();
        playingCountDict.Clear();

        if (soundInfos != null && soundInfos.Length > 0)
        {
            foreach (SoundInfo info in soundInfos)
            {
                if (info == null)
                {
                    continue;
                }

                List<string> resolvedPrimaryKeys = ResolvePrimaryKeysFromSoundInfo(info);

                if (resolvedPrimaryKeys.Count == 0)
                {
                    Debug.LogWarning($"[SoundManager] SoundInfo has no valid keys. Type:{info.type}");
                    continue;
                }

                RuntimeSoundInfo runtimeInfo = new RuntimeSoundInfo(
                    info.type,
                    info.baseVolume,
                    info.pitchRange,
                    Mathf.Max(1, info.maxConcurrent),
                    resolvedPrimaryKeys);

                runtimeSoundInfoDict[info.type] = runtimeInfo;
                playingCountDict[info.type] = 0;
            }

            return;
        }

        foreach (KeyValuePair<SoundType, string> kvp in sfxTypeToPrimaryKey)
        {
            SoundType type = kvp.Key;
            string primaryKey = kvp.Value;

            RuntimeSoundInfo runtimeInfo = new RuntimeSoundInfo(
                type,
                1f,
                0.2f,
                3,
                new List<string> { primaryKey });

            runtimeSoundInfoDict[type] = runtimeInfo;
            playingCountDict[type] = 0;
        }
    }

    private List<string> ResolvePrimaryKeysFromSoundInfo(SoundInfo info)
    {
        List<string> list = new List<string>();

        if (info.soundKeys == null || info.soundKeys.Length == 0)
        {
            return list;
        }

        foreach (string key in info.soundKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string trimmed = key.Trim();

            if (IsProbablyPrimaryKey(trimmed))
            {
                list.Add(trimmed);
                continue;
            }

            if (sfxAliasToPrimaryKey.TryGetValue(trimmed, out string primaryKey))
            {
                list.Add(primaryKey);
                continue;
            }

            Debug.LogWarning($"[SoundManager] SoundInfo key not found in SFX catalog: '{trimmed}', Type:{info.type}");
        }

        return list;
    }

    private async Task BuildSfxCatalogFromLabelAsync()
    {
        sfxAliasToPrimaryKey.Clear();
        sfxTypeToPrimaryKey.Clear();
        sfxPrimaryKeyToType.Clear();

        AsyncOperationHandle<IList<IResourceLocation>> locationsHandle =
            Addressables.LoadResourceLocationsAsync(sfxLabel, typeof(AudioClip));

        await locationsHandle.Task;

        if (locationsHandle.Status != AsyncOperationStatus.Succeeded || locationsHandle.Result == null)
        {
            Debug.LogError($"[SoundManager] SFX locations load failed. Label:{sfxLabel}");
            Addressables.Release(locationsHandle);
            return;
        }

        foreach (IResourceLocation location in locationsHandle.Result)
        {
            string primaryKey = location.PrimaryKey;

            if (!TryExtractAliasFromPrimaryKey(primaryKey, out string alias))
            {
                continue;
            }

            RegisterAliasOrMute(sfxLabel, sfxAliasToPrimaryKey, alias, primaryKey);

            if (Enum.TryParse(alias, true, out SoundType type))
            {
                RegisterSfxTypeOrMute(type, alias, primaryKey);
            }
        }

        Addressables.Release(locationsHandle);
    }

    private async Task BuildBgmCatalogFromLabelAsync()
    {
        bgmAliasToPrimaryKey.Clear();

        AsyncOperationHandle<IList<IResourceLocation>> locationsHandle =
            Addressables.LoadResourceLocationsAsync(bgmLabel, typeof(AudioClip));

        await locationsHandle.Task;

        if (locationsHandle.Status != AsyncOperationStatus.Succeeded || locationsHandle.Result == null)
        {
            Debug.LogError($"[SoundManager] BGM locations load failed. Label:{bgmLabel}");
            Addressables.Release(locationsHandle);
            return;
        }

        foreach (IResourceLocation location in locationsHandle.Result)
        {
            string primaryKey = location.PrimaryKey;

            if (!TryExtractAliasFromPrimaryKey(primaryKey, out string alias))
            {
                continue;
            }

            RegisterAliasOrMute(bgmLabel, bgmAliasToPrimaryKey, alias, primaryKey);
        }

        Addressables.Release(locationsHandle);
    }

    private void RegisterAliasOrMute(string label, Dictionary<string, string> dict, string alias, string primaryKey)
    {
        if (dict.TryGetValue(alias, out string existing) &&
            !string.Equals(existing, primaryKey, StringComparison.OrdinalIgnoreCase))
        {
            HandleAliasCollision(label, alias, existing, primaryKey, () =>
            {
                dict.Remove(alias);
            });

            return;
        }

        if (!dict.ContainsKey(alias))
        {
            dict.Add(alias, primaryKey);
        }
    }

    private void RegisterSfxTypeOrMute(SoundType type, string alias, string primaryKey)
    {
        if (sfxTypeToPrimaryKey.TryGetValue(type, out string existingByType) &&
            !string.Equals(existingByType, primaryKey, StringComparison.OrdinalIgnoreCase))
        {
            HandleAliasCollision(sfxLabel, alias, existingByType, primaryKey, () =>
            {
                sfxTypeToPrimaryKey.Remove(type);
                sfxPrimaryKeyToType.Remove(existingByType);
            });

            return;
        }

        if (!sfxTypeToPrimaryKey.ContainsKey(type))
        {
            sfxTypeToPrimaryKey.Add(type, primaryKey);
        }

        if (!sfxPrimaryKeyToType.ContainsKey(primaryKey))
        {
            sfxPrimaryKeyToType.Add(primaryKey, type);
        }
    }

    private void HandleAliasCollision(string label, string alias, string existingPrimaryKey, string newPrimaryKey, Action onInvalidate)
    {
        string message =
            $"[SoundManager] Alias collision detected. Label:{label}, Alias:'{alias}', Existing:'{existingPrimaryKey}', New:'{newPrimaryKey}'. Fix Addressables so alias is unique.";

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (throwOnAliasCollisionInDevelopmentBuild)
        {
            throw new InvalidOperationException(message);
        }
#endif

        Debug.LogError(message);
        onInvalidate?.Invoke();
    }

    private bool TryExtractAliasFromPrimaryKey(string primaryKey, out string alias)
    {
        alias = string.Empty;

        if (string.IsNullOrWhiteSpace(primaryKey))
        {
            return false;
        }

        string fileName = primaryKey.Split('/').LastOrDefault();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        alias = Path.GetFileNameWithoutExtension(fileName);
        return !string.IsNullOrWhiteSpace(alias);
    }

    private void PreloadSfxAliases()
    {
        foreach (string alias in preloadSFXKeys)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            string trimmed = alias.Trim();

            if (!TryResolveSfxPrimaryKey(trimmed, out string primaryKey))
            {
                Debug.LogWarning($"[SoundManager] Preload failed (alias not found): {trimmed}");
                continue;
            }

            RequestClip(primaryKey,
                onLoaded: clip =>
                {
                    if (clip != null)
                    {
                        Debug.Log($"[SoundManager] Preloaded SFX alias:{trimmed}, key:{primaryKey}");
                    }
                },
                onFailed: () =>
                {
                    Debug.LogWarning($"[SoundManager] Preload failed (load error): alias:{trimmed}, key:{primaryKey}");
                });
        }
    }

    #region Public API

    public void PlayEffect(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!isCatalogReady)
        {
            return;
        }

        string trimmedKey = key.Trim();

        if (TryResolveSfxPrimaryKey(trimmedKey, out string primaryKey))
        {
            PlaySfxPrimaryKey(null, primaryKey, PlayOption.Default);
            return;
        }

        if (IsProbablyPrimaryKey(trimmedKey))
        {
            PlaySfxPrimaryKey(null, trimmedKey, PlayOption.Default);
            return;
        }

        Debug.LogWarning($"[SoundManager] PlayEffect failed. Unknown alias: {trimmedKey}");
    }

    public void PlaySFX(SoundType type)
    {
        PlaySFX(type, PlayOption.Default);
    }

    public void PlaySFX(SoundType type, float volume)
    {
        PlaySFX(type, new PlayOption
        {
            volume = volume,
            pitch = 1f,
            pitchRange = 0.1f,
            delay = 0f,
            is3D = false,
            followTarget = null
        });
    }

    public void PlaySFX(SoundType type, PlayOption option)
    {
        if (!isCatalogReady)
        {
            return;
        }

        if (!runtimeSoundInfoDict.TryGetValue(type, out RuntimeSoundInfo info))
        {
            Debug.LogWarning($"[SoundManager] No runtime info for type:{type}");
            return;
        }

        if (!TryAcquireConcurrency(type, info.MaxConcurrent))
        {
            return;
        }

        string primaryKey = info.PrimaryKeys[Random.Range(0, info.PrimaryKeys.Count)];

        PlayOption finalOption = option;
        finalOption.pitchRange = Mathf.Max(0f, info.PitchRange);

        float finalVolume = masterVolume * EffectVolume * info.BaseVolume * finalOption.volume;

        if (finalOption.delay > 0f)
        {
            StartCoroutine(DelayedPlaySfx(type, primaryKey, finalVolume, finalOption));
            return;
        }

        PlaySfxPrimaryKey(type, primaryKey, finalOption, finalVolume);
    }

    public void Play3DSFX(SoundType type, Transform target)
    {
        PlaySFX(type, new PlayOption
        {
            volume = 1f,
            pitch = 1f,
            pitchRange = 0.1f,
            delay = 0f,
            is3D = true,
            followTarget = target
        });
    }

    public void Play3DSFX(SoundType type, Transform target, float volume)
    {
        PlaySFX(type, new PlayOption
        {
            volume = volume,
            pitch = 1f,
            pitchRange = 0.1f,
            delay = 0f,
            is3D = true,
            followTarget = target
        });
    }

    private bool hasPendingBgmRequest = false;
    private string pendingBgmKeyOrAlias = string.Empty;
    private float pendingBgmVolume = 1f;

    public void PlayBGM(string bgmKeyOrAlias, float volume = 1f)
    {
        if (string.IsNullOrWhiteSpace(bgmKeyOrAlias))
        {
            return;
        }

        if (!isCatalogReady)
        {
            hasPendingBgmRequest = true;
            pendingBgmKeyOrAlias = bgmKeyOrAlias.Trim();
            pendingBgmVolume = volume;
            return;
        }

        LoadAndPlayBgm(bgmKeyOrAlias.Trim(), volume);
    }

    private void TryPlayPendingBgm()
    {
        if (!hasPendingBgmRequest)
        {
            return;
        }

        hasPendingBgmRequest = false;
        LoadAndPlayBgm(pendingBgmKeyOrAlias, pendingBgmVolume);
    }

    #endregion

    private IEnumerator DelayedPlaySfx(SoundType type, string primaryKey, float finalVolume, PlayOption option)
    {
        float delay = Mathf.Max(0f, option.delay);
        yield return new WaitForSeconds(delay);

        if (!runtimeSoundInfoDict.TryGetValue(type, out RuntimeSoundInfo info))
        {
            ReleaseConcurrency(type);
            yield break;
        }

        PlaySfxPrimaryKey(type, primaryKey, option, finalVolume);
    }

    private void PlaySfxPrimaryKey(SoundType? typeOrNull, string primaryKey, PlayOption option)
    {
        float finalVolume = masterVolume * EffectVolume * option.volume;
        PlaySfxPrimaryKey(typeOrNull, primaryKey, option, finalVolume);
    }

    private void PlaySfxPrimaryKey(SoundType? typeOrNull, string primaryKey, PlayOption option, float finalVolume)
    {
        RequestClip(primaryKey,
            onLoaded: clip =>
            {
                if (clip == null)
                {
                    if (typeOrNull.HasValue)
                    {
                        ReleaseConcurrency(typeOrNull.Value);
                    }

                    return;
                }

                PlaySfxClip(typeOrNull, clip, finalVolume, option);
            },
            onFailed: () =>
            {
                if (typeOrNull.HasValue)
                {
                    ReleaseConcurrency(typeOrNull.Value);
                }
            });
    }

    private void PlaySfxClip(SoundType? typeOrNull, AudioClip clip, float volume, PlayOption option)
    {
        AudioSource source = null;

        lock (poolLock)
        {
            if (sfxPool.Count == 0)
            {
                Debug.LogWarning("[SoundManager] Effect pool exhausted!");

                if (typeOrNull.HasValue)
                {
                    ReleaseConcurrency(typeOrNull.Value);
                }

                return;
            }

            source = sfxPool.Pop();
            activePlayingCount++;
            peakPlayingCount = Mathf.Max(peakPlayingCount, activePlayingCount);
        }

        Transform sourceTransform = source.transform;

        if (option.is3D)
        {
            source.spatialBlend = 1f;

            if (option.followTarget != null)
            {
                sourceTransform.position = option.followTarget.position;
                StartFollow(source, option.followTarget);
            }
            else
            {
                sourceTransform.position = transform.position;
            }
        }
        else
        {
            source.spatialBlend = 0f;
            sourceTransform.localPosition = Vector3.zero;
            StopFollowIfNeeded(source);
        }

        source.clip = clip;
        source.volume = volume;
        source.pitch = option.pitch + Random.Range(-option.pitchRange, option.pitchRange);
        source.loop = false;
        source.Play();

        StartCoroutine(ReturnSourceAfterPlay(source, typeOrNull));
    }

    private IEnumerator ReturnSourceAfterPlay(AudioSource source, SoundType? typeOrNull)
    {
        yield return new WaitWhile(() => source != null && source.isPlaying);

        StopFollowIfNeeded(source);

        lock (poolLock)
        {
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                source.volume = 1f;
                source.pitch = 1f;
                source.loop = false;
                source.spatialBlend = 0f;

                if (allSfxSources.Contains(source))
                {
                    sfxPool.Push(source);
                }
            }

            activePlayingCount = Mathf.Max(0, activePlayingCount - 1);
        }

        if (typeOrNull.HasValue)
        {
            ReleaseConcurrency(typeOrNull.Value);
        }
    }

    private void StartFollow(AudioSource source, Transform target)
    {
        if (source == null || target == null)
        {
            return;
        }

        StopFollowIfNeeded(source);

        Coroutine coroutine = StartCoroutine(FollowTargetCoroutine(source.transform, target));
        followCoroutineDict[source] = coroutine;
    }

    private void StopFollowIfNeeded(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        if (!followCoroutineDict.TryGetValue(source, out Coroutine coroutine))
        {
            return;
        }

        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        followCoroutineDict.Remove(source);
    }

    private IEnumerator FollowTargetCoroutine(Transform follower, Transform target)
    {
        while (follower != null && target != null)
        {
            follower.position = target.position;
            yield return null;
        }
    }

    private bool TryAcquireConcurrency(SoundType type, int maxConcurrent)
    {
        lock (poolLock)
        {
            if (!playingCountDict.ContainsKey(type))
            {
                playingCountDict[type] = 0;
            }

            int playing = playingCountDict[type];

            if (playing >= maxConcurrent)
            {
                Debug.Log($"[SoundManager] {type} concurrent limit reached ({playing}/{maxConcurrent})");
                return false;
            }

            playingCountDict[type] = playing + 1;
            return true;
        }
    }

    private void ReleaseConcurrency(SoundType type)
    {
        lock (poolLock)
        {
            if (!playingCountDict.ContainsKey(type))
            {
                return;
            }

            playingCountDict[type] = Mathf.Max(0, playingCountDict[type] - 1);
        }
    }

    private bool TryResolveSfxPrimaryKey(string keyOrAlias, out string primaryKey)
    {
        primaryKey = string.Empty;

        if (string.IsNullOrWhiteSpace(keyOrAlias))
        {
            return false;
        }

        if (sfxAliasToPrimaryKey.TryGetValue(keyOrAlias, out primaryKey))
        {
            return true;
        }

        return false;
    }

    private bool TryResolveBgmPrimaryKey(string keyOrAlias, out string primaryKey)
    {
        primaryKey = string.Empty;

        if (string.IsNullOrWhiteSpace(keyOrAlias))
        {
            return false;
        }

        if (IsProbablyPrimaryKey(keyOrAlias))
        {
            primaryKey = keyOrAlias;
            return true;
        }

        if (bgmAliasToPrimaryKey.TryGetValue(keyOrAlias, out primaryKey))
        {
            return true;
        }

        return false;
    }

    private bool IsProbablyPrimaryKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (key.Contains("/"))
        {
            return true;
        }

        if (key.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void RequestClip(string primaryKey, Action<AudioClip> onLoaded, Action onFailed)
    {
        if (string.IsNullOrWhiteSpace(primaryKey))
        {
            onFailed?.Invoke();
            return;
        }

        if (clipResultCache.TryGetValue(primaryKey, out AudioClip cachedClip) && cachedClip != null)
        {
            onLoaded?.Invoke(cachedClip);
            return;
        }

        if (inFlightClipRequests.TryGetValue(primaryKey, out ClipRequest inFlight))
        {
            if (onLoaded != null)
            {
                inFlight.onLoadedList.Add(onLoaded);
            }

            if (onFailed != null)
            {
                inFlight.onFailedList.Add(onFailed);
            }

            return;
        }

        ClipRequest request = new ClipRequest();
        request.primaryKey = primaryKey;
        request.onLoadedList = new List<Action<AudioClip>>();
        request.onFailedList = new List<Action>();

        if (onLoaded != null)
        {
            request.onLoadedList.Add(onLoaded);
        }

        if (onFailed != null)
        {
            request.onFailedList.Add(onFailed);
        }

        inFlightClipRequests[primaryKey] = request;

        if (AddressablesAssetManager.Instance == null)
        {
            Debug.LogError("[SoundManager] AddressablesAssetManager.Instance is null.");
            FinishClipRequest(primaryKey, null, isSuccess: false);
            return;
        }

        AddressablesAssetManager.Instance.LoadAssetAsync<AudioClip>(
            AddressablesAssetManager.AssetCategory.AudioClip,
            primaryKey,
            clip =>
            {
                if (clip != null)
                {
                    clipResultCache[primaryKey] = clip;
                    FinishClipRequest(primaryKey, clip, isSuccess: true);
                }
                else
                {
                    FinishClipRequest(primaryKey, null, isSuccess: false);
                }
            });
    }

    private void FinishClipRequest(string primaryKey, AudioClip clip, bool isSuccess)
    {
        if (!inFlightClipRequests.TryGetValue(primaryKey, out ClipRequest request))
        {
            return;
        }

        inFlightClipRequests.Remove(primaryKey);

        if (isSuccess)
        {
            foreach (Action<AudioClip> callback in request.onLoadedList)
            {
                callback?.Invoke(clip);
            }
        }
        else
        {
            foreach (Action callback in request.onFailedList)
            {
                callback?.Invoke();
            }
        }
    }

    private void LoadAndPlayBgm(string bgmKeyOrAlias, float volume)
    {

        if (!TryResolveBgmPrimaryKey(bgmKeyOrAlias, out string primaryKey))
        {
            Debug.LogWarning($"[SoundManager] PlayBGM failed. Unknown alias: {bgmKeyOrAlias}");
            return;
        }

        StopBGM();
        currentBgmPrimaryKey = primaryKey;

        Debug.Log($"[SoundManager] BGM resolved. input:{bgmKeyOrAlias}, primaryKey:{primaryKey}");

        RequestClip(primaryKey,
            onLoaded: clip =>
            {
                if (clip == null)
                {
                    Debug.LogError($"[SoundManager] BGM load failed. key:{primaryKey}");
                    return;
                }

                bgmSource.clip = clip;
                bgmSource.volume = masterVolume * BGMVolume * Mathf.Clamp01(volume);
                bgmSource.Play();
            },
            onFailed: () =>
            {
                Debug.LogError($"[SoundManager] BGM load failed. key:{primaryKey}");
            });
    }

    #region Volume Control

    public void SetEffectVolume(float volume)
    {
        EffectVolume = Mathf.Clamp01(volume);

        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.SoundEffectVolume.Value = EffectVolume;
        }
    }

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);

        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.BGMVolume.Value = BGMVolume;
        }

        ApplyBgmVolume();

        if (bgmSource != null && bgmSource.clip != null && !bgmSource.isPlaying && BGMVolume > 0f)
        {
            bgmSource.Play();
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }

        currentBgmPrimaryKey = string.Empty;
    }

    public void PauseBGM()
    {
        bgmSource?.Pause();
    }

    public void ResumeBGM()
    {
        bgmSource?.UnPause();
    }

    #endregion

    #region Debug Stats

    [ContextMenu("Print Stats")]
    private void PrintStats()
    {
        Debug.Log($"[SoundManager] Active:{activePlayingCount} Peak:{peakPlayingCount} Pool:{sfxPool.Count}");
    }

    #endregion

    private void OnDestroy()
    {
        StopBGM();

        foreach (KeyValuePair<AudioSource, Coroutine> kvp in followCoroutineDict.ToList())
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }

        followCoroutineDict.Clear();
        inFlightClipRequests.Clear();
        clipResultCache.Clear();
    }
}