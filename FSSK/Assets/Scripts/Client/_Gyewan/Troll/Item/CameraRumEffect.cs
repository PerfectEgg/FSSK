using UnityEngine;
using Photon.Pun;
using System.Collections;

public class CameraRumEffect : MonoBehaviourPun
{
    [Header("럼주 이펙트 메테리얼")]
    [Tooltip("화면 일렁임(Glitch) 효과가 적용된 메테리얼")]
    [SerializeField] private Material _glitchMaterial;

    [Header("디버프 설정")]
    [SerializeField] private float _maxDuration = 8f;         // 기본 지속 시간
    [SerializeField] private float _holdSpeedMultiplier = 4f; // AD 홀드 시 시간이 줄어드는 배속 (4배속)

    [Header("글리치(Glitch) 세부 설정")]
    [SerializeField] private float _noiseAmount = 2f;
    [SerializeField] private float _glitchStrength = 2.5f;
    [SerializeField] private float _scanLinesStrength = 1f;

    // 코루틴 내부에서 목표로 할 강도
    private float _targetIntensity = 0f;
    private float _currentIntensity = 0f;
    private float _currentTimer = 0f;

    void Awake() => SetMaterialProperties(0f);

    private void OnEnable()
    {
        // 🟢 플레이어의 RPC 수신 스크립트가 터뜨리는 이벤트를 구독
        TrollEvents.OnHitByRum += HandleHitByRum;
        GameEvents.OnGameOverTriggered += HandleGameOver;
    }

    private void OnDisable()
    {
        TrollEvents.OnHitByRum -= HandleHitByRum;
        GameEvents.OnGameOverTriggered -= HandleGameOver;
        SetMaterialProperties(0f);
    }

    private void HandleHitByRum()
    {
        // 🟢 [로컬 처리 핵심] RPC를 보낸 건 상대방이지만, 소환은 내 컴퓨터에서만 합니다!
        if (!photonView.IsMine) return;

        StopAllCoroutines(); // 이전 타이머가 돌고 있다면 중단 (중첩 방지)
        
        StartCoroutine(RumDebuffCoroutine());
    }

    private void HandleGameOver()
    {
        if (!photonView.IsMine) return;

        StopAllCoroutines();
        ClearEffect();
    }

    private IEnumerator RumDebuffCoroutine()
    {
        _currentTimer = _maxDuration;
        
        // 🟢 시작 시 최대 어지러움(2.5f)으로 세팅
        _currentIntensity = _glitchStrength; 
        _targetIntensity = _glitchStrength;

        Debug.Log($"🥃 [럼 피격] 찰싹! 화면에 럼이 붙었습니다! AD 연타하세요! (8초)");

        // 1. 디버프 타이머 및 해제 루프 시작
        while (_currentTimer > 0f)
        {
            // 기본적으로 흐르는 시간
            float timeToDecrease = Time.deltaTime;

            // 🟢 AD 키 홀드 감지 (Input.GetKey 사용)
            bool isHoldingAD = Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D);

            if (isHoldingAD)
            {
                // 🟢 누르고 있을 때: 시간은 빨리 닳고, 목표 강도는 최소치(1f)로 설정!
                timeToDecrease *= _holdSpeedMultiplier; 
                _targetIntensity = 1f; 
            }
            else
            {
                // 🟢 안 누를 때: 목표 강도를 다시 최대치(2.5f)로 복구!
                _targetIntensity = _glitchStrength; 
            }

            _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, Time.deltaTime * 10f);

            // 메테리얼에 값 적용
            SetMaterialProperties(_currentIntensity);

            _currentTimer -= timeToDecrease;

            // 🟢 UI 게이지 바가 있다면 업데이트 (예: image.fillAmount = _currentTimer / _maxDuration)

            yield return null; // 다음 프레임까지 대기
        }

        // 2. 디버프 종료 및 정리
        ClearEffect();
    }

    // 🟢 쉐이더 속성을 한 번에 관리하는 헬퍼 함수
    private void SetMaterialProperties(float intensity)
    {
        if (_glitchMaterial == null) return;

        _glitchMaterial.SetFloat("_Intensity", intensity);
        _glitchMaterial.SetFloat("_NoiseAmount", _noiseAmount);
        _glitchMaterial.SetFloat("_GlitchStrength", _glitchStrength);
        _glitchMaterial.SetFloat("_ScanLineStrength", _scanLinesStrength);
    }

    private void ClearEffect()
    {
        Debug.Log("✨ [럼주 해제] 술기운이 깼습니다. 시야가 완전히 정상으로 돌아왔습니다.");

        // 화면 일렁임 효과 완전히 제거
        SetMaterialProperties(0f);

        // 🟢 UI 게이지 바 끄기
    }
}
