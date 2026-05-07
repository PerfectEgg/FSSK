using UnityEngine;
using DG.Tweening;

public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용 세팅")]
    [SerializeField] private float _reachDistance = 100f; // 사물을 집을 수 있는 최대 사거리
    [SerializeField] private LayerMask _interactableLayer; // 인스펙터에서 'Interactable' 레이어 체크

    [Header("IK 세팅 (살짝 쥐기)")]
    [SerializeField] [Range(0f, 1f)] private float _holdWeight = 0.4f; // 드래그 중일 때 팔을 뻗는 정도
    [SerializeField] private float _ikTransitionSpeed = 5f;            // 손을 뻗고 거두는 기본 속도

    [Header("IK 세팅 (던지기 연출)")]
    [SerializeField] private float _throwDuration = 0.15f;  // 던질 때 걸리는 시간
    [SerializeField] private float _holdDuration = 0.05f;   // 다 뻗고 멈춰있는 시간 (임팩트)
    [SerializeField] private float _retractDuration = 0.3f; // 팔을 스무스하게 거두는 시간

    private Animator _animator; // IK 제어를 위한 애니메이터 참조

    // 🟢 오직 오른손 타겟과 가중치만 관리
    private Vector3 _rightHandTarget;
    private float _rightHandWeight = 0f;
    
    private Sequence _throwSequence;
    private bool _isThrowing = false; // 오른손이 던지기 연출 중인지 체크

    private GameObject _currentGrabbedObject;
    private Transform _grabbedTransform;
    private Rigidbody _grabbedRigidbody;

    private string _grabbedTag;         // 그랩 대상의 태그

    private bool _canInteract = false;  // 상호작용 가능 여부

    private float _stunTimer = 0f;      // 자체 기절 타이머

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

        // 예외 처리: 확장 모드가 꺼질 때 무언가를 들고 있다면 강제로 놓기
        if (!isExpansion && _grabbedTransform != null)
        {
            ReleaseItem();
        }
    }

    private void HandleStunEffect(float stunDuration)
    {
        // 1. 기절 시간 갱신
        _stunTimer = Mathf.Max(_stunTimer, stunDuration);

        // 🟢 2. 기절하는 순간, 잡고 있는 물체가 있다면 강제로 놓아버림 (Drop)
        if (_stunTimer > 0f && _currentGrabbedObject != null)
        {
            ReleaseItem();
        }
    }

    void Update()
    {
        UpdateHoldIK();

        // 기절 상태라면 모든 마우스 입력 처리를 무시 (return)
        if (_stunTimer > 0f)
        {
            _stunTimer -= Time.deltaTime;
            
            if (_stunTimer <= 0f)
            {
                Debug.Log("✋ [상호작용] 기절 종료, 조작 가능");
            }
            return; 
        }

        // 상호작용 상태 체크
        if (!_canInteract) return; 

        // 테스트 용 레이저 쏘기
        DrawDebugRay();

        // 클릭 시도 (잡기)
        if (Input.GetMouseButtonDown(0)&& _currentGrabbedObject == null) TryGrab();

        // 마우스 놓기 (드롭)
        if (Input.GetMouseButtonUp(0) && _currentGrabbedObject != null) ReleaseItem();
    }

    // 🟢 드래그 중일 때 "오른손"을 살짝 뻗어서 따라가게 만드는 로직
    private void UpdateHoldIK()
    {
        // 던지기 연출(DOTween)이 진행 중일 때는 이 연산을 멈춤
        if (_isThrowing) return; 

        bool isGrabbing = _currentGrabbedObject != null;
        float targetWeight = isGrabbing ? _holdWeight : 0f;

        // 가중치 스무딩
        _rightHandWeight = Mathf.MoveTowards(_rightHandWeight, targetWeight, Time.deltaTime * _ikTransitionSpeed);
        
        // 대상이 있다면 실시간 위치 추적
        if (isGrabbing) _rightHandTarget = _grabbedTransform.position;
    }

    // 씬 뷰에 레이저를 그려주는 함수
    private void DrawDebugRay()
    {
        // 화면 정중앙에서 발사되는 레이 계산
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Debug.DrawRay(시작점, 방향 * 길이, 색상)
        Debug.DrawRay(ray.origin, ray.direction * _reachDistance, Color.red);
    }

    private void TryGrab()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // 🟢 마지막 인자에 interactableLayer를 추가하여 다른 레이어(책상 등)는 무시하게 합니다.
        if (Physics.Raycast(ray, out RaycastHit hit, _reachDistance, _interactableLayer))
        {
            Debug.Log($"상호작용 성공! 대상: {hit.collider.name}");
            
            _currentGrabbedObject = hit.collider.gameObject;
            _grabbedTag = hit.collider.tag;
            _grabbedTransform = hit.collider.transform;
            TrollEvents.TriggerTrollInteraction(true, _grabbedTransform.gameObject);
            
            if (_grabbedTransform.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        }
        else
        {
            Debug.Log("아무것도 맞지 않았거나, Interactable 레이어가 아닙니다.");
        }
    }

    private void ReleaseItem()
    {
        if (_grabbedTransform.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;

        // 던지는 힘 대신, 현재 위치에 정적으로 내려놓는 처리
        // 트롤일 경우 장외 판정 등을 체크하기 위해 이벤트 발송
        if (_grabbedTag == "Troll" || _grabbedTag == "Item")
        {
            TrollEvents.TriggerTrollInteraction(false, _grabbedTransform.gameObject);
        }

        if (_grabbedTag == "Item")
        {
            TrollEvents.TriggerItemCollected(_grabbedTag, _grabbedTransform.gameObject);
        }

        // 🟢 물건을 놓았으므로 오른손 던지기 연출 발동!
        ExecuteThrowMotion();

        _currentGrabbedObject = null;
        _grabbedTransform = null;
    }

    // 🟢 DOTween을 이용한 오른손 던지기 연출
    private void ExecuteThrowMotion()
    {
        _isThrowing = true; 
        
        // 던질 때 손이 향할 목표 지점 (화면 정중앙 앞쪽 2미터)
        _rightHandTarget = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 2f));

        _throwSequence?.Kill();
        _throwSequence = DOTween.Sequence();

        // 1. 끝까지 확 뻗음 (스냅 타격감)
        _throwSequence.Append(DOTween.To(() => _rightHandWeight, x => _rightHandWeight = x, 1f, _throwDuration).SetEase(Ease.OutBack));
        
        // 2. 찰나의 대기
        _throwSequence.AppendInterval(_holdDuration);
        
        // 3. 부드럽게 거두기 (다 거두면 0f가 됨)
        _throwSequence.Append(DOTween.To(() => _rightHandWeight, x => _rightHandWeight = x, 0f, _retractDuration).SetEase(Ease.InOutQuad));
        
        // 4. 연출 종료 시 다시 일반 시스템으로 복귀
        _throwSequence.OnComplete(() => _isThrowing = false);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;

        // 🟢 오른손 하나만 확실하게 제어
        if (_rightHandWeight > 0f)
        {
            Debug.Log($"IK 작동 중! 가중치: {_rightHandWeight}, 목표: {_rightHandTarget}");
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _rightHandWeight);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHandTarget);
        }
    }
}