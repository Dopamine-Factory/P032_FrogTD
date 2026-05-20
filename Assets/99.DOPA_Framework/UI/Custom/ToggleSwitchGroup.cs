using UnityEngine;
using System.Collections.Generic;

namespace Framework.Game.UI.Controls
{
    /// <summary>
    /// ToggleSwitch들을 그룹화. 라디오 버튼처럼 동작.
    /// </summary>
    public class ToggleSwitchGroup : MonoBehaviour
    {
        [SerializeField] private List<ToggleSwitch> toggles = new List<ToggleSwitch>();
        private ToggleSwitch currentToggle;

        private void Awake()
        {
            InitializeGroup();
        }

        private void InitializeGroup()
        {
            foreach (var toggle in toggles)
            {
                if (toggle == null) continue;
                toggle.OnValueChanged.AddListener(OnToggleValueChanged);
            }
        }

        /// <summary>
        /// ToggleSwitch에서 호출: 그룹에 "나 켜짐" 알림.
        /// </summary>
        public void NotifyToggleOn(ToggleSwitch sender, bool isOn)
        {
            if (!isOn || sender == currentToggle) return;

            // 다른 토글 OFF
            foreach (var toggle in toggles)
            {
                if (toggle != null && toggle != sender && toggle.IsOn)
                {
                    toggle.SetIsOn(false, false); // 무한 루프 방지 위해 invokeEvent=false
                }
            }

            currentToggle = sender;
        }

        private void OnToggleValueChanged(bool isOn)
        {
            // 백업 로직 (NotifyToggleOn이 호출되지 않을 때)
            foreach (var toggle in toggles)
            {
                if (toggle != null && toggle.IsOn && toggle != currentToggle)
                {
                    currentToggle = toggle;
                    break;
                }
            }
        }

        public void RegisterToggle(ToggleSwitch toggle)
        {
            if (!toggles.Contains(toggle))
            {
                toggles.Add(toggle);
                toggle.OnValueChanged.AddListener(OnToggleValueChanged);
            }
        }

        public void UnregisterToggle(ToggleSwitch toggle)
        {
            if (toggles.Contains(toggle))
            {
                toggles.Remove(toggle);
                toggle.OnValueChanged.RemoveListener(OnToggleValueChanged);
            }
        }

        private void OnDestroy()
        {
            foreach (var toggle in toggles)
            {
                toggle?.OnValueChanged.RemoveListener(OnToggleValueChanged);
            }
        }
    }
}