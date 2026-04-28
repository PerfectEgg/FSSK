using System;
using UnityEngine;

public static class GameEvents
{
    // 트롤 관련 이벤트: (상태, 대상 오브젝트)
    public static Action<bool, GameObject> OnTrollInteraction;
    
    // 아이템 관련 이벤트: (아이템 타입, 대상 오브젝트)
    public static Action<string, GameObject> OnItemCollected;
    
    // 카메라 모드 변경 이벤트: (확장 모드 여부)
    public static Action<bool> OnExpansionModeChanged;

    // 바다 게: 빈자리를 요청하는 이벤트 (반환값이 Vector3)
    public static Func<Vector3> RequestSafePosition;

    // 바다 게: 자리를 다 쓰고 반납하는 이벤트
    public static Action<Vector3> OnPositionReleased;

    // 세이렌: 매혹 동안 확장 시야 고정 이벤트
    public static Action<bool, Transform> OnSirenEffect;

    // 크라켄 & 세이렌: 기절 시 정지 이벤트
    public static Action<float> OnStunEffect;

    // 바다 게: 편의성을 위한 래퍼 함수들
    public static Vector3 GetSafePosition() 
    {
        // 구독자가 있으면 호출해서 반환받고, 아무도 없으면 (0,0,0) 반환
        return RequestSafePosition != null ? RequestSafePosition.Invoke() : Vector3.zero;
    }

    

    public static void TriggerTrollInteraction(bool isGrabbed, GameObject obj) => OnTrollInteraction?.Invoke(isGrabbed, obj);
    public static void TriggerItemCollected(string itemTag, GameObject obj) => OnItemCollected?.Invoke(itemTag, obj);
    public static void TriggerExpansionMode(bool isExpansion) => OnExpansionModeChanged?.Invoke(isExpansion);
    public static void TriggerPositionReleased(Vector3 pos) => OnPositionReleased?.Invoke(pos);
}