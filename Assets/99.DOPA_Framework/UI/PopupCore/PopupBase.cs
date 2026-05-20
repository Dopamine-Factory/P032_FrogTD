using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System;


public class PopupBase : MonoBehaviour
{
    #region Configuration
    [System.Serializable]
    public class PopupSettings
    {
        [Header("Animation")]
        public bool useAnimation = true;
        public float fadeDuration = 0.3f;

        [Header("Behavior")]
        public bool destroyOnClose = false;
        public bool blockBackground = true;

        [Header("Background Dim")]
        public bool useCustomDimColor = false;
        public Color dimColor = new Color(0, 0, 0, 0.8f);

        [Header("SafeArea Option")]
        [Tooltip("해당 팝업 상위 캔버스가 SafeArea 미적용 중이라면 현재 팝업 세이프 에리어 개별 적용 시 사용")]
        public bool applySafeAreaIndividually = false;

        [Header("TimeScale Control")]
        public PopupTimeScaleMode timeScaleMode = PopupTimeScaleMode.IgnoreTimeScale;

        public enum PopupTimeScaleMode
        {
            FollowTimeScale,
            IgnoreTimeScale,
            InstantAlways
        }
    }

    [SerializeField] protected PopupSettings settings;
    #endregion

    #region Core References (Inspector 또는 Auto Setup)
    [Header("Core Components")]
    [SerializeField] protected CanvasGroup canvasGroup;
    [SerializeField] protected RectTransform mainPanel;
    [SerializeField] protected TextMeshProUGUI titleText;
    [SerializeField] protected Image backgroundDim;
    #endregion

    #region User Manual Close Button Settings
    [Header("Manual Close Button Settings")]
    [SerializeField]
    [Tooltip("기본 닫기 버튼")]
    protected Button mainCloseButton;
    public Action onMainCloseButtonCallback;

    [SerializeField]
    [Tooltip("확인 버튼 (옵션)")]
    protected Button confirmButton;

    [SerializeField]
    [Tooltip("취소 버튼 (옵션)")]
    private Button cancelButton;

    [SerializeField]
    [Tooltip("배경 클릭으로 닫기 옵션")]
    private bool closeOnBackgroundClick = false;

    [SerializeField]
    [Tooltip("ESC 키로 닫기")]
    private bool closeOnEscape = false;
    #endregion

    #region Scroll Settings
    [Header("Scroll Settings")]
    [SerializeField]
    [Tooltip("팝업 내 스크롤 등록 (옵션)")]
    protected ScrollRect[] scrollRects;
    #endregion


    #region Events
    [Header("Callbacks")]
    public UnityEvent onOpen;
    public UnityEvent onClose;
    public UnityEvent<string> onContentBind;
    #endregion

    #region Properties (Canvas 풀링 지원)
    public bool IsOpened { get; private set; }
    public bool IsPooled { get; private set; }
    public string AddressKey { get; private set; }
    public Canvas TargetCanvas { get; private set; }

    public event Action<PopupBase> OnClosed;
    #endregion

    #region Runtime Fields
    private Coroutine currentAnimation;
    private Transform originalParent;
    private AsyncOperationHandle<GameObject> instantiateHandle;
    private bool isInitialized = false;
    #endregion

    #region Unity Lifecycle
    protected virtual void Awake()
    {
        InitializeComponents();
    }

    protected virtual void Update()
    {
        if (closeOnEscape && IsOpened && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    protected virtual void OnDestroy()
    {
        CleanupResources();
    }
    #endregion

    #region Addressables & Canvas Initialization
    public virtual void InitializeForAddressables(string addressKey, AsyncOperationHandle<GameObject> handle, bool isPooled = false)
    {
        AddressKey = addressKey;
        instantiateHandle = handle;
        IsPooled = isPooled;
        originalParent = transform.parent;
        isInitialized = true;
    }

    public void AssignCanvas(Canvas canvas)
    {
        TargetCanvas = canvas;
        Debug.Log($"PopupBase[{AddressKey}]: Assigned to Canvas '{canvas.name}'");
    }

    protected virtual void InitializeComponents()
    {
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (mainPanel == null)
            mainPanel = GetComponent<RectTransform>();

        SetupManualCloseButtons();

        SetupBackgroundDim();
        SetInitialState();

        OnInitialize();
    }
    #endregion

    #region Manual Close Button Setup (Core 기능 추가)
    private void SetupManualCloseButtons()
    {
        if (mainCloseButton != null)
        {
            mainCloseButton.onClick.RemoveAllListeners();
            mainCloseButton.onClick.AddListener(OnMainCloseClicked);
            Debug.Log($"[PopupBase] MainCloseButton bound for {AddressKey}");
        }
        else
        {
            Debug.LogWarning($"[PopupBase] {AddressKey}: mainCloseButton not assigned!");
        }

        // Confirm Button
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
            Debug.Log($"[PopupBase] ConfirmButton bound for {AddressKey}");
        }

        // Cancel Button  
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
            Debug.Log($"[PopupBase] CancelButton bound for {AddressKey}");
        }

        // Background Click
        if (closeOnBackgroundClick && backgroundDim != null)
        {
            SetupBackgroundClick();
        }
    }

