using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가
using IEnumerator = System.Collections.IEnumerator;

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

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void ApplyEffect() {  }
    public override void EndTroll() 
    { 
        // 🟢 [멀티플레이 핵심] 주인(방금 던진 사람)의 컴퓨터에서만 네트워크 파괴를 실행합니다.
        if (photonView.IsMine)
        {
            StartCoroutine(DelayedNetworkDestroy(3f));
        }
    }

    // 🟢 [멀티플레이 추가] 지연된 네트워크 파괴를 위한 코루틴
    private IEnumerator DelayedNetworkDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        PhotonNetwork.Destroy(gameObject);
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
        // 🟢 [멀티플레이 핵심] "내가 던진 아이템"이 "플레이어"에게 맞았을 때만 판정합니다.
        // 이렇게 해야 중복 데미지나 중복 파괴 에러가 발생하지 않습니다.
        if (!photonView.IsMine) return;

        if (_isThrown && other.CompareTag("Player"))
        {
            ApplyDebuff(other.gameObject);

            // 🟢 즉시 파괴 시에도 네트워크 파괴 사용
            PhotonNetwork.Destroy(gameObject);
        }
    }

    public abstract void ApplyDebuff(GameObject target);
}

