using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverPopup : PopupBase
{
    [SerializeField] protected TextMeshProUGUI contentText;
    [SerializeField] protected TextMeshProUGUI resultColor;
    [SerializeField] protected List<Color> resultColorList = new();

    public override void Open(string title = "", string content = "")
    {
        base.Open(title, content);

        if (!string.IsNullOrEmpty(content))
            ScoreSetting(content);
    }

    public void ResultSetting(bool isWin = true)
    {
        if (resultColor != null && resultColorList.Count > 1)
            resultColor.color = isWin ? resultColorList[0] : resultColorList[1];
    }

    public void ScoreSetting(string scoreStr)
    {
        if(contentText != null)
            contentText.text = scoreStr;
    }

    public override void Close()
    {
        HomeButtonOn();
    }

    public void HomeButtonOn()
    {
        GameStateManager.Instance.ChangeState(GameState.Home);
        SoundManager.Instance.PlayEffect("BUTTON_04");

        base.Close();
    }

    public void RestartButtonOn()
    {
        HomeButtonOn();
    }
}
