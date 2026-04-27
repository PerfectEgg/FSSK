using UnityEngine;

public enum AnimalState { Entering, Waiting, Action, Exiting }

// 동물의 경우 (마우스로 치울 수 있음)
public class AnimalTroll : TrollBase, IDraggable 
{
    protected Rigidbody rb;
    protected bool isGrabbed = false;   // 드래그 중인지 여부를 체크하는 변수
    protected bool isOnTable = true;    // 현재 판(책상) 위에 있는지 여부를 체크하는 변수
    protected bool isInteractable = false;  // 드래그 가능 여부

    // 상태에 관한 변수들
    protected AnimalState currentState = AnimalState.Entering;
    protected float currentTime = 0f;   // 총합 행동 시간
    protected float enteringTime = 1f;  // 기본 진입 대기 시간 (상호작용 불가)
    protected float waittingTime = 1f;  // 행동 직전 대기 시간 (상호작용 가능)


    [SerializeField] private float holdDistance = 12.5f;    // 잡고 있을 때의 거리
    private Vector3 originalPosition;   // 움직이는 위치를 저장하고 되돌릴 때 사용할 변수

    private void OnEnable() => GameEvents.OnTrollInteraction += HandleTrollInteraction;
    private void OnDisable() => GameEvents.OnTrollInteraction -= HandleTrollInteraction;

    private void HandleTrollInteraction(bool isGrabbedEvent, GameObject target)
    {
        if (target != gameObject) return;

        if (isGrabbedEvent) OnDragStart();  // 잡힘 이벤트 -> 잡기 로직 실행
        else OnDragEnd();                   // 놓임 이벤트 -> 놓기 로직 실행
    }

    private void Awake() => rb = GetComponent<Rigidbody>();

    private void Update()
    {
        // 잡힌 순간 타이머 작동 X
        if (isGrabbed)
        {
            OnDragging();
            return;
        }

        // 타이머 작동 (잡혀있지 않을 때만 시간이 흐름)
        currentTime += Time.deltaTime;

        originalPosition = transform.position;

        UpdateState();
    }

    protected void ChangeState(AnimalState newState)
    {
        currentState = newState;
        currentTime = 0;        // 타이머 0으로 세팅
        OnStateEnter(newState); // 상태 진입 순간 1회 실행
    }

    // 상태에 막 진입했을 때 할 일 (무적 판정, 애니메이션 재생 등)
    protected virtual void OnStateEnter(AnimalState state)
    {
        if (state == AnimalState.Entering)
            isInteractable = false;
        else
            isInteractable = true;
    }

    protected virtual void UpdateState()
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
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }


    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void ApplyEffect() {  }
    public override void EndTroll() 
    { 
        Destroy(gameObject, 3f); 
    }

    // --- IDraggable(인터페이스)의 메서드 구현 ---
    public void OnDragStart()
    {
        // 상호작용 불가능 상태면 리턴
        if(!isInteractable) return;

        isGrabbed = true;
        rb.isKinematic = true; 

        transform.position += Vector3.up * 1f;
        
    }

    public void OnDragging()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPosition = ray.GetPoint(holdDistance);
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
    }

    public void OnDragEnd()
    {
        isGrabbed = false;
        rb.isKinematic = false;

        CheckDropLocation();
    }

    private void CheckDropLocation()
    {
        // 드롭 시점에 bool 값만 확인하면 끝!
        if (!isOnTable)
        {
            Debug.Log("장외로 치우기 성공!");

            ChangeState(AnimalState.Exiting);
        }
        else
        {
            Debug.Log("아직 책상 위입니다! 방해 계속 진행");
            transform.position = originalPosition;     // 원래 위치로 되돌리기
        }
    }

    // --- Trigger 이벤트로 상태 스위칭 ---
    private void OnTriggerEnter(Collider other)
    {
        // "Table" 태그를 가진 책상 콜라이더 영역에 들어왔을 때
        if (other.CompareTag("Table"))
        {
            isOnTable = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // "Table" 콜라이더 영역 밖으로 완전히 나갔을 때
        if (other.CompareTag("Table"))
        {
            isOnTable = false;
        }
    }
}

