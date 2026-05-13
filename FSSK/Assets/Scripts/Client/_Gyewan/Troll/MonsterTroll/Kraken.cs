using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가

// 크라켄
public class Kraken : MonsterTroll
{
    [Header("크라켄 연출 설정")]
    [SerializeField] private float _enterDuration = 2f;     // 등장에 걸리는 시간
    [SerializeField] private float _attackDelay = 1f;       // 애니메이션 시작 후 타격까지의 대기 시간 (2초~3초 구간)
    [SerializeField] private float _waitDelay = 1f;         // 애니메이션 시작 후 타격까지의 대기 시간 (2초~3초 구간)
    [SerializeField] private float _exitDuration = 3f;      // 퇴장에 걸리는 시간
    [SerializeField] private float _penaltyDuration = 3f; // 스턴 및 암전 시간 (3초)
    [SerializeField] private float _sinkDepth = 20f;        // 아래로 가라앉을 깊이
    [SerializeField] private float _sinkBack = 40f;         // 뒤로 물러날 거리

    [Header("위치 설정")]
   // 위치 캐싱용 변수
    private Vector3 _spawnPos;          // 최종적으로 등장할 위치 (책상 위)
    private Vector3 _startHidingPos;    // 숨는 연출을 위해 잠시 숨겨질 위치 (아래)
    private Vector3 _endHidingPos;      // 공격 판정이 발생하는 위치 (책상 위)

    [Header("사운드 설정")]
    [SerializeField] private AudioClip _enterSound;

    private Animator _animator;
    private readonly string _enterTrigger = "Enter"; // 애니메이션 트리거 이름

    // 언제 착수 모드인지 확인하는 플래그 (기본값은 true라고 가정하거나 초기화 필요)
    private bool _isExpansionMode = false;

    // 이벤트 구독 및 해제
    private void OnEnable() => TrollEvents.OnExpansionModeChanged += HandleModeChanged;
    private void OnDisable() => TrollEvents.OnExpansionModeChanged -= HandleModeChanged;

    private void HandleModeChanged(bool expansionModeActive)
    {
        _isExpansionMode = expansionModeActive;
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    protected override void Start()
    {
        base.Start();

        if (!PhotonNetwork.IsMasterClient) return;

        _spawnPos = transform.position;     // 초기 위치를 등장 위치로 설정

        // 🟢 [핵심] 소환된 위치에 따른 방향 가중치 계산
        // 오른쪽에 있으면 1, 왼쪽에 있으면 -1
        float sideMultiplier = (_spawnPos.x > 0) ? 1f : -1f;
        float rotationMultiplier = (_spawnPos.x > 0) ? 180f : 0f;

        Vector3 lookDirection = new Vector3(0, rotationMultiplier, 0);
        transform.rotation = Quaternion.Euler(lookDirection);

        // 🟢 [위치 보정] 숨는 위치를 '보드 바깥쪽'으로 자동 설정
        // _sinkBack(물러날 거리)에 sideMultiplier를 곱해서 
        // 오른쪽이면 +, 왼쪽이면 - 방향으로 자연스럽게 멀어지게 함
        _startHidingPos = _spawnPos + new Vector3(_sinkBack * sideMultiplier, -_sinkDepth, 0);
        _endHidingPos = _spawnPos + new Vector3(_sinkBack * sideMultiplier * 5f, -_sinkDepth * 20f, 0);

        
        transform.position = _startHidingPos;    // 시작 위치를 숨겨진 곳으로 강제 이동

        Debug.Log("크라켄 등장!! 유의하세요!!");
    }

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void ApplyEffect()
    {
        Debug.Log("💥 [기절] 크라켄에게 한 대 맞았습니다!! 스턴 및 어지러움!");
        
        // TODO: UI 매니저 호출하여 3초 스턴 및 시야 암전 이벤트 발생
        TrollEvents.TriggerStunEffect(_penaltyDuration);
    }

    protected override void OnStateEnter(MonsterState state)
    {
        if (state == MonsterState.Entering)
        {
            TrollEvents.OnEnterExpansionModeRequest?.Invoke();
            TrollEvents.OnShowWarningMessage?.Invoke(MonsterType.Kraken);

            Debug.Log($"<color=cyan>[Kraken]</color> Entering 상태 진입. RPC 발사 시도!");
            photonView.RPC("RPC_EnterSound", RpcTarget.All);
        }

        if (state == MonsterState.Action)
        {
            _animator.SetTrigger(_enterTrigger); // 등장 애니메이션 트리거
            Debug.Log("크라켄이 등장합니다! 회피 준비하세요!");
        }
    }

    protected override void UpdateState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        switch(_currentState)
        {
            case MonsterState.Entering:
                float enterProgress = _currentTime / _enterDuration;
                transform.position = Vector3.Lerp(_startHidingPos, _spawnPos, enterProgress);

                if (_currentTime >= _enterDuration)
                {
                    transform.position = _spawnPos; // 정확한 위치 보정
                    ChangeState(MonsterState.Action);
                }
                break;
            case MonsterState.Action:
                if (_currentTime >= _attackDelay)
                {
                    photonView.RPC("RPC_ExecuteDodgeCheck", RpcTarget.All);

                    ChangeState(MonsterState.Exiting);
                }
                break;
            case MonsterState.Exiting:

                if (_currentTime >= _waitDelay)
                {
                    float exitProgress = (_currentTime - _waitDelay) / _exitDuration;
                    transform.position = Vector3.Lerp(_spawnPos, _endHidingPos, exitProgress);

                    if (_currentTime >= _waitDelay + _exitDuration)
                    {
                        transform.position = _endHidingPos; // 정확한 위치 보정
                        EndTroll();
                    }
                }
                break;
        }
    }

    [PunRPC]
    private void RPC_EnterSound()
    {
        Debug.Log($"<color=cyan>[Kraken]</color> 등장 사운드 재생 요청 받음");

        if (TrollEvents.IsGameplayEventBlocked) return;

        if (_enterSound == null) 
        {
            Debug.LogError("🚨 [Kraken] _enterSound 클립이 비어있습니다! 인스펙터를 확인하세요.");
            return;
        }

        SoundEvents.Play3DSFX?.Invoke(_enterSound, transform.position, 0.45f); // 등장 사운드 재생
    }

    void OnDestroy()
    {
        TrollEvents.OnHideWarningMessage?.Invoke();

        if (PhotonNetwork.IsMasterClient)
            TrollEvents.TriggerTrollFinished();
    }

    [PunRPC]
    private void RPC_ExecuteDodgeCheck()
    {
        EvaluateDodge();
    }

    private void EvaluateDodge()
    {
        // 1. 내 화면 기준 크라켄 위치 판별 (시점 데칼코마니 해결)
        if (TrollEvents.IsGameplayEventBlocked) return;

        bool isKrakenOnMyRight = PhotonNetwork.IsMasterClient ? transform.position.x > 0 : transform.position.x < 0;

        // 2. 🟢 이벤트를 통해 내 캐릭터에게 물리적 회피 성공 여부를 물어봅니다.
        bool isDodgeSuccess = false;
        if (TrollEvents.OnKrakenDodgeCheck != null)
        {
            isDodgeSuccess = TrollEvents.OnKrakenDodgeCheck.Invoke(isKrakenOnMyRight);
        }

        // 결과 처리
        if (isDodgeSuccess)
        {
            Debug.Log("✨ [회피 성공] 크라켄의 공격을 피했습니다!");
        }
        else
        {
            Debug.Log("💥 [회피 실패] 공격에 적중당했습니다! 3초 스턴 및 시야 암전!");
            ApplyEffect();
        }
    }
}
