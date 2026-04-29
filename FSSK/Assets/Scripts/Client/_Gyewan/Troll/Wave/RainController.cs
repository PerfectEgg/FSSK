using UnityEngine;
using System.Collections.Generic;

public class RainController : MonoBehaviour
{
    // 지정된 레벨 프로세스
    private List<int> _levelProgression = new List<int> { 0, 0, 1, 1, 1, 2, 2, 2, 3, 3 };

    private int _currentLevel = 0;  // 현재 레벨

    private void OnEnable() => GameEvents.OnWaveStageChanged += HandleWaveStage;
    private void OnDisable() => GameEvents.OnWaveStageChanged -= HandleWaveStage;

    private void HandleWaveStage(int stage)
    {
        // 🟢 1. List 범위를 초과하는 무한 웨이브 방지용 안전장치 (최대 단계 유지)
        int targetIndex = Mathf.Min(stage, _levelProgression.Count - 1);
        
        // 🟢 2. 조건문 없이 인덱스로 값을 바로 뽑아냅니다! (O(1) 성능)
        int newLevel = _levelProgression[targetIndex];

        // 🟢 3. 레벨이 이전과 달라졌을 때만 연출을 갱신합니다.
        if (_currentLevel != newLevel)
        {
            _currentLevel = newLevel;
            ApplRainingEffect(_currentLevel);
        }
    }

    private void ApplRainingEffect(int level)
    {
        if (level == 0) return;

        Debug.Log($"[비] {level}단계 번개 코루틴을 시작합니다!");
        // TODO: 레벨에 따른 Fade In/Out 및 암전 코루틴 실행
    }
}