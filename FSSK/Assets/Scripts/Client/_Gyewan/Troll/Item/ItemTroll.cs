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
        if (_isGrabbed) OnDragging();
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
        _isGrabbed = true;

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
        _isGrabbed = false;

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

