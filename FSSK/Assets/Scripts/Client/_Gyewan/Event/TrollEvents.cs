using System;
using UnityEngine;

// 트롤 및 환경, 웨이브 관련 이벤트
public static class TrollEvents
{
    // 트롤 관련 이벤트: (상태, 대상 오브젝트)
    public static Action<bool, GameObject> OnTrollInteraction;
    public static void TriggerTrollInteraction(bool isGrabbed, GameObject obj) => OnTrollInteraction?.Invoke(isGrabbed, obj);
    
    // 아이템 관련 이벤트: (아이템 타입, 대상 오브젝트)
    public static Action<string, GameObject> OnItemCollected;
    public static void TriggerItemCollected(string itemTag, GameObject obj) => OnItemCollected?.Invoke(itemTag, obj);
    
    // 카메라 모드 변경 이벤트: (확장 모드 여부)
    public static Action<bool> OnExpansionModeChanged;
    public static void TriggerExpansionMode(bool isExpansion) => OnExpansionModeChanged?.Invoke(isExpansion);

    // 쥐 1단계: 쥐가 매니저에게 "훔칠 돌 정보(색상, 좌표)"를 요청하는 이벤트
    public static Action OnRequestStoneToSteal;
    public static void TriggerRequestStoneToSteal() => OnRequestStoneToSteal?.Invoke();

    // 쥐 1.5단계: 매니저가 쥐에게 정보를 전달하는 콜백 이벤트 (1: 흑돌, 2: 백돌, 좌표)
    public static Action<int, Transform> OnStoneTargetCallback;
    public static void TriggerStoneTargetCallback(int color, Transform pos) => OnStoneTargetCallback?.Invoke(color, pos);

    // 쥐 2단계: 쥐가 목적지에 도착해서 "이 좌표의 돌을 지워줘"라고 요청하는 이벤트
    public static Action OnExecuteSteal;
    public static void TriggerExecuteSteal() => OnExecuteSteal?.Invoke();

    // 바다 게: 빈자리를 요청하는 이벤트 (반환값이 Vector3)
    public static Func<Vector3> RequestSafePosition;

    // 바다 게: 자리를 다 쓰고 반납하는 이벤트
    public static Action<Vector3> OnPositionReleased;
    public static void TriggerPositionReleased(Vector3 pos) => OnPositionReleased?.Invoke(pos);

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

    // 🟢 웨이브 및 환경 변화 이벤트
    public static Action<int> OnWaveStageChanged;       // 웨이브 단계 변경 (UI 처리 용)
    public static Action<int> OnRainLevelChanged;       // 비 강도 변경 (0~3)
    public static Action<int> OnWindLevelChanged;       // 바람 강도 변경 (0~3)
    public static Action<int> OnLightningLevelChanged;  // 번개 강도 변경 (0~3)
}