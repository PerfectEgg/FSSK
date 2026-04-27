using UnityEngine;
using UnityEngine.UIElements;

// 거북이
public class Turtle : AnimalTroll
{
    [SerializeField] private float moveSpeed = 1.25f;  // 속도 측정
    private Vector3 targetDirection;
    private Vector3 targetPosition;

    void Start()
    {
        targetPosition = new Vector3(-transform.position.x, transform.position.y, -transform.position.z);

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
                    ChangeState(AnimalState.Waiting);
                break;
            case AnimalState.Waiting:
                if (currentTime >= waittingTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Action:
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
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