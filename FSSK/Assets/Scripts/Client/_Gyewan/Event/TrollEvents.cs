using System;
using UnityEngine;

// 트롤 및 환경, 웨이브 관련 이벤트
public static class TrollEvents
{
    // 게임 종료 이후 새 트롤/환경 이벤트가 실행되지 않도록 막는 공통 게이트
    public static bool IsGameplayEventBlocked => GameEvents.IsGameOver;

    // 시간초과 종료 연출 중에는 물 잠김 연출을 위해 웨이브 단계 이벤트만 잠깐 허용
    public static bool IsWaveStageEventBlocked =>
        GameEvents.IsGameOver && !GameEvents.AllowsTimeoutWaveEvents;

    private static bool IsWaveStageBlocked => IsWaveStageEventBlocked;

    // 아이템 관련 이벤트: (아이템 타입, 대상 오브젝트)
    public static Action<string, GameObject> OnItemCollected;
    public static void TriggerItemCollected(string itemTag, GameObject obj)
    {
        if (IsGameplayEventBlocked) return;
        OnItemCollected?.Invoke(itemTag, obj);
    }

    // 카메라 모드 변경 이벤트: (확장 모드 여부)
    public static Action<bool> OnExpansionModeChanged;
    public static void TriggerExpansionMode(bool isExpansion)
    {
        if (IsGameplayEventBlocked && isExpansion) return;
        OnExpansionModeChanged?.Invoke(isExpansion);
    }

    // 쥐 1단계: 쥐가 매니저에게 "훔칠 돌 정보(색상, 좌표)"를 요청하는 이벤트
    public static Action OnRequestStoneToSteal;
    public static void TriggerRequestStoneToSteal()
    {
        if (IsGameplayEventBlocked) return;
        OnRequestStoneToSteal?.Invoke();
    }

    // 쥐 1.5단계: 매니저가 쥐에게 정보를 전달하는 콜백 이벤트 (1: 흑돌, 2: 백돌, 좌표)
    public static Action<int, Transform> OnStoneTargetCallback;
    public static void TriggerStoneTargetCallback(int color, Transform pos)
    {
        if (IsGameplayEventBlocked) return;
        OnStoneTargetCallback?.Invoke(color, pos);
    }

    // 쥐 2단계: 쥐가 목적지에 도착해서 "이 좌표의 돌을 지워줘"라고 요청하는 이벤트
    public static Action OnExecuteSteal;
    public static void TriggerExecuteSteal()
    {
        if (IsGameplayEventBlocked) return;
        OnExecuteSteal?.Invoke();
    }

    // 바다 게: 빈자리를 요청하는 이벤트 (반환값이 Vector3)
    public static Func<Vector3> RequestSafePosition;
    // 바다 게: 편의성을 위한 래퍼 함수
    public static Vector3 GetSafePosition()
    {
        if (IsGameplayEventBlocked) return Vector3.zero;
        return RequestSafePosition != null ? RequestSafePosition.Invoke() : Vector3.zero;
    }

    // 바다 게: 자리를 다 쓰고 반납하는 이벤트
    public static Action<Vector3> OnPositionReleased;
    public static void TriggerPositionReleased(Vector3 pos)
    {
        if (IsGameplayEventBlocked) return;
        OnPositionReleased?.Invoke(pos);
    }

    // 크라켄: 공격 시작 시 강제 확장 모드 요청 이벤트
    public static Action OnEnterExpansionModeRequest;

    // 크라켄: "오른쪽(true)에서 공격하는데 피했니?" 라고 물어보면, bool(성공 여부)로 대답하는 이벤트
    public static Func<bool, bool> OnKrakenDodgeCheck;

    // 세이렌: 매혹 동안 확장 시야 고정 이벤트
    public static Action<bool, Transform> OnSirenEffect;
    public static void TriggerSirenEffect(bool isSinging, Transform target)
    {
        if (IsGameplayEventBlocked) return;
        OnSirenEffect?.Invoke(isSinging, target);
    }

    // 크라켄 & 세이렌: 기절 시 정지 이벤트
    public static Action<float> OnStunEffect;
    public static void TriggerStunEffect(float duration)
    {
        if (IsGameplayEventBlocked) return;
        OnStunEffect?.Invoke(duration);
    }

    // 아이템 피격 이벤트 (플레이어가 아이템에 맞았을 때 트롤이 발동하는 효과를 처리하기 위한 이벤트)
    // 럼주 피격 이벤트
    public static Action OnHitByRum;
    public static void TriggerHitByRum()
    {
        if (IsGameplayEventBlocked) return;
        OnHitByRum?.Invoke();
    }

    // 문어 피격 이벤트
    public static Action OnHitByOctopus;
    public static void TriggerHitByOctopus()
    {
        if (IsGameplayEventBlocked) return;
        OnHitByOctopus?.Invoke();
    }

    // 웨이브 단계 변경 (UI 처리 용)
    public static Action<int> OnWaveStageChanged;
    public static void TriggerWaveStageChanged(int stage)
    {
        if (IsWaveStageBlocked) return;
        OnWaveStageChanged?.Invoke(stage);
    }

    // 비 강도 변경 (0~3)
    public static Action<int> OnRainLevelChanged;
    public static void TriggerRainLevelChanged(int level)
    {
        if (IsGameplayEventBlocked) return;
        OnRainLevelChanged?.Invoke(level);
    }

    // 바람 강도 변경 (0~3)
    public static Action<int> OnWindLevelChanged;
    public static void TriggerWindLevelChanged(int level)
    {
        if (IsGameplayEventBlocked) return;
        OnWindLevelChanged?.Invoke(level);
    }

    // 번개 강도 변경 (0~3)
    public static Action<int> OnLightningLevelChanged;
    public static void TriggerLightningLevelChanged(int level)
    {
        if (IsGameplayEventBlocked) return;
        OnLightningLevelChanged?.Invoke(level);
    }

    // 번개 단발 연출 요청 이벤트: (번개 레벨, 패턴 인덱스)
    public static Action<int, int> OnLightningStrikeRequested;
    public static void TriggerLightningStrikeRequested(int level, int patternIndex)
    {
        if (IsGameplayEventBlocked) return;
        OnLightningStrikeRequested?.Invoke(level, patternIndex);
    }

    // 트롤링 이벤트 종료 알림 (이 이벤트가 호출되면 매니저가 대기 타이머를 재시작함)
    public static Action OnTrollFinished;
    public static void TriggerTrollFinished()
    {
        if (IsGameplayEventBlocked) return;
        OnTrollFinished?.Invoke();
    }
}
