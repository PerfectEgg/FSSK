using UnityEngine;
using DG.Tweening;
using Photon.Pun;
using IEnumerator = System.Collections.IEnumerator;

public class PlayerInteraction : MonoBehaviourPun, IPunObservable
{
    [Header("상호작용 세팅")]
    [SerializeField] private float _reachDistance = 100f; 
    [SerializeField] private LayerMask _interactableLayer; 

    [Header("그랩 세팅")]
    [SerializeField] private float _holdDistance = 7.5f;
    [SerializeField] private float _throwAimAssistRadius = 3f;
    [SerializeField] private float _minThrowDirectionY = -0.05f;

    [Header("IK 세팅 (살짝 쥐기)")]
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
    private bool _isGameOver = false;       // 게임 오버 상태 추적 변수
    private bool _isProcessing = false;     // 현재 처리 중인 상태 추적 변수
    private Coroutine _pendingGrabCoroutine;

    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    { 
        TrollEvents.OnExpansionModeChanged += HandleCameraModeChanged;
        TrollEvents.OnStunEffect += HandleStunEffect;
        GameEvents.OnGameOverTriggered += HandleGameOver;
    }

    private void OnDisable()
    {
        TrollEvents.OnExpansionModeChanged -= HandleCameraModeChanged;
        TrollEvents.OnStunEffect -= HandleStunEffect;
        GameEvents.OnGameOverTriggered -= HandleGameOver;

        CancelPendingGrab();
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

    private void HandleGameOver()
    {
        if (!photonView.IsMine) return;

        // 🟢 게임 오버 시, 모든 카메라 모드를 착수 모드로 고정하고, 커서도 해제합니다.
        _isGameOver = true;
        CancelPendingGrab();

        // 🟢 만약 게임 종료 순간에 손에 무언가를 들고 있다면 강제로 놓게 만듭니다.
        if (_currentGrabbedObject != null)
        {
            ReleaseItem();
        }
        
        // IK 가중치도 0으로 만들어 팔을 내리게 합니다.
        _targetWeight = 0f;
    }

    private void CancelPendingGrab()
    {
        if (_pendingGrabCoroutine == null) return;

        StopCoroutine(_pendingGrabCoroutine);
        _pendingGrabCoroutine = null;
        _isProcessing = false;
    }

    void Update()
    {
        if (_stunTimer > 0f) _stunTimer -= Time.deltaTime;

        // 1. [내 캐릭터 전용] 입력 처리 및 조준점 계산
        if (photonView.IsMine)
        {
            if (!_canInteract || _stunTimer > 0f || _isGameOver || _isProcessing) 
            {
                _targetWeight = 0f; // 조작 불가능 시 손 내리기
            }
            else
            {
                DrawDebugRay();
                if (Input.GetMouseButtonDown(0) && _currentGrabbedObject == null) TryGrab();
                if (Input.GetMouseButtonUp(0) && _currentGrabbedObject != null) ReleaseItem();

                if (!_isThrowing && _currentGrabbedObject != null)
                {
                    Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                    _rightHandTarget = ray.GetPoint(_holdDistance); 
                    
                    // 🟢 [핵심 수정] 위치 업데이트는 오직 내 캐릭터일 때만 수행!
                    // 상대방 화면에서는 이 캐릭터의 _rightHandTarget이 OnPhotonSerializeView를 통해 
                    // 동기화되고 있으므로, 아이템의 PhotonTransformView가 자연스럽게 따라가게 됩니다.
                    if (_grabbedTransform != null)
                    {
                        _grabbedTransform.position = _rightHandTarget;
                    }
                }
            }
        }

        // 2. [모든 클라이언트 공통] IK 가중치 스무딩
        if (!_isThrowing)
        {
            _rightHandWeight = Mathf.MoveTowards(_rightHandWeight, _targetWeight, Time.deltaTime * _ikTransitionSpeed);
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
            PhotonView itemPV = hit.collider.GetComponentInParent<PhotonView>();
            if (itemPV != null)
            {
                // 동물 트롤일 경우, 현재 상태가 상호작용 불가능한 상태(예: 쥐가 달리는 중)라면 잡기를 취소합니다.
                AnimalTroll animalTroll = itemPV.GetComponent<AnimalTroll>();
                // _isInteractable이 false라면(예: 쥐가 달리는 중) 여기서 잡기를 취소합니다.
                if (animalTroll != null && !animalTroll.IsInteractable) return;

                CancelPendingGrab();
                _pendingGrabCoroutine = StartCoroutine(GrabItemWhenOwned(itemPV, hit.collider.tag));
            }
        }
    }

    private IEnumerator GrabItemWhenOwned(PhotonView itemPV, string grabbedTag)
    {
        _isProcessing = true;

        int actorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        if (itemPV.OwnershipTransfer == OwnershipOption.Takeover && actorNumber > 0)
        {
            itemPV.TransferOwnership(actorNumber);
        }
        else
        {
            itemPV.RequestOwnership();
        }

        const float ownershipWaitSeconds = 0.35f;
        float elapsed = 0f;
        while (itemPV != null && !itemPV.IsMine && elapsed < ownershipWaitSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _isProcessing = false;
        _pendingGrabCoroutine = null;

        if (itemPV == null || !itemPV.IsMine || !_canInteract || _stunTimer > 0f || _isGameOver)
        {
            yield break;
        }

        _grabbedItemViewId = itemPV.ViewID;
        _grabbedTag = grabbedTag;

        photonView.RPC(
            nameof(SyncGrabItemRPC),
            RpcTarget.All,
            _grabbedItemViewId,
            true,
            Vector3.zero,
            itemPV.transform.position,
            actorNumber);
    }

    private void ReleaseItem()
    {
        if (_grabbedItemViewId != -1)
        {
            // 🟢 로컬 이벤트 발송 삭제! (아래 RPC 함수에서 처리합니다)
            Vector3 releaseDirection = GetReleaseDirection();
            Vector3 releasePosition = _grabbedTransform != null ? _grabbedTransform.position : transform.position;
            int actorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
            photonView.RPC(
                nameof(SyncGrabItemRPC),
                RpcTarget.All,
                _grabbedItemViewId,
                false,
                releaseDirection,
                releasePosition,
                actorNumber);
            _grabbedItemViewId = -1;
        }

        // 던지기 연출 발동
        Vector3 throwTarget = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 2f));
        photonView.RPC("PlayThrowMotionRPC", RpcTarget.All, throwTarget);

        _currentGrabbedObject = null;
        _grabbedTransform = null;
    }

