using UnityEngine;

/// <summary>
/// 기준 해상도의 가로폭을 항상 동일하게 유지하도록
/// Camera.orthographicSize를 런타임에 자동 조정합니다.
///
/// 세로형 모바일 게임 기준:
///   - 어떤 기기든 게임 월드의 가로 범위가 동일하게 보임
///   - 세로가 긴 폰일수록 위아래가 더 많이 보임 (자연스러운 확장)
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraAspectFitter : MonoBehaviour
{
    [Header("기준 해상도 (디자인 기준)")]
    [SerializeField] private int referenceWidth  = 720;
    [SerializeField] private int referenceHeight = 1280;

    [Header("기준 orthoSize (referenceResolution 기준)")]
    [SerializeField] private float referenceOrthoSize = 5f;

    private Camera _camera;
    private int    _lastWidth;
    private int    _lastHeight;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        Apply();
    }

    private void Update()
    {
        // 화면 크기가 바뀔 때만 재계산 (에디터 리사이즈, 기기 회전 대응)
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            Apply();
    }

    private void Apply()
    {
        _lastWidth  = Screen.width;
        _lastHeight = Screen.height;

        float referenceAspect = (float)referenceWidth / referenceHeight;
        float currentAspect   = (float)Screen.width   / Screen.height;

        // 가로폭 고정: 기준 aspect보다 현재가 좁으면 orthoSize를 키워서 가로를 맞춤
        _camera.orthographicSize = referenceOrthoSize * (referenceAspect / currentAspect);

        Debug.Log(string.Format(
            "[CameraAspectFitter] {0}x{1} (aspect={2:F3}) -> orthoSize={3:F2}",
            Screen.width, Screen.height, currentAspect, _camera.orthographicSize));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        Apply();
    }
#endif
}
