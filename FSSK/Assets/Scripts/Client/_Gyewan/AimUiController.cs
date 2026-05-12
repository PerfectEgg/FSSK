using UnityEngine;

public class AimUIController : MonoBehaviour
{
    [Header("에임 UI 오브젝트")]
    [SerializeField] private GameObject _aimDotUI; // 방금 만든 AimDot 이미지를 연결하세요

    private void OnEnable()
    {
        // 확장 시야 모드가 켜지거나 꺼질 때 발생하는 이벤트를 구독
        TrollEvents.OnExpansionModeChanged += HandleExpansionMode;
    }

    private void OnDisable()
    {
        // 메모리 누수 방지를 위해 이벤트 구독 해제
        TrollEvents.OnExpansionModeChanged -= HandleExpansionMode;
    }

    private void HandleExpansionMode(bool isExpansionMode)
    {
        if (_aimDotUI != null)
        {
            // 확장 모드가 켜지면 에임도 켜지고, 꺼지면 에임도 꺼집니다.
            _aimDotUI.SetActive(isExpansionMode);
            
            if (isExpansionMode)
            {
                Debug.Log("🎯 확장 시야 ON: 에임 점 활성화!");
            }
        }
    }
}