    private Vector3 GetReleaseDirection()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return transform.forward;
        }

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 origin = _grabbedTransform != null ? _grabbedTransform.position : ray.origin;

        if (TryGetPlayerAimPoint(ray, out Vector3 aimPoint))
        {
            Vector3 targetDirection = aimPoint - origin;
            if (targetDirection.sqrMagnitude > 0.0001f)
            {
                return targetDirection.normalized;
            }
        }

        Vector3 direction = ray.direction;
        if (direction.y < _minThrowDirectionY)
        {
            direction.y = _minThrowDirectionY;
        }

        return direction.normalized;
    }

    private bool TryGetPlayerAimPoint(Ray ray, out Vector3 aimPoint)
    {
        aimPoint = default;

        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            Mathf.Max(0.01f, _throwAimAssistRadius),
            _reachDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        foreach (RaycastHit hit in hits)
        {
            PhotonView targetPV = hit.collider.GetComponentInParent<PhotonView>();
            if (targetPV == null || targetPV.OwnerActorNr == localActorNumber)
            {
                continue;
            }

            if (!hit.collider.CompareTag("Player") && targetPV.GetComponentInChildren<PlayerController>() == null)
            {
                continue;
            }

            aimPoint = hit.collider.bounds.center;
            return true;
        }

        return false;
    }

    // 🟢 [추가됨] 아이템 잡기/놓기 상태를 모든 유저의 화면에서 동기화하는 함수
    [PunRPC]
    private void SyncGrabItemRPC(
        int itemViewId,
        bool isGrabbed,
        Vector3 releaseDirection,
        Vector3 itemPosition,
        int actorNumber)
    {
        PhotonView itemPV = PhotonView.Find(itemViewId);
        if (itemPV == null) return;

        AnimalTroll animalTroll = itemPV.GetComponent<AnimalTroll>();
        if (animalTroll != null) animalTroll.SetGrabbedState(isGrabbed);

        ItemTroll itemTroll = itemPV.GetComponent<ItemTroll>();
        if (itemTroll != null) itemTroll.SetGrabbedState(isGrabbed, releaseDirection, itemPosition, actorNumber);

        if (isGrabbed)
        {
            if (itemPV.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;

            // 🟢 [핵심 수정] 내 캐릭터일 때만 손에 쥐도록 변경! (줄다리기 방지)
            if (photonView.IsMine) 
            {
                _currentGrabbedObject = itemPV.gameObject;
                _grabbedTransform = itemPV.transform;
            }
        }
        else
        {
            if (itemPV.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;

            // 🟢 [핵심 수정] 놓을 때도 내 캐릭터만 비워줍니다.
            if (photonView.IsMine) 
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
