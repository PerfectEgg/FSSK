using UnityEngine;
using Photon.Pun;
using System.Collections;

public class CameraOctopusEffect : MonoBehaviourPun
{
    [Header("프리팹 설정 (Layer: LocalEffect 필수)")]
    [SerializeField] private GameObject _screenOctopusPrefab; // 1단계에서 만든 프리팹 등록

    [Header("문어 배치 설정")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, -0.5f, 1.5f); // 카메라 기준 스폰 위치
    [SerializeField] private Vector3 _spawnRotation = new Vector3(90f, 0f, 0f); // 카메라를 바라보게 회전

    [Header("디버프 설정")]
    [SerializeField] private float _maxDuration = 8f;         // 기본 지속 시간
    [SerializeField] private float _mashReduction = 0.5f;     // AD 연타 1회당 단축 시간 (초)

    [Header("사운드 설정")]
    [SerializeField] private AudioClip _octopusHitSound;   // 적중 사운드

    private GameObject _currentOctopus; // 현재 화면에 붙은 문어 인스턴스
    private float _currentTimer = 0f;
    private Transform _mainCameraTransform; // 메인 카메라 위치 정보

    private void Awake()
    {
        // 씬에 있는 메인 카메라를 찾아둡니다.
        if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        // 🟢 플레이어의 RPC 수신 스크립트가 터뜨리는 이벤트를 구독
        TrollEvents.OnHitByOctopus += HandleHitByOctopus;
    }

    private void OnDisable()
    {
        TrollEvents.OnHitByOctopus -= HandleHitByOctopus;
        
        // 스크립트가 꺼질 때 문어가 남아있다면 정리
        if (_currentOctopus != null) Destroy(_currentOctopus);
    }

    private void HandleHitByOctopus()
    {
        // 🟢 [로컬 처리 핵심] RPC를 보낸 건 상대방이지만, 소환은 내 컴퓨터에서만 합니다!
        if (!photonView.IsMine) return;

        StopAllCoroutines(); // 이전 타이머가 돌고 있다면 중단 (중첩 방지)
        
        StartCoroutine(OctopusDebuffCoroutine());
    }

    private IEnumerator OctopusDebuffCoroutine()
    {
        _currentTimer = _maxDuration;

        Debug.Log($"🐙 [문어 피격] 찰싹! 화면에 문어가 붙었습니다! AD 연타하세요! (8초)");

        // 1. 이미 문어가 있다면 삭제 (지속 시간 갱신)
        if (_currentOctopus != null) Destroy(_currentOctopus);

        // 2. 카메라 바로 앞에 문어 소환
        if (_mainCameraTransform != null && _screenOctopusPrefab != null)
        {
            // 카메라 위치/회전을 기준으로 오프셋을 계산하여 소환
            Vector3 spawnPos = _mainCameraTransform.TransformPoint(_spawnOffset);
            Quaternion spawnRot = _mainCameraTransform.rotation * Quaternion.Euler(_spawnRotation);

            // 🟢 [멀티플레이 핵심] Instantiate를 사용하여 로컬에서만 생성합니다. (RPC 소모 X)
            _currentOctopus = Instantiate(_screenOctopusPrefab, spawnPos, spawnRot);
            
            // 문어가 카메라를 따라다니게 만들기 위해 자식으로 집어넣습니다.
            _currentOctopus.transform.SetParent(_mainCameraTransform);
        }

        // 🟢 UI 게이지 바가 있다면 여기서 8초로 초기화하여 띄우면 됩니다.

        // 3. 디버프 타이머 및 해제 루프 시작
        while (_currentTimer > 0f)
        {
            _currentTimer -= Time.deltaTime;

            // AD 키 연타 감지 (Input.GetKeyDown 사용)
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D))
            {
                _currentTimer -= _mashReduction; // 시간 단축
                Debug.Log($"👊 [연타!] 남은 시간: {_currentTimer:F1}초");
                // TODO: UI Hit! 연출, 문어 움찔거리는 애니메이션 재생
            }

            // 🟢 UI 게이지 바가 있다면 업데이트 (예: image.fillAmount = _currentTimer / _maxDuration)

            yield return null; // 다음 프레임까지 대기
        }

        // 4. 디버프 종료 및 정리
        ClearEffect();
    }

    private void ClearEffect()
    {
        Debug.Log("✨ [문어 해제] 시야가 정상으로 돌아왔습니다.");

        // 문어 오브젝트 삭제
        if (_currentOctopus != null)
        {
            Destroy(_currentOctopus);
            _currentOctopus = null;
        }

        // 🟢 UI 게이지 바 끄기
    }
}