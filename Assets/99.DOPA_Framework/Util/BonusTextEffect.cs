using System.Collections;
using TMPro;
using UnityEngine;

public class BonusTextEffect : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI bonusText; // TextMeshPro 텍스트 컴포넌트
    private float animationTime = 2f; // 애니메이션 지속 시간
    private float elapsedTime = 0f;
    [SerializeField] private float fontSize = 0f;

    void Start()
    {
        // bonusText.text = "BONUS!";
        fontSize = bonusText.fontSize;
        bonusText.fontSize = fontSize; // 텍스트 크기 조정
        bonusText.color = Color.yellow; // 초기 색상 설정
    }

    public void AnimationStart()
    {
        StartCoroutine(AnimateBonusText());
    }

    IEnumerator AnimateBonusText()
    {
        Vector3 originalPosition = bonusText.transform.position;

        while (elapsedTime < animationTime)
        {
            elapsedTime += Time.deltaTime;

            // 텍스트 흔들림 효과 (위아래로 움직임)
            float shakeAmount = Mathf.Sin(elapsedTime * 10) * 5f;
            bonusText.transform.position = originalPosition + new Vector3(0, shakeAmount, 0);

            // 색상 변화 효과 (무지개 색상)
            float t = Mathf.PingPong(elapsedTime, 1f);
            bonusText.color = Color.Lerp(Color.yellow, Color.red, t);

            yield return null;
        }

        // 애니메이션 종료 후 원래 위치로 복구
        bonusText.transform.position = originalPosition;
    }
}