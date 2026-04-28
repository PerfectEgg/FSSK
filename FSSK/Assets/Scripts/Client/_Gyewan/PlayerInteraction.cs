using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용 세팅")]
    [SerializeField] private float _reachDistance = 100f; // 사물을 집을 수 있는 최대 사거리
    [SerializeField] private LayerMask _interactableLayer; // 인스펙터에서 'Interactable' 레이어 체크

    private GameObject _currentGrabbedObject;
    private Transform _grabbedTransform;
    private Rigidbody _grabbedRigidbody;

    private string _grabbedTag;         // 그랩 대상의 태그

    private bool _canInteract = false;  // 상호작용 가능 여부

    private float _stunTimer = 0f;      // 자체 기절 타이머

    private void OnEnable()
    { 
        GameEvents.OnExpansionModeChanged += HandleCameraModeChanged;
        GameEvents.OnStunEffect += HandleStunEffect;
    }

    private void OnDisable()
    {
        GameEvents.OnExpansionModeChanged -= HandleCameraModeChanged;
        GameEvents.OnStunEffect -= HandleStunEffect;
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
            GameEvents.TriggerTrollInteraction(true, _grabbedTransform.gameObject);
            
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
        if (_grabbedTag == "Troll")
        {
            GameEvents.TriggerTrollInteraction(false, _grabbedTransform.gameObject);
        }
        else if (_grabbedTag == "Item")
        {
            GameEvents.TriggerItemCollected(_grabbedTag, _grabbedTransform.gameObject);
        }

        _currentGrabbedObject = null;
        _grabbedTransform = null;
    }
}