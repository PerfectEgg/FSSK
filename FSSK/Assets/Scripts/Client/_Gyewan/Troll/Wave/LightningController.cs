using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using IEnumerator = System.Collections.IEnumerator;
using System.Collections.Generic;
using Photon.Pun; // 🟢 포톤 네임스페이스 추가

public class LightningController : MonoBehaviourPun
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
        // 🟢 가장 중요한 핵심! "방장"만 번개의 주기를 계산합니다.
        if (!PhotonNetwork.IsMasterClient) return;

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
            // 1. 방장이 다음 번개의 패턴과 대기 시간을 뽑습니다.
            int maxPatternIndex = Mathf.Clamp(level, 1, 3);
            int randomPatternIndex = Random.Range(1, maxPatternIndex + 1);

            LightningPattern selectedPattern = GetPatternByIndex(randomPatternIndex);
            float waitTime = Random.Range(selectedPattern.minInterval, selectedPattern.maxInterval);
            yield return new WaitForSeconds(waitTime);

            // 2. 대기 시간이 끝나면, 모든 클라이언트에게 "이 패턴으로 연출 재생해!" 하고 소리칩니다.
            photonView.RPC("PlayLightningStrikeRPC", RpcTarget.All, randomPatternIndex);
        }
    }

    // 🟢 가장 핵심이 되는 기획 로직 구현부
    private LightningPattern GetPatternByIndex(int index)
    {
        switch (index)
        {
            case 1: return _pattern1;
            case 2: return _pattern2;
            case 3: return _pattern3;
            default: return _pattern1;
        }
    }

    // 🟢 [추가됨] 사라졌던 RPC 수신 함수 복구! 모두의 화면에서 똑같은 번쩍임을 만들어냅니다.
    [PunRPC]
    private void PlayLightningStrikeRPC(int patternIndex)
    {
        // 진행 중이던 암전 연출이 있다면 강제 초기화
        if (_blackoutPanel != null) _blackoutPanel.DOKill(); 
        
        LightningPattern patternToPlay = GetPatternByIndex(patternIndex);
        StartCoroutine(ExecuteLightningSequence(patternToPlay));
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