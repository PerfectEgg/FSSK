using UnityEngine;
using Photon.Pun;
using System.Collections;

public class CameraRumEffect : MonoBehaviourPun
{
    [Header("럼주 이펙트 설정")]
    [Tooltip("화면 일렁임 쉐이더(또는 Post-Processing Volume)가 있는 오브젝트")]
    [SerializeField] private GameObject _rumShaderEffect;

    [Header("디버프 설정")]
    [SerializeField] private float _maxDuration = 8f;         // 기본 지속 시간
    [SerializeField] private float _holdSpeedMultiplier = 4f; // AD 홀드 시 시간이 줄어드는 배속 (4배속)

    private float _currentTimer = 0f;
    private bool _isEffectActive = false;

    private void OnEnable()
    {
        // 🟢 플레이어의 RPC 수신 스크립트가 터뜨리는 이벤트를 구독
        TrollEvents.OnHitByRum += HandleHitByRum;
    }

    private void OnDisable()
    {
        TrollEvents.OnHitByRum -= HandleHitByRum;
    }

    private void HandleHitByRum()
    {
        // 🟢 [로컬 처리 핵심] RPC를 보낸 건 상대방이지만, 소환은 내 컴퓨터에서만 합니다!
        if (!photonView.IsMine) return;

        StopAllCoroutines(); // 이전 타이머가 돌고 있다면 중단 (중첩 방지)
        
        StartCoroutine(RumDebuffCoroutine());
    }

    private IEnumerator RumDebuffCoroutine()
    {
        _isEffectActive = true;
        _currentTimer = _maxDuration;

        Debug.Log($"🥃 [럼 피격] 찰싹! 화면에 럼이 붙었습니다! AD 연타하세요! (8초)");
        // TODO: 럼 철썩 소리 재생

        // 1. 디버프 타이머 및 해제 루프 시작
        while (_currentTimer > 0f)
        {
            // 기본적으로 흐르는 시간
            float timeToDecrease = Time.deltaTime;

            // 🟢 AD 키 홀드 감지 (Input.GetKey 사용)
            bool isHoldingAD = Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D);

            if (isHoldingAD)
            {
                // 홀드 중: 시간이 더 빨리 닳고, 일렁임 쉐이더를 끕니다 (시야 정상화)
                timeToDecrease *= _holdSpeedMultiplier; 
            }

            // 일렁임 쉐이더 켜기
            if (_rumShaderEffect != null && !_rumShaderEffect.activeSelf)
            {
                _rumShaderEffect.SetActive(true);
            }

            _currentTimer -= timeToDecrease;

            // 🟢 UI 게이지 바가 있다면 업데이트 (예: image.fillAmount = _currentTimer / _maxDuration)

            yield return null; // 다음 프레임까지 대기
        }

        // 2. 디버프 종료 및 정리
        ClearEffect();
    }

    private void ClearEffect()
    {
        _isEffectActive = false;
        Debug.Log("✨ [럼주 해제] 술기운이 깼습니다. 시야가 완전히 정상으로 돌아왔습니다.");

        // 쉐이더 이펙트 끄기
        if (_rumShaderEffect != null)
        {
            _rumShaderEffect.SetActive(false);
        }

        // 🟢 UI 게이지 바 끄기
    }
}