    private void SetupBackgroundClick()
    {
        var trigger = backgroundDim.gameObject.GetComponent<EventTrigger>() ??
                      backgroundDim.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerClick);

        var pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        pointerClick.callback.AddListener((data) =>
        {
            Close();
        });
        trigger.triggers.Add(pointerClick);

        Debug.Log($"[PopupBase] Background click enabled for {AddressKey}");
    }

    protected virtual void OnMainCloseClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundManager.SoundType.Click);

        onMainCloseButtonCallback?.Invoke();

        Close();
    }

    protected virtual void OnConfirmClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundManager.SoundType.Click);
        OnConfirm();
        Close();
    }

    protected virtual void OnCancelClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundManager.SoundType.Click);
        OnCancel();
        Close();
    }

    protected virtual void OnConfirm() { }
    protected virtual void OnCancel() { }
    #endregion

    #region Public API
    public virtual void Open(string title = null, string content = null)
    {
        if (IsOpened) return;

        SetupPopupCanvas();
        gameObject.SetActive(true);

        BindTitle(title);
        onContentBind?.Invoke(content);

        StartCoroutine(settings.useAnimation ? OpenWithAnimation() : OpenInstant());

        IsOpened = true;
        onOpen?.Invoke();

        SetInteractable(true);
    }

    public virtual void Close()
    {
        if (!IsOpened) return;

        IsOpened = false;

        StartCoroutine(settings.useAnimation ? CloseWithAnimation() : CloseInstant());
    }


    public void SetInteractable(bool interactable)
    {
        canvasGroup.interactable = interactable;

        canvasGroup.blocksRaycasts = IsOpened;

        SetButtonInteractable(mainCloseButton, interactable);
        SetButtonInteractable(confirmButton, interactable);
        SetButtonInteractable(cancelButton, interactable);

        Debug.Log($"[PopupBase] SetInteractable({interactable}), blocksRaycasts={canvasGroup.blocksRaycasts}");
    }


    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }
    #endregion

    #region Canvas Setup (PopupManager 의존)
    private void SetupPopupCanvas()
    {
        transform.SetParent(TargetCanvas.transform, false);

        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = true;
    }
    #endregion

    #region Background Dim

    private void SetupBackgroundDim()
    {
        if (backgroundDim == null) return;

        if (settings.useCustomDimColor)
        {
            backgroundDim.color = settings.dimColor;
        }

        backgroundDim.raycastTarget = settings.blockBackground;
    }
    #endregion

    #region Animation System (기존 그대로)
    private IEnumerator OpenWithAnimation()
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);

        float duration = settings.fadeDuration;
        float elapsed = 0f;

        canvasGroup.alpha = 0f;
        mainPanel.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            switch (settings.timeScaleMode)
            {
                case PopupSettings.PopupTimeScaleMode.FollowTimeScale: elapsed += Time.deltaTime; break;
                case PopupSettings.PopupTimeScaleMode.IgnoreTimeScale: elapsed += Time.unscaledDeltaTime; break;
                case PopupSettings.PopupTimeScaleMode.InstantAlways: elapsed = duration; break;
            }

            float t = elapsed / duration;
            mainPanel.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        mainPanel.localScale = Vector3.one;

        OnOpenAnimationComplete();
        currentAnimation = null;
    }

    private IEnumerator CloseWithAnimation()
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);

        float duration = settings.fadeDuration;
        float elapsed = 0f;
        canvasGroup.alpha = 1f;
        mainPanel.localScale = Vector3.one;

        while (elapsed < duration)
        {
            switch (settings.timeScaleMode)
            {
                case PopupSettings.PopupTimeScaleMode.FollowTimeScale: elapsed += Time.deltaTime; break;
                case PopupSettings.PopupTimeScaleMode.IgnoreTimeScale: elapsed += Time.unscaledDeltaTime; break;
                case PopupSettings.PopupTimeScaleMode.InstantAlways: elapsed = duration; break;
            }

            float t = elapsed / duration;
            mainPanel.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        mainPanel.localScale = Vector3.zero;

        OnCloseAnimationComplete();
        currentAnimation = null;
    }

    private IEnumerator OpenInstant()
    {
        canvasGroup.alpha = 1f;
        mainPanel.localScale = Vector3.one;
        OnOpenAnimationComplete();
        yield return null;
    }

    private IEnumerator CloseInstant()
    {
        OnCloseAnimationComplete();
        yield return null;
    }

    protected virtual void OnOpenAnimationComplete()
    {
        mainPanel.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
    }

    private void OnCloseAnimationComplete()
    {
        mainPanel.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;



        onClose?.Invoke();
        IsOpened = false;

        OnClosed?.Invoke(this);


        if (scrollRects != null)
        {
            foreach (var scrollRect in scrollRects)
            {
                if (scrollRect?.content == null) continue;

                RectTransform content = scrollRect.content;

                if (Mathf.Approximately(content.anchorMin.y, 1f) && Mathf.Approximately(content.anchorMax.y, 1f))
                {
                    float halfHeight = content.sizeDelta.y * 0.5f;
                    Vector2 stablePos = content.anchoredPosition;
                    stablePos.y = Mathf.Clamp(stablePos.y, -halfHeight, 0f);
                    content.anchoredPosition = stablePos;
                }
                else
                {
                    content.anchoredPosition = content.anchoredPosition;
                }

                Canvas.ForceUpdateCanvases();
            }
        }


        if (settings.destroyOnClose)
        {
            CleanupResources();
            Destroy(gameObject);
        }
        else
        {
            ReturnToPool();
        }
    }
    #endregion

    #region Pooling System (기존)
    private void SetInitialState()
    {
        gameObject.SetActive(false);
        canvasGroup.alpha = 0f;
        mainPanel.localScale = Vector3.zero;

        canvasGroup.blocksRaycasts = false;
        SetInteractable(false);
    }

    public void ReturnToPool()
    {
        IsOpened = false;
        SetInitialState();

        if (originalParent != null)
            transform.SetParent(originalParent, false);

        TargetCanvas = null;

        OnReturnToPool();
    }

    protected virtual void OnReturnToPool() { }
    #endregion

    #region Virtual Methods (기존)
    protected virtual void OnInitialize() { }

    private void BindTitle(string title)
    {
        if (string.IsNullOrEmpty(title) || titleText == null) return;
        titleText.text = title;
    }
    #endregion

    #region Cleanup (기존)
    private void CleanupResources()
    {
        if (instantiateHandle.IsValid())
        {
            Addressables.ReleaseInstance(instantiateHandle);
        }

        currentAnimation = null;
        TargetCanvas = null;

        OnCleanup();
    }

    public void RegisterToSafeArea(EnhancedSafeArea safeArea)
    {
        Debug.Log($"[PopupBase] {AddressKey} RegisterToSafeArea Start");

        if (!settings.applySafeAreaIndividually)
        {
            Debug.Log($"[PopupBase] {AddressKey} settings.applySafeAreaIndividually false");

            return;
        }

        if (TargetCanvas != null)
        {
            RectTransform parentRT = TargetCanvas.GetComponent<RectTransform>();
            if (parentRT != null && safeArea.TargetPanels.Contains(parentRT))
            {
                Debug.Log($"[PopupBase] {AddressKey} safeArea.TargetPanels.Contains(parentRT)");

                return;
            }
        }

        if (safeArea != null && mainPanel != null && !safeArea.TargetPanels.Contains(mainPanel))
        {
            safeArea.TargetPanels.Add(mainPanel);
            safeArea.RefreshAllPanels();
            Debug.Log($"[PopupBase] {AddressKey} RegisterToSafeArea Setting Success");
        }
    }

    protected virtual void OnCleanup() { }
    #endregion
}