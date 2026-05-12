using System;
using UnityEngine;

// 게임 진행 관련 이벤트
public static class GameEvents
{
    // 게임 오버 트리거 (모든 기믹 정지, 입력 제한용)
    public static Action OnGameOverTriggered;

    // 게임 결과 전달 (개인 클라이언트 승패 여부 UI 표시용)
    // 매개변수 bool isWin: 내가 이겼으면 true, 졌으면 false
    public static Action<bool> OnGameOverResult;

    public static void TriggerGameOver()
    {
        OnGameOverTriggered?.Invoke();
    }

    public static void TriggerGameOverResult(bool isWin)
    {
        OnGameOverResult?.Invoke(isWin);
    }
}
