using UnityEngine;
using UnityEngine.UIElements;

// 거북이
public class Turtle : AnimalTroll
{
    [SerializeField] private float _moveSpeed = 1.25f;  // 속도 측정
    private Vector3 _targetDirection;
    private Vector3 _targetPosition;

    // 목표 지점을 바라보는 함수
    private void LookAtTarget()
    {
        _targetPosition = new Vector3(-transform.position.x, transform.position.y, -transform.position.z);

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

        if (state == AnimalState.Waiting)
            LookAtTarget();
    }

    void OnDestroy()
    {
        // 트롤이 제거될 때 매니저에게 종료 알림
        TrollEvents.TriggerTrollFinished();
    }

    protected override void UpdateState()
    {
        switch(_currentState)
        {
            case AnimalState.Entering:
                EnterAction();
                break;
            case AnimalState.Waiting:
                if (_currentTime >= _waittingTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Action:
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _targetPosition) <= 0.05f)
                {
                    ChangeState(AnimalState.Exiting);
                }
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }
}