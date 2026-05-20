using Framework.Game.UI.Controls;
using UnityEngine;
using UnityEngine.UI;

public class SettingPopup : PopupBase
{
    public ToggleSwitch toggleSFX;
    public ToggleSwitch toggleBGM;
    public ToggleSwitch toggleHaptic;

    protected override void InitializeComponents()
    {
        base.InitializeComponents();

        toggleSFX.SetIsOn(SoundManager.Instance.EffectVolume == 1, false);
        toggleBGM.SetIsOn(SoundManager.Instance.BGMVolume == 1, false);
        toggleHaptic.SetIsOn(HapticManager.IsHapticEnabled, false);

        toggleSFX.OnValueChanged.AddListener(ToggleSFXClick);
        toggleBGM.OnValueChanged.AddListener(ToggleBGMClick);
        toggleHaptic.OnValueChanged.AddListener(ToggleHapticClick);
    }

    public void HomeButtonOn()
    {
        GameManager.Instance.ReturnToHome();
        SoundManager.Instance.PlayEffect("BUTTON_04");

        base.Close();
    }

    public void ToggleSFXClick(bool isOn)
    {
        SoundManager.Instance.PlayEffect("BUTTON_02");

        SoundManager.Instance.SetEffectVolume(isOn ? 1f : 0f);
    }

    public void ToggleBGMClick(bool isOn)
    {
        SoundManager.Instance.PlayEffect("BUTTON_02");

        SoundManager.Instance.SetBGMVolume(isOn ? 1f : 0f);
    }

    public void ToggleHapticClick(bool isOn)
    {
        SoundManager.Instance.PlayEffect("BUTTON_02");

        Debug.Log("SettingPopup ToggleHapticClick toggleHaptic.isOn : " + toggleHaptic.IsOn);
        Debug.Log("SettingPopup ToggleHapticClick isOn : " + isOn);

        HapticManager.SetHapticEnabled(toggleHaptic.IsOn);
    }


    protected override void OnDestroy()
    {
        toggleSFX?.OnValueChanged.RemoveListener(ToggleSFXClick);
        toggleBGM?.OnValueChanged.RemoveListener(ToggleBGMClick);
        toggleHaptic?.OnValueChanged.RemoveListener(ToggleHapticClick);

        base.OnDestroy();
    }

}
