using UnityEngine;

// 바다 게
public class SeaCrab : AnimalTroll
{
    [SerializeField] private float _moveSpeed = 2.5f;  // 속도 측정
    private Vector3 _targetDirection;
    private Vector3 _targetPosition;

    private bool isArrive = false;  // 도착 여부 체크

    // 목표 지점을 바라보는 함수
    private void LookAtTarget()
    {
        // 생성 시 겹치지 않는 위치를 파악 후 이동
        Vector3 safePos = TrollEvents.GetSafePosition();

        _targetPosition = new Vector3(safePos.x, transform.position.y, safePos.z);

        _targetDirection = _targetPosition.normalized;

        if (_targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(_targetDirection);
        }
    }

    // 상태에 막 진입했을 때 할 일 (무적 판정, 애니메이션 재생 등)
    protected override void OnStateEnter(AnimalState state)
    {
        base.OnStateEnter(state);

        if (state == AnimalState.Action)
            LookAtTarget();
    }

    void OnDestroy()
    {
        if(!isArrive)
        {
            // 바다 게는 도착 시점에 매니저에게 종료 알림을 보내므로, 도착 이후 파괴될 때는 추가로 알림을 보내지 않습니다.
            TrollEvents.TriggerTrollFinished();
            return;
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