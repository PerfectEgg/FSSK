using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using IEnumerator = System.Collections.IEnumerator;
using System.Collections.Generic;

public class LightningController : MonoBehaviour
{
    [System.Serializable]
    public struct LightningPattern
    {
        public float minInterval;    // 최소 대기 시간
        public float maxInterval;    // 최대 대기 시간
        public float fadeOutTime;    // 어두워지는 시간
        public float blockTime;      // 암전 유지 시간
        public float fadeInTime;     // 밝아지는 시간
        public AudioClip thunderSound; // 천둥 소리 (약/중/강)
    }

    [Header("UI 및 연출 설정")]
    [SerializeField] private Image _blackoutPanel; 
    [SerializeField] private AudioSource _audioSource;

    [Header("단계별 패턴 풀 (Inspector에서 설정)")]
    // 💡 기획서 수치대로 기본값을 세팅해두었습니다. 인스펙터에서 밸런스 수정이 가능합니다.
    [SerializeField] private LightningPattern _pattern1;
    [SerializeField] private LightningPattern _pattern2;
    [SerializeField] private LightningPattern _pattern3;

    // 지정된 레벨 프로세스
    private List<int> _levelProgression = new List<int> { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3 };

    private int _currentLevel = 0;  // 현재 레벨
    private Coroutine _lightningCoroutine;

    private void OnEnable() => TrollEvents.OnWaveStageChanged += HandleWaveStage;
    private void OnDisable() => TrollEvents.OnWaveStageChanged -= HandleWaveStage;

    void Start()
    {
        if (_blackoutPanel != null)
        {
            _blackoutPanel.color = new Color(0, 0, 0, 0);
            _blackoutPanel.gameObject.SetActive(false);
        }
    }

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
            ApplyLightningEffect(_currentLevel);
        }
    }

    private void ApplyLightningEffect(int level)
    {
        if (_lightningCoroutine != null) StopCoroutine(_lightningCoroutine);
        
        if (_blackoutPanel != null)
        {
            _blackoutPanel.DOKill(); 
            _blackoutPanel.color = new Color(0, 0, 0, 0);
            _blackoutPanel.gameObject.SetActive(false);
        }

        if (level == 0) return;

        Debug.Log($"[번개] {level}단계 번개 코루틴을 시작합니다!");
        _lightningCoroutine = StartCoroutine(LightningRoutine(level));
    }

    private IEnumerator LightningRoutine(int level)
    {
        while (true)
        {
            // 🟢 현재 레벨에 맞춰 추가된 패턴들 중 하나를 뽑아옵니다.
            LightningPattern selectedPattern = GetRandomPattern(level);

            float waitTime = Random.Range(selectedPattern.minInterval, selectedPattern.maxInterval);
            yield return new WaitForSeconds(waitTime);

            yield return StartCoroutine(ExecuteLightningSequence(selectedPattern));
        }
    }

    // 🟢 가장 핵심이 되는 기획 로직 구현부
    private LightningPattern GetRandomPattern(int level)
    {
        // 안전장치: 최대 3단계로 제한
        int maxPatternIndex = Mathf.Clamp(level, 1, 3);
        
        // Random.Range(min, max)에서 int max는 Exclusive(제외)이므로 +1 해줍니다.
        // - 1단계: Range(1, 2) -> 무조건 1 반환
        // - 2단계: Range(1, 3) -> 1, 2 중 랜덤 반환
        // - 3단계: Range(1, 4) -> 1, 2, 3 중 랜덤 반환
        int randomIndex = Random.Range(1, maxPatternIndex + 1);

        switch (randomIndex)
        {
            case 1: return _pattern1;
            case 2: return _pattern2;
            case 3: return _pattern3;
            default: return _pattern1;
        }
    }

    private IEnumerator ExecuteLightningSequence(LightningPattern pattern)
    {
        _blackoutPanel.gameObject.SetActive(true);
        
        // 🟢 1. 시작은 하얀색의 완전 투명 상태로 세팅
        _blackoutPanel.color = new Color(1f, 1f, 1f, 0f);
        
        Sequence lightningSeq = DOTween.Sequence();

        // 🟢 2. 번쩍! (아주 짧은 시간 동안 하얀색 + 낮은 알파값으로 변경)
        // 알파값이 0.3 ~ 0.5 정도면 눈이 아프지 않으면서도 화면 전체가 번쩍이는 느낌을 줍니다.
        float flashTime = 0.1f; 
        lightningSeq.Append(_blackoutPanel.DOColor(new Color(1f, 1f, 1f, 0.4f), flashTime));

        // 번쩍이는 순간에 맞춰 천둥 소리 재생
        lightningSeq.InsertCallback(0f, () => 
        {
            if (_audioSource != null && pattern.thunderSound != null)
                _audioSource.PlayOneShot(pattern.thunderSound);
        });

        // 🟢 3. 시야 차단 (하얀색 -> 검은색으로 부드럽게 변하며 알파값 100% 덮임)
        // 기획된 fadeOutTime 동안 망막이 어두워지는 연출
        lightningSeq.Append(_blackoutPanel.DOColor(new Color(0f, 0f, 0f, 1f), pattern.fadeOutTime).SetEase(Ease.InOutSine));

        // 🟢 4. 암전 유지 (Block)
        lightningSeq.AppendInterval(pattern.blockTime);

        // 🟢 5. 시야 복구 (검은색을 유지한 채로 다시 투명해짐)
        lightningSeq.Append(_blackoutPanel.DOColor(new Color(0f, 0f, 0f, 0f), pattern.fadeInTime).SetEase(Ease.InOutSine));

        yield return lightningSeq.WaitForCompletion();
        
        _blackoutPanel.gameObject.SetActive(false);
    }
}