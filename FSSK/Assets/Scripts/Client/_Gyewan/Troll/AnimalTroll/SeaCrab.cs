using UnityEngine;

// 바다 게
public class SeaCrab : AnimalTroll
{
    [SerializeField] private float moveSpeed = 2.5f;  // 속도 측정
    private Vector3 targetDirection;
    private Vector3 targetPosition;

    void Start()
    {
        // 생성 시 겹치지 않는 위치를 파악 후 이동
        Vector3 safePos = GameEvents.GetSafePosition();

        targetPosition = new Vector3(safePos.x, transform.position.y, safePos.z);

        targetDirection = targetPosition.normalized;

        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    protected override void UpdateState()
    {
        switch(currentState)
        {
            case AnimalState.Entering:
                if (currentTime >= enteringTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Waiting:
                break;
            case AnimalState.Action:
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
                {
                    ChangeState(AnimalState.Waiting);
                }
                break;
            case AnimalState.Exiting:
                // 퇴치될 때 이벤트를 통해 자리를 반납합니다!
                Vector3 releasePos = new Vector3(targetPosition.x, 0, targetPosition.z);
                GameEvents.TriggerPositionReleased(releasePos);

                EndTroll();
                break;
        }
    }
}