using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가
using IEnumerator = System.Collections.IEnumerator;

public enum ItemType { Rum, Octopus }

// 아이템의 경우 (마우스로 던질 수 있음)
public abstract class ItemTroll : TrollBase, IDraggable, IPunObservable
{
    protected Rigidbody rb;
    protected bool _isGrabbed = false;   // 드래그 중인지 여부를 체크하는 변수
    protected bool _isThrown = false;

    [Header("Item Settings")]
    [SerializeField] protected float _throwForce = 100f;    // 던지는 힘
    [SerializeField] protected ItemType _itemType;

    [SerializeField] protected AudioClip _hitSound; // 🟢 적중 사운드
    protected Vector3 _grabbedScale = Vector3.one; // 🟢 잡았을 때 원래 크기

    // ✅ SyncGrabItemRPC에서 직접 호출 (로컬 이벤트 의존 제거)
    public void SetGrabbedState(bool isGrabbed)
    {
        if (isGrabbed) OnDragStart();
        else OnDragEnd();
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

        if (rb != null)
        {
            rb.isKinematic = false;
        }

        if (photonView.IsMine)
        {
            Throw(Camera.main.transform.forward);
        }
    }

    private void Throw(Vector3 direction)
    {
        _isThrown = true;
        rb.AddForce(direction * _throwForce, ForceMode.Impulse);

        EndTroll();
    }

    // 🟢 상대방(트리거 콜라이더 등)에게 맞았을 때 실행
    private void OnTriggerEnter(Collider other)
    {
        // 🟢 [멀티플레이 핵심] "내가 던진 아이템"이 "플레이어"에게 맞았을 때만 판정합니다.
        // 이렇게 해야 중복 데미지나 중복 파괴 에러가 발생하지 않습니다.
        if (!photonView.IsMine) return;
        // 아직 던져지지 않았으면 무시 (잡은 상태에서 충돌해도 효과 안 나도록)
        if (_isGrabbed) return;

        if (_isThrown && other.CompareTag("Player"))
        {
            PhotonView targetPV = other.GetComponentInParent<PhotonView>();

            // 🟢 내가 나를 맞춘 게 아니라면? (상대방 명중!)
            if (targetPV != null && !targetPV.IsMine)
            {
                Debug.Log($"🎯 [명중] 상대방에게 {_itemType}을 맞췄습니다!");

                TrollEvents.TriggerItemCollected(gameObject.tag, gameObject);

                // 🟢 enum을 (int)로 변환해서 안전하게 RPC 송출
                targetPV.RPC("RPC_ApplyItemEffect", targetPV.Owner, (int)_itemType);

                photonView.RPC("RPC_HitItemSound", RpcTarget.All); // 적중 사운드 재생

                StartCoroutine(DelayedNetworkDestroy(0.1f));
            }
        }
    }

    [PunRPC]
    private void RPC_HitItemSound()
    {
        if (_hitSound != null)
        {
            Debug.Log($"🎯 [아이템 명중] {_itemType}가 플레이어에게 명중했습니다! 적중 사운드를 재생합니다.");
            SoundEvents.Play3DSFX?.Invoke(_hitSound, transform.position, 0.45f);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_isGrabbed);
            stream.SendNext(_isThrown);
        }
        else
        {
            _isGrabbed = (bool)stream.ReceiveNext();
            _isThrown = (bool)stream.ReceiveNext();
        }
    }
}

