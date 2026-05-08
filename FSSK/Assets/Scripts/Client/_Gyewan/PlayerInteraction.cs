using UnityEngine;
using DG.Tweening;
using Photon.Pun;

public class PlayerInteraction : MonoBehaviourPun, IPunObservable
{
    [Header("상호작용 세팅")]
    [SerializeField] private float _reachDistance = 100f; 
    [SerializeField] private LayerMask _interactableLayer; 

    [Header("그랩 세팅")]
    [SerializeField] private float _holdDistance = 7.5f;

    [Header("IK 세팅 (살짝 쥐기)")]
    [SerializeField] [Range(0f, 1f)] private float _holdWeight = 0.4f; 
    [SerializeField] private float _ikTransitionSpeed = 5f;            

    [Header("IK 세팅 (던지기 연출)")]
    [SerializeField] private float _throwDuration = 0.15f;  
    [SerializeField] private float _holdDuration = 0.05f;   
    [SerializeField] private float _retractDuration = 0.3f; 

    private Animator _animator; 

    private Vector3 _rightHandTarget;
    private float _rightHandWeight = 0f;
    private float _targetWeight = 0f;
    
    private Sequence _throwSequence;
    private bool _isThrowing = false; 

    private GameObject _currentGrabbedObject;
    private Transform _grabbedTransform;
    
    // 🟢 누락되었던 변수 선언 추가
    private int _grabbedItemViewId = -1; 
    private string _grabbedTag;         
    private bool _canInteract = false;  
    private float _stunTimer = 0f;      

    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    { 
        TrollEvents.OnExpansionModeChanged += HandleCameraModeChanged;
        TrollEvents.OnStunEffect += HandleStunEffect;
    }

    private void OnDisable()
    {
        TrollEvents.OnExpansionModeChanged -= HandleCameraModeChanged;
        TrollEvents.OnStunEffect -= HandleStunEffect;
    }

    private void HandleCameraModeChanged(bool isExpansion)
    {
        _canInteract = isExpansion;
        if (photonView.IsMine && !isExpansion && _grabbedTransform != null) ReleaseItem();
    }

    private void HandleStunEffect(float stunDuration)
    {
        _stunTimer = Mathf.Max(_stunTimer, stunDuration);
        if (photonView.IsMine && _stunTimer > 0f && _currentGrabbedObject != null) ReleaseItem();
    }

    void Update()
    {
        if (_stunTimer > 0f) _stunTimer -= Time.deltaTime;

        // 1. [내 캐릭터 전용] 입력 처리 및 조준점 계산
        if (photonView.IsMine)
        {
            if (!_canInteract || _stunTimer > 0f) 
            {
                _targetWeight = 0f; // 조작 불가능 시 손 내리기
            }
            else
            {
                DrawDebugRay();
                if (Input.GetMouseButtonDown(0) && _currentGrabbedObject == null) TryGrab();
                if (Input.GetMouseButtonUp(0) && _currentGrabbedObject != null) ReleaseItem();

                if (!_isThrowing)
                {
                    bool isGrabbing = _currentGrabbedObject != null;
                    _targetWeight = isGrabbing ? _holdWeight : 0f;

                    if (isGrabbing) 
                    {
                        // 🟢 [수정 2] 아이템의 현재 위치가 아니라, 화면 중앙(마우스) 좌표를 목표로 삼습니다!
                        // 이렇게 해야 아이템이 화면을 돌리는 대로 예쁘게 따라옵니다.
                        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                        _rightHandTarget = ray.GetPoint(_holdDistance); 
                    }
                }
            }
        }

        // 2. [모든 클라이언트 공통] IK 가중치 스무딩
        if (!_isThrowing)
        {
            _rightHandWeight = Mathf.MoveTowards(_rightHandWeight, _targetWeight, Time.deltaTime * _ikTransitionSpeed);
        }

        // 3. 🟢 [핵심] 누군가 물건을 쥐고 있다면, 아이템 위치를 손 위치로 강제 고정 (모든 화면 동기화)
        if (_grabbedTransform != null)
        {
            _grabbedTransform.position = _rightHandTarget;
        }
    }

    private void DrawDebugRay()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Debug.DrawRay(ray.origin, ray.direction * _reachDistance, Color.red);
    }

    private void TryGrab()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        if (Physics.Raycast(ray, out RaycastHit hit, _reachDistance, _interactableLayer))
        {
            PhotonView itemPV = hit.collider.GetComponent<PhotonView>();
            if (itemPV != null)
            {
                // 소유권 요청 (이제 이 아이템은 내가 통제한다)
                itemPV.RequestOwnership(); 

                _grabbedItemViewId = itemPV.ViewID; 
                _grabbedTag = hit.collider.tag;

                // 🟢 [수정 3] 로컬 이벤트 발송 삭제! (모든 클라이언트가 알 수 있도록 RPC로 옮겼습니다)
                photonView.RPC("SyncGrabItemRPC", RpcTarget.All, _grabbedItemViewId, true);
            }
        }
    }

    private void ReleaseItem()
    {
        if (_grabbedItemViewId != -1)
        {
            // 🟢 로컬 이벤트 발송 삭제! (아래 RPC 함수에서 처리합니다)
            photonView.RPC("SyncGrabItemRPC", RpcTarget.All, _grabbedItemViewId, false);
            _grabbedItemViewId = -1;
        }

        // 던지기 연출 발동
        Vector3 throwTarget = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 2f));
        photonView.RPC("PlayThrowMotionRPC", RpcTarget.All, throwTarget);

        _currentGrabbedObject = null;
        _grabbedTransform = null;
    }

    // 🟢 [추가됨] 아이템 잡기/놓기 상태를 모든 유저의 화면에서 동기화하는 함수
    [PunRPC]
    private void SyncGrabItemRPC(int itemViewId, bool isGrabbed)
    {
        PhotonView itemPV = PhotonView.Find(itemViewId);
        if (itemPV == null) return;

         TrollEvents.TriggerTrollInteraction(isGrabbed, itemPV.gameObject);

        if (!isGrabbed && itemPV.CompareTag("Item"))
        {
            TrollEvents.TriggerItemCollected(itemPV.tag, itemPV.gameObject);
        }

        if (isGrabbed)
        {
            _currentGrabbedObject = itemPV.gameObject;
            _grabbedTransform = itemPV.transform;
            if (itemPV.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        }
        else
        {
            if (itemPV.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;

            if (!photonView.IsMine)
            {
                _currentGrabbedObject = null;
                _grabbedTransform = null;
            }
        }
    }

    [PunRPC]
    private void PlayThrowMotionRPC(Vector3 targetPos)
    {
        _isThrowing = true; 
        _rightHandTarget = targetPos;

        _throwSequence?.Kill();
        _throwSequence = DOTween.Sequence();

        _throwSequence.Append(DOTween.To(() => _rightHandWeight, x => _rightHandWeight = x, 1f, _throwDuration).SetEase(Ease.OutBack));
        _throwSequence.AppendInterval(_holdDuration);
        _throwSequence.Append(DOTween.To(() => _rightHandWeight, x => _rightHandWeight = x, 0f, _retractDuration).SetEase(Ease.InOutQuad));
        
        _throwSequence.OnComplete(() => 
        {
            _isThrowing = false;
            _targetWeight = 0f; 
        });
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;
        if (_rightHandWeight > 0f)
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _rightHandWeight);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHandTarget);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_targetWeight);
            stream.SendNext(_rightHandTarget);
        }
        else
        {
            _targetWeight = (float)stream.ReceiveNext();
            _rightHandTarget = (Vector3)stream.ReceiveNext();
        }
    }
}