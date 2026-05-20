using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Framework.Game.UI.Controls
{
    public interface IToggle
    {
        bool IsOn { get; }
        void SetIsOn(bool value, bool invokeEvent = true);
        UnityEvent<bool> OnValueChanged { get; }
    }

    [RequireComponent(typeof(Button))]
    public class ToggleSwitch : MonoBehaviour, IToggle
    {
        [Header("Required")]
        [SerializeField] private Image toggleImage;

        [Header("Visual States")]
        [SerializeField] private Color onColor = Color.white;
        [SerializeField] private Color offColor = Color.red;
        [SerializeField] private bool isReversed;

        [Header("Group")]
        [SerializeField] private ToggleSwitchGroup toggleGroup;

        [SerializeField] private bool isOn;
        public bool IsOn => isOn;
        public UnityEvent<bool> OnValueChanged { get; } = new UnityEvent<bool>();

        private Button button;
        private AudioSource audioSource;
        private Coroutine animationCoroutine;

        private void Awake()
        {
            button = GetComponent<Button>();
            audioSource = GetComponent<AudioSource>();
            RefreshVisualState();
        }

        private void OnEnable()
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            RefreshVisualState();
        }

        private void OnDisable()
        {
            button.onClick.RemoveAllListeners();
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            OnValueChanged.RemoveAllListeners();
        }

        public void SetIsOn(bool value, bool invokeEvent = true)
        {
            if (isOn == value) return;

            isOn = value;

            if (invokeEvent)
            {
                OnValueChanged.Invoke(isOn);
            }

            if (toggleGroup != null && isOn)
            {
                toggleGroup.NotifyToggleOn(this, isOn);
            }

            RefreshVisualState();
        }

        private void OnClick()
        {
            SetIsOn(!isOn);
            PlayToggleSound();
        }

        private void RefreshVisualState()
        {
            if (toggleImage == null)
            {
                Debug.LogWarning("[ToggleSwitch] toggleImage null", this);
                return;
            }

            bool visualOn = isReversed ? !isOn : isOn;
            Color targetColor = visualOn ? onColor : offColor;

            toggleImage.color = targetColor;

            if (gameObject.activeInHierarchy)
            {
                AnimateColorChange(targetColor);
            }
        }

        private void AnimateColorChange(Color targetColor)
        {
            if (!gameObject.activeInHierarchy) return;

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
            animationCoroutine = StartCoroutine(FadeToColor(targetColor));
        }


        private IEnumerator FadeToColor(Color target)
        {
            Color start = toggleImage.color;
            float elapsed = 0f;
            float duration = 0.15f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                toggleImage.color = Color.Lerp(start, target, t);
                yield return null;
            }
            toggleImage.color = target;
            animationCoroutine = null;
        }

        private void PlayToggleSound()
        {
            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.PlayOneShot(audioSource.clip);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshVisualState();
        }

        [ContextMenu("Toggle ON")]
        private void CtxToggleOn() => SetIsOn(true);

        [ContextMenu("Toggle OFF")]
        private void CtxToggleOff() => SetIsOn(false);

        [ContextMenu("Force Refresh")]
        private void CtxForceRefresh() => RefreshVisualState();
#endif
    }
}