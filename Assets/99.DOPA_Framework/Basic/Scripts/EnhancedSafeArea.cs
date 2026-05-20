using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;
using System;
using UnityEngine.UI;
using Unity.VisualScripting;

[RequireComponent(typeof(Canvas))]
public class EnhancedSafeArea : MonoBehaviour
{
    [System.Serializable]
    public class SafeAreaConfig
    {
        public bool ignoreX = false;
        public bool ignoreY = false;
        public Vector2 padding = Vector2.zero;
    }

    public enum CameraType
    {
        Auto,
        Orthographic,
        Perspective
    }

    public interface IGameWorldData
    {
        Transform[] GetGameContainers();
        Vector3Int GetMapSize();
        float GetCellSize();
    }

    [Header("UI 대상 Panels (RectTransform)")]
    [SerializeField] private List<RectTransform> targetPanels = new List<RectTransform>();
    public List<RectTransform> TargetPanels
    {
        get => targetPanels;
        set => targetPanels = value;
    }

    [Header("Popup Panels (Runtime 등록)")]
    public List<RectTransform> popupPanels = new List<RectTransform>();

    [Header("Cameras (우선순위: Ingame > Virtual > Main)")]
    [SerializeField] private Camera ingameCamera;
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Camera mainCamera;
    public CameraType preferredCameraType = CameraType.Auto;

    [Header("추적 부드러움")]
    public float trackingSpeed = 8f;     // 1~20 (낮을수록 부드러움)
    public float minTrackingDistance = 5f; // Perspective 최소 거리

    private Vector3 targetCameraPos;
    private float targetCameraSize;

    [Header("World Fitting (성능 토글)")]
    [Tooltip("Renderer 기반 ounds로 카메라 자동 피팅 (2D/3D 다 됩니다)")]
    public bool enableWorldFitting = false;
    [Tooltip("LayerMask로 검사할 Renderer만 필터링")]
    public LayerMask rendererLayers = -1;
    [Tooltip("bounds 계산 주기 (0 = 매번)")]
    public float boundsUpdateInterval = 0.2f;
    [Tooltip("컨테이너 없을 때 fallback")]
    public float fallbackOrthoSize = 5f;

    [Header("SafeArea 기본 설정")]
    public SafeAreaConfig globalSettings = new SafeAreaConfig();

    [Header("외부 데이터 주입")]
    public IGameWorldData gameWorldData;

    [Header("Canvas Scaler 자동 조정")]
    [Tooltip("SafeArea 변화에 따라 Canvas Scaler match 가중치 자동 조정")]
    public bool autoAdjustCanvasScaler = true;

