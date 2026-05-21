using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragMergeSlotUI : MonoBehaviour
{
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI lvTxt;

    public WeaponData Data { get; private set; } = new WeaponData();

    RectTransform rectTransform;
    public MergeBoard Board;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Show(WeaponType type, int level)
    {
        Data = new WeaponData { weaponType = type, level = level };
        weaponIcon.sprite = Data.GetSprite();
        lvTxt.text = $"Lv.{Data.level}";
        gameObject.SetActive(true);
    }

    public void Clear()
    {
        gameObject.SetActive(false);
    }

    public void UpdatePosition(PointerEventData eventData)
    {
        // 1. 현재 DragMergeSlotUI의 부모 RectTransform을 가져옵니다.
        RectTransform parentRect = rectTransform.parent as RectTransform;

        // 2. 화면 좌표(eventData.position)를 부모 기준의 로컬 좌표로 변환합니다.
        // (Canvas가 Screen Space - Overlay인 경우 카메라는 null이 들어가도 무방하며, 
        //  eventData.pressEventCamera가 알아서 적절한 카메라/null 값을 넘겨줍니다.)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector2 localPointerPosition))
        {
            // 3. 변환된 로컬 좌표를 내 위치로 설정합니다.
            rectTransform.localPosition = localPointerPosition;
        }
    }
}
