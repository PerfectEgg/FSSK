using UnityEngine;

// 바다 게
public class SeaCrab : AnimalTroll
{
    [SerializeField] private float _moveSpeed = 2.5f;  // 속도 측정
    private Vector3 _targetDirection;
    private Vector3 _targetPosition;

    void Start()
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

    protected override void UpdateState()
    {
        switch(_currentState)
        {
            case AnimalState.Entering:
                if (_currentTime >= _enteringTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Waiting:
                break;
            case AnimalState.Action:
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _targetPosition) <= 0.05f)
                {
                    ChangeState(AnimalState.Waiting);
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