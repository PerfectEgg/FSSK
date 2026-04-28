using UnityEngine;

public class PlayerController : MonoBehaviour
{
   [Header("기울임(Lean) 설정")]
    [SerializeField] private float _leanDistance = 1.5f; // 좌우로 이동할 최대 거리
    [SerializeField] private float _leanAngle = 10f;     // 좌우로 갸우뚱거릴 최대 각도(Z축)
    [SerializeField] private float _leanSpeed = 8f;      // 기울어지는 속도 (부드러움 조절)

    private Vector3 _initialLocalPos;
    private Quaternion _initialLocalRot;

    // 현재 기울임 기능이 활성화되었는지 체크하는 변수
    private bool _canLean = false;

    // 이벤트 구독 및 해제
    private void OnEnable() => GameEvents.OnExpansionModeChanged += HandleModeChanged;
    private void OnDisable() => GameEvents.OnExpansionModeChanged -= HandleModeChanged;

    private void HandleModeChanged(bool isExpansionMode)
    {
        _canLean = isExpansionMode;
    }

    private void Start()
    {
        // 카메라의 초기 위치와 회전값을 '기준점'으로 저장합니다.
        // World 좌표가 아닌 Local 좌표를 사용해야 부모 오브젝트가 회전해도 꼬이지 않습니다.
        _initialLocalPos = transform.localPosition;
        _initialLocalRot = transform.localRotation;
    }

    private void Update()
    {
        // 1. 입력 받기 (A키: -1, D키: 1, 안 누르면 0)
        float h = 0f;

        if (_canLean)
        {
            h = Input.GetAxis("Horizontal");
        }

        // 2. 목표 위치 계산 (기준점 + 좌우 오프셋)
        Vector3 targetPos = _initialLocalPos + new Vector3(h * _leanDistance, 0, 0);
        
        // 부드러운 위치 이동 (Lerp)
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, _leanSpeed * Time.deltaTime);

        // 3. 목표 회전 계산 (Z축 회전)
        // 오른쪽(D)을 누르면 h가 양수이므로, Z축을 음수 방향으로 꺾어야 고개가 오른쪽으로 기울어집니다.
        float targetZRotation = -h * _leanAngle;
        Quaternion targetRot = _initialLocalRot * Quaternion.Euler(0, 0, targetZRotation);

        // 부드러운 회전 적용 (Slerp)
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, _leanSpeed * Time.deltaTime);
    }
}