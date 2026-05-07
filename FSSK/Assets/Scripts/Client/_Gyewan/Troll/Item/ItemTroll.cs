using UnityEngine;

// 아이템의 경우 (마우스로 던질 수 있음)
public abstract class ItemTroll : TrollBase, IDraggable 
{
    protected Rigidbody rb;
    protected bool _isGrabbed = false;   // 드래그 중인지 여부를 체크하는 변수
    protected bool _isThrown = false;

    [Header("Item Settings")]
    [SerializeField] protected float _throwForce = 100f; // 던지는 힘
    [SerializeField] private float _holdDistance = 10f;    // 잡고 있을 때의 거리

    protected Vector3 _grabbedScale = Vector3.zero; // 🟢 잡았을 때 원래 크기

    private void OnEnable() => TrollEvents.OnTrollInteraction += HandleTrollInteraction;
    private void OnDisable() => TrollEvents.OnTrollInteraction -= HandleTrollInteraction;

    private void HandleTrollInteraction(bool isGrabbedEvent, GameObject target)
    {
        if (target != gameObject) return;

        if (isGrabbedEvent) OnDragStart();  // 잡힘 이벤트 -> 잡기 로직 실행
        else OnDragEnd();                   // 놓임 이벤트 -> 놓기 로직 실행
    }

    private void Awake() => rb = GetComponent<Rigidbody>();

    private void Update()
    {
        if (_isGrabbed) OnDragging();
    }


    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void ApplyEffect() {  }
    public override void EndTroll() 
    { 
        Destroy(gameObject, 3f); 
    }

    // --- IDraggable(인터페이스)의 메서드 구현 ---
    public virtual void OnDragStart()
    {
        // 🟢 [중복 집기 방지] 이미 잡혀있다면 무시
        if (_isGrabbed) return;

        _isGrabbed = true;

        // 🟢 [물리 안정화] 들고 있는 동안 중력이나 다른 물리 충돌에 영향받지 않게 고정
        if (rb != null) rb.isKinematic = true;
        
        // 🟢 [스케일 조절] 손에 쥐는 순간 크기를 정상으로 축소
        transform.localScale = _grabbedScale;

        // 🟢 [레이어 변경] 마우스 레이캐스트에 다시 걸리지 않도록 무시 레이어로 덮어씌움
        gameObject.layer = LayerMask.NameToLayer("Ignore");

        transform.position += Vector3.up * 1f;
        
    }

    public void OnDragging()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPosition = ray.GetPoint(_holdDistance);
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
    }

    public void OnDragEnd()
    {
        // 이미 놨거나 안 잡은 상태면 무시
        if (!_isGrabbed) return;

        _isGrabbed = false;

        // 🟢 물리 연산 복구 (던지기 위해)
        if (rb != null) rb.isKinematic = false;

        Throw(Camera.main.transform.forward);
    }

    private void Throw(Vector3 direction)
    {
        _isThrown = true;
        rb.AddForce(direction * _throwForce, ForceMode.Impulse);

        EndTroll();
    }

    // 🟢 상대방(트리거 콜라이더 등)에게 맞았을 때 실행
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (_isThrown && other.CompareTag("Player"))
        {
            ApplyDebuff(other.gameObject);
            Destroy(gameObject); // 맞히면 즉시 파괴
        }
    }

    public abstract void ApplyDebuff(GameObject target);
}

