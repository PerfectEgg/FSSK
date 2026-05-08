using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가

// 바다 게
public class SeaCrab : AnimalTroll
{
    [SerializeField] private float _moveSpeed = 2.5f;  // 속도 측정
    private Vector3 _targetDirection;
    private Vector3 _targetPosition;

    private bool isArrive = false;  // 도착 여부 체크

    protected override void Start()
    {
        base.Start();

        // 애니메이터 컴포넌트 캐싱
        _animator = GetComponent<Animator>();
    }

    // 목표 지점을 바라보는 함수
    private void LookAtTarget()
    {
        // 생성 시 겹치지 않는 위치를 파악 후 이동
        Vector3 safePos = TrollEvents.GetSafePosition();

        _targetPosition = new Vector3(safePos.x, transform.position.y, safePos.z);

        if (_targetPosition != transform.position)
        {
            transform.LookAt(_targetPosition);
        }
    }

    // 상태에 막 진입했을 때 할 일 (무적 판정, 애니메이션 재생 등)
    protected override void OnStateEnter(AnimalState state)
    {
        base.OnStateEnter(state);

        // 🟢 [멀티플레이 핵심] 계산은 오직 방장(주인)만!
        if (!photonView.IsMine) return;

        if (state == AnimalState.Action)
            LookAtTarget();

        // 2. 🟢 상태에 맞는 애니메이션 트리거 단 한 번 실행
        if (_animator != null)
        {
            // 사용하시는 트리거 변수들을 여기서 모두 Reset 해줍니다.
            SendAnimationReset(_enterTrigger);
            SendAnimationReset(_exitTrigger);

            switch(state)
            {
                case AnimalState.Action:
                    SendAnimationTrigger(_enterTrigger);
                    break;
                case AnimalState.Waiting:
                    SendAnimationTrigger(_exitTrigger);
                    break;
            }
        }
    }

    protected override void OnDestroy()
    {
        // 🟢 [멀티플레이] 트롤 파괴 시 이벤트는 '주인(마지막으로 들고 있던 사람)'만 쏩니다.
        // 안 그러면 모든 유저의 컴퓨터에서 중복으로 이벤트가 발생해 웨이브 타이머가 꼬일 수 있습니다.
        if (photonView.IsMine)
        {
            if(!isArrive)
            {
                // 🟢 파괴하기 전, 마스터에게 완료 보고를 먼저 합니다.
                photonView.RPC("ReportTrollFinishedRPC", RpcTarget.MasterClient);
                StartCoroutine(DelayedNetworkDestroy(3f));
                return;
            }
        }
    }

    protected override void EnterAction()
    {
        // 0.0 ~ 1.0 사이의 진행률 계산
        float progress = _currentTime / _enteringTime;

        // 🟢 위치: 땅속에서 위로 부드럽게 상승
        transform.position = Vector3.Lerp(_hiddenSpawnPos, _finalSpawnPos, progress);
        
        // 🟢 회전: 90도로 숙인 상태에서 0도(원래 각도)로 부드럽게 세워짐
        transform.rotation = Quaternion.Slerp(_hiddenSpawnRot, _finalSpawnRot, progress);

        if (_currentTime >= _enteringTime)
        {
            // 🟢 오차 보정: 정확한 최종 위치/회전으로 딱 맞춰줌
            transform.position = _finalSpawnPos; 
            transform.rotation = _finalSpawnRot;
            
            ChangeState(AnimalState.Action);
        }
    }

    protected override void UpdateState()
    {
        switch(_currentState)
        {
            case AnimalState.Entering:
                EnterAction();
                break;
            case AnimalState.Waiting:
                break;
            case AnimalState.Action:
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _targetPosition) <= 0.05f)
                {
                    ChangeState(AnimalState.Waiting);

                    // 바다 게는 예외로, 도착 완료 시 매니저에게 종료 알림
                    TrollEvents.TriggerTrollFinished();
                    isArrive = true;   // 도착 여부 체크
                }
                break;
            case AnimalState.Exiting:
                // 퇴치될 때 이벤트를 통해 자리를 반납합니다!
                Vector3 releasePos = new Vector3(_targetPosition.x, 0, _targetPosition.z);
                TrollEvents.TriggerPositionReleased(releasePos);

                EndTroll();
                break;
        }
    }
}