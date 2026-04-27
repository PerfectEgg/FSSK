using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용 세팅")]
    public float reachDistance = 10f; // 동물을 집을 수 있는 최대 사거리
    public LayerMask interactableLayer; // 인스펙터에서 'Interactable' 레이어 체크

    public float throwForce = 500f;   // 던지는 힘

    private GameObject currentGrabbedObject;
    private Transform grabbedTransform;
    private Rigidbody grabbedRigidbody;

    private string grabbedTag;

    private bool canInteract = false;

    private void OnEnable() => GameEvents.OnExpansionModeChanged += HandleCameraModeChanged;
    private void OnDisable() => GameEvents.OnExpansionModeChanged -= HandleCameraModeChanged;

    private void HandleCameraModeChanged(bool isExpansion)
    {
        canInteract = isExpansion;

        // 예외 처리: 확장 모드가 꺼질 때 무언가를 들고 있다면 강제로 놓기
        if (!isExpansion && grabbedTransform != null)
        {
            ReleaseItem();
        }
    }

    void Update()
    {
        // 상호작용 상태 체크
        if (!canInteract) return; 

        // 테스트 용 레이저 쏘기
        DrawDebugRay();

        // 클릭 시도 (잡기)
        if (Input.GetMouseButtonDown(0)&& currentGrabbedObject == null) TryGrab();

        // 마우스 놓기 (드롭)
        if (Input.GetMouseButtonUp(0) && currentGrabbedObject != null) ReleaseItem();
    }

    // 씬 뷰에 레이저를 그려주는 함수
    private void DrawDebugRay()
    {
        // 화면 정중앙에서 발사되는 레이 계산
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Debug.DrawRay(시작점, 방향 * 길이, 색상)
        Debug.DrawRay(ray.origin, ray.direction * reachDistance, Color.red);
    }

    private void TryGrab()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // 🟢 마지막 인자에 interactableLayer를 추가하여 다른 레이어(책상 등)는 무시하게 합니다.
        if (Physics.Raycast(ray, out RaycastHit hit, reachDistance, interactableLayer))
        {
            Debug.Log($"상호작용 성공! 대상: {hit.collider.name}");
            
            currentGrabbedObject = hit.collider.gameObject;
            grabbedTag = hit.collider.tag;
            grabbedTransform = hit.collider.transform;
            GameEvents.TriggerTrollInteraction(true, grabbedTransform.gameObject);
            
            if (grabbedTransform.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        }
        else
        {
            Debug.Log("아무것도 맞지 않았거나, Interactable 레이어가 아닙니다.");
        }
    }

    private void ReleaseItem()
    {
        if (grabbedTransform.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;

        // 던지는 힘 대신, 현재 위치에 정적으로 내려놓는 처리
        // 트롤일 경우 장외 판정 등을 체크하기 위해 이벤트 발송
        if (grabbedTag == "Troll")
        {
            GameEvents.TriggerTrollInteraction(false, grabbedTransform.gameObject);
        }
        else if (grabbedTag == "Item")
        {
            GameEvents.TriggerItemCollected(grabbedTag, grabbedTransform.gameObject);
        }

        currentGrabbedObject = null;
        grabbedTransform = null;
    }
}