    private CanvasScaler canvasScaler;
    private Vector2 baseRefResolution;
    private float baseAspect;
    private float lastMatchWeight;
    [Header("실시간 추적")]
    public bool enableRealtimeTracking = true;
    public float trackingInterval = 0.05f;
    private float lastTrackingTime;
    // Private fields
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);

    [Header("배너 높이")]
    [SerializeField] private float bannerHeightPixels = 0f;
    public float BannerHeightPixels
    {
        get => bannerHeightPixels;
        set
        {
            if (Mathf.Abs(bannerHeightPixels - value) > 0.1f)
            {
                bannerHeightPixels = value;
                if (!isRefreshInProgress)
                {
                    RefreshAllPanels(bannerHeightPixels);
                }
            }
        }
    }

    private float lastBannerHeight = -1f;
    private bool isRefreshInProgress = false;
    private float lastCanvasMatchWeight = -1f;

    private Vector2Int lastScreenSize = new Vector2Int(0, 0);
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

    private float lastBoundsUpdateTime;
    private Bounds cachedTotalBounds;
    private bool boundsDirty = true;
    private float currentWorldPadding;

    private void Awake()
    {
        Debug.Log("[SafeArea] Awake START!");
        Debug.Log("[SafeArea] Awake! GameObject.active=" + gameObject.activeInHierarchy);
        Debug.Log("[SafeArea] Script.enabled=" + enabled);
        Debug.Log("[SafeArea] Canvas=" + GetComponent<Canvas>() != null);

        canvasScaler = GetComponent<CanvasScaler>();

        if (mainCamera == null)
            mainCamera = Camera.main;


        if (canvasScaler != null)
        {
            baseRefResolution = canvasScaler.referenceResolution;
            baseAspect = baseRefResolution.x / baseRefResolution.y;
            lastMatchWeight = canvasScaler.matchWidthOrHeight;
            lastCanvasMatchWeight = lastMatchWeight;
        }

        CacheScreenState();
        lastBannerHeight = bannerHeightPixels;

        isRefreshInProgress = true;
        RefreshAllPanels(0f);
        isRefreshInProgress = false;
    }


    private void Update()
    {
        if (isRefreshInProgress) return;

        if (Mathf.Abs(lastBannerHeight - bannerHeightPixels) > 0.1f)
        {
            lastBannerHeight = bannerHeightPixels;
            RefreshAllPanels(bannerHeightPixels);
            return;
        }

        if (HasScreenChanged())
        {
            RefreshAllPanels(0f);
            return;
        }

        if (enableRealtimeTracking && enableWorldFitting &&
            Time.time - lastTrackingTime >= trackingInterval)
        {
            RefreshWorldFittingOnly();
            lastTrackingTime = Time.time;
        }

        if (enableWorldFitting)
        {
            float bannerWorld = bannerHeightPixels / (float)Screen.height * GetCurrentOrthoSize();
            AutoFitCamera(cachedTotalBounds, bannerWorld);
        }
    }
    private bool HasScreenChanged()
    {
        bool screenChanged = Screen.safeArea != lastSafeArea ||
                           Screen.width != lastScreenSize.x ||
                           Screen.height != lastScreenSize.y ||
                           Screen.orientation != lastOrientation;

        bool canvasChanged = false;
        if (canvasScaler != null && lastCanvasMatchWeight >= 0f)
        {
            canvasChanged = Mathf.Abs(canvasScaler.matchWidthOrHeight - lastCanvasMatchWeight) > 0.01f;
        }

        return screenChanged || canvasChanged;
    }

    /// <summary>
    /// UI SafeArea + World Camera 완전 갱신 (배너 높이 지원)
    /// </summary>
    public void RefreshAllPanels(float bannerHeightPixel = 0f)
    {
        if (isRefreshInProgress)
        {
            Debug.LogWarning("[SafeArea] Refresh skipped: already in progress");
            return;
        }

        isRefreshInProgress = true;

        Debug.Log($"[SafeArea] RefreshAllPanels(banner={bannerHeightPixels:F0}px)");

        Rect safeArea = ApplySafeAreaToPanels(bannerHeightPixels);

        if (enableWorldFitting && gameWorldData?.GetGameContainers() != null)
        {
            float bottomBannerWorld = bannerHeightPixels / (float)Screen.height * GetCurrentOrthoSize();
            Bounds totalBounds = GetTotalWorldBounds();
            AutoFitCamera(totalBounds, bottomBannerWorld);
        }

        CacheScreenState();
        isRefreshInProgress = false;
    }

    /// <summary>
    /// World fitting만 갱신 (화면 변화 없을 때)
    /// </summary>
    public void RefreshWorldFittingOnly()
    {
        if (!enableWorldFitting || gameWorldData == null) return;

        Bounds totalBounds = GetTotalWorldBounds();
        float bannerWorld = 0f;
        AutoFitCamera(totalBounds, bannerWorld);
    }

    private Rect ApplySafeAreaToPanels(float bannerHeightPixels)
    {
        Rect safeArea = Screen.safeArea;
        Debug.Log($"[SafeArea] Original Screen.safeArea: {safeArea}");
        Debug.Log($"[SafeArea] Input bannerHeightPixels: {bannerHeightPixels:F0}px");

        // 1. 기본 패딩
        safeArea.x += globalSettings.padding.x;
        safeArea.y += globalSettings.padding.y;
        safeArea.width -= globalSettings.padding.x * 2f;
        safeArea.height -= globalSettings.padding.y * 2f;

        // 2. 배너 적용 (항상!)
        float effectiveBannerHeight = bannerHeightPixels;
        if (globalSettings.ignoreY)
        {
            // ignoreY=true: 전체 높이 기준으로 비율만 적용
            effectiveBannerHeight = bannerHeightPixels / Screen.height * Screen.height;
        }

        safeArea.y += effectiveBannerHeight;
        safeArea.height -= effectiveBannerHeight;

        // 3. Ignore 적용
        if (globalSettings.ignoreX)
        {
            safeArea.x = 0;
            safeArea.width = Screen.width;
        }
        if (globalSettings.ignoreY)
        {
            safeArea.y = 0;
            safeArea.height = Screen.height - effectiveBannerHeight;
            Debug.Log($"[SafeArea] ignoreY + Banner: height={safeArea.height:F0}");
        }

        Debug.Log($"[SafeArea] FINAL: {safeArea}");

        if (autoAdjustCanvasScaler && canvasScaler != null)
        {
            AdjustCanvasScaler(safeArea);
        }

        Vector2 anchorMin = safeArea.position / new Vector2(Screen.width, Screen.height);
        Vector2 anchorMax = (safeArea.position + safeArea.size) / new Vector2(Screen.width, Screen.height);
        ApplyAnchorsToPanels(anchorMin, anchorMax);

        return safeArea;
    }

    private const float MatchEpsilon = 0.001f;

    private void AdjustCanvasScaler(Rect safeArea)
    {
        if (canvasScaler == null) return;

        float safeAspect = safeArea.width / safeArea.height;
        float targetMatchWeight = safeAspect >= baseAspect ? 1f : 0f;

        if (Mathf.Abs(canvasScaler.matchWidthOrHeight - targetMatchWeight) > MatchEpsilon)
        {
            canvasScaler.matchWidthOrHeight = targetMatchWeight;
            lastMatchWeight = targetMatchWeight;
            lastCanvasMatchWeight = targetMatchWeight;
            Debug.Log($"[SafeArea] CanvasScaler adjusted: match={targetMatchWeight}");
        }
    }

    private void ApplyAnchorsToPanels(Vector2 anchorMin, Vector2 anchorMax)
    {
        foreach (var panel in TargetPanels)
        {
            if (panel != null)
                SetPanelAnchors(panel, anchorMin, anchorMax);
        }

        for (int i = popupPanels.Count - 1; i >= 0; i--)
        {
            if (popupPanels[i] == null)
                popupPanels.RemoveAt(i);
            else
                SetPanelAnchors(popupPanels[i], anchorMin, anchorMax);
        }
    }


    private void SetPanelAnchors(RectTransform panel, Vector2 min, Vector2 max)
    {
        panel.anchorMin = min;
        panel.anchorMax = max;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }

    private Bounds GetTotalWorldBounds()
    {
        if (!boundsDirty)
            return cachedTotalBounds;

        Bounds totalBounds = new Bounds();
        bool hasBounds = false;

        Transform[] containers = gameWorldData.GetGameContainers();
        if (containers == null || containers.Length == 0)
        {
            totalBounds.size = Vector3.one * fallbackOrthoSize * 2f;
            return totalBounds;
        }

        foreach (Transform container in containers)
        {
            if (container == null) continue;

            Bounds containerBounds = CalculateWorldBoundsOfRenderers(container);
            if (containerBounds.size == Vector3.zero) continue;

            if (!hasBounds)
            {
                totalBounds = containerBounds;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(containerBounds);
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning("[EnhancedSafeArea] No valid Renderer bounds found");
            totalBounds.size = Vector3.one * fallbackOrthoSize * 2f;
        }

        cachedTotalBounds = totalBounds;
        boundsDirty = false;
        lastBoundsUpdateTime = Time.time;



        Debug.Log($"[SafeArea] Total containers: {containers?.Length ?? 0}");
        for (int i = 0; i < containers.Length; i++)
        {
            var cb = CalculateWorldBoundsOfRenderers(containers[i]);
            Debug.Log($"Container[{i}] '{containers[i]?.name}': size={cb.size}, count={cb.size.magnitude:F2}");
        }

        Debug.Log($"[SafeArea] Final bounds: center={totalBounds.center}, size={totalBounds.size}");
        return totalBounds;
    }

    private Bounds CalculateWorldBoundsOfRenderers(Transform container)
    {
        Renderer[] renderers = container.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(container.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (((1 << renderers[i].gameObject.layer) & rendererLayers) != 0)
                bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    [SerializeField] private Transform cameraTrackingTargetOverride;

    private void EnsureTrackingTargetOverride()
    {
        if (virtualCamera == null)
        {
            return;
        }

        if (cameraTrackingTargetOverride != null)
        {
            return;
        }

        GameObject go = new GameObject("SafeAreaCameraTrackingTarget");
        go.transform.SetParent(transform, false);
        cameraTrackingTargetOverride = go.transform;

        virtualCamera.Target.TrackingTarget = cameraTrackingTargetOverride;
    }


    private void AutoFitCamera(Bounds totalBounds, float bottomBannerWorld)
    {
        Bounds paddedBounds = GetPaddedBounds(totalBounds);

        targetCameraPos = paddedBounds.center;
        targetCameraSize = CalculateTargetSize(paddedBounds.size, bottomBannerWorld, (float)Screen.width / Screen.height);

        float t = Time.deltaTime * trackingSpeed;

        if (virtualCamera != null)
        {
            if (cameraTrackingTargetOverride != null)
            {
                cameraTrackingTargetOverride.position = Vector3.Lerp(cameraTrackingTargetOverride.position, targetCameraPos, t);
            }
            else
            {
                virtualCamera.transform.position = Vector3.Lerp(virtualCamera.transform.position, targetCameraPos, t);
            }

            LensSettings lens = virtualCamera.Lens;

            if (lens.Orthographic)
            {
                lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, targetCameraSize, t);
            }
            else
            {
                float distance = Mathf.Max(minTrackingDistance, Vector3.Distance(virtualCamera.transform.position, targetCameraPos));
                float fov = 2f * Mathf.Atan(targetCameraSize / distance) * Mathf.Rad2Deg;
                lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, fov, t);
            }

            virtualCamera.Lens = lens;
            return;
        }

        Camera targetCamera = ingameCamera ?? mainCamera ?? Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, targetCameraPos, t);

        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetCameraSize, t);
        }
        else
        {
            float distance = Mathf.Max(minTrackingDistance, Vector3.Distance(targetCamera.transform.position, targetCameraPos));
            float fov = 2f * Mathf.Atan(targetCameraSize / distance) * Mathf.Rad2Deg;
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, fov, t);
        }
    }

    private void ApplyToCinemachine(CinemachineCamera vcam, Vector3 size, float bannerWorld, Vector3 center, float aspect)
    {
        var lens = vcam.Lens;

        if (lens.Orthographic)
        {
            float orthoHeight = (size.y + bannerWorld * 2f) * 0.5f;
            float orthoWidth = size.x * 0.5f / aspect;
            lens.OrthographicSize = Mathf.Max(orthoHeight, orthoWidth);
        }
        else  // Perspective
        {
            float distance = Mathf.Abs(vcam.transform.position.z - center.z);
            float fovHeight = 2f * Mathf.Atan((size.y * 0.5f + bannerWorld) / distance) * Mathf.Rad2Deg;
            float fovWidth = 2f * Mathf.Atan(size.x * 0.5f / distance / aspect) * Mathf.Rad2Deg;
            lens.FieldOfView = Mathf.Max(fovHeight, fovWidth);
        }

        vcam.transform.position = center;
    }

    private void ApplyToUnityCamera(Camera camera, Vector3 size, float bannerWorld, Vector3 center, float aspect)
    {
        if (camera.orthographic)
        {
            float orthoHeight = (size.y + bannerWorld * 2f) * 0.5f;
            float orthoWidth = size.x * 0.5f / aspect;
            camera.orthographicSize = Mathf.Max(orthoHeight, orthoWidth);
        }
        else
        {
            float distance = Mathf.Abs(camera.transform.position.z - center.z);
            float fovHeight = 2f * Mathf.Atan((size.y * 0.5f + bannerWorld) / distance) * Mathf.Rad2Deg;
            float fovWidth = 2f * Mathf.Atan(size.x * 0.5f / distance / aspect) * Mathf.Rad2Deg;
            camera.fieldOfView = Mathf.Max(fovHeight, fovWidth);
        }

        camera.transform.position = center;
    }

    private float GetCurrentOrthoSize()
    {
        if (gameWorldData != null)
        {
            Vector3Int mapSize = gameWorldData.GetMapSize();
            return (mapSize.y * gameWorldData.GetCellSize() / 2f) + currentWorldPadding;
        }
        return fallbackOrthoSize;
    }

    private void CacheScreenState()
    {
        lastSafeArea = Screen.safeArea;

        lastScreenSize.x = Screen.width;
        lastScreenSize.y = Screen.height;

        lastOrientation = Screen.orientation;
        if (canvasScaler != null)
        {
            lastCanvasMatchWeight = canvasScaler.matchWidthOrHeight;
        }
    }


    // Public API
    public void MarkBoundsDirty()
    {
        boundsDirty = true;
    }

    public void RegisterPopup(RectTransform popup)
    {
        if (!popupPanels.Contains(popup))
            popupPanels.Add(popup);
    }

    public void UnregisterPopup(RectTransform popup)
    {
        popupPanels.Remove(popup);
    }

    private Bounds GetPaddedBounds(Bounds bounds)
    {
        currentWorldPadding = 0.3f;  // 여백 조절
        return new Bounds(bounds.center, bounds.size + Vector3.one * currentWorldPadding * 2f);
    }

    private float CalculateTargetSize(Vector3 boundsSize, float bannerWorld, float aspect)
    {
        float height = boundsSize.y + bannerWorld * 2f;
        float width = boundsSize.x;
        return Mathf.Max(height * 0.5f, width * 0.5f / aspect);
    }
}