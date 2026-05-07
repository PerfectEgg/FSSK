using UnityEngine;

public class PlayerController : MonoBehaviour
{
   [Header("기울임(Lean) 설정")]
    [SerializeField] private float _leanDistance = 1.5f; // 좌우로 이동할 최대 거리
    [SerializeField] private float _leanAngle = 10f;     // 좌우로 갸우뚱거릴 최대 각도(Z축)
    [SerializeField] private float _leanSpeed = 8f;      // 기울어지는 속도 (부드러움 조절)

    [Header("캐릭터 상체 기울임 설정")]
    private Animator _characterAnimator; // 🟢 카메라가 캐릭터 뼈대를 찾을 수 있게 연결해주세요!
    [SerializeField] private float _bodyLeanAngle = 20f;  // 상체가 기울어질 최대 각도
    [SerializeField] private Vector3 _leanAxis = new Vector3(0, 0, 1); // 뼈대의 회전 축

    private Vector3 _initialLocalPos;
    private Quaternion _initialLocalRot;

    private Transform _spineBone;
    private float _currentBodyLean = 0f; // 현재 상체 기울기 값을 저장

    // 현재 기울임 기능이 활성화되었는지 체크하는 변수
    private bool _canLean = false;

    private float _stunTimer = 0f;      // 자체 기절 타이머

    // 이벤트 구독 및 해제
    private void OnEnable()
    {
        TrollEvents.OnExpansionModeChanged += HandleModeChanged;
        TrollEvents.OnStunEffect += HandleStunEffect;
    }

    private void OnDisable()
    {
        TrollEvents.OnExpansionModeChanged -= HandleModeChanged;
        TrollEvents.OnStunEffect -= HandleStunEffect;
    }

    private void HandleModeChanged(bool isExpansionMode)
    {
        _canLean = isExpansionMode;
    }

    private void HandleStunEffect(float stunDuration)
    {
        // 1. 기절 시간 갱신
        _stunTimer = Mathf.Max(_stunTimer, stunDuration);
    }


    private void Start()
    {
        // 카메라의 초기 위치와 회전값을 '기준점'으로 저장합니다.
        // World 좌표가 아닌 Local 좌표를 사용해야 부모 오브젝트가 회전해도 꼬이지 않습니다.
        _initialLocalPos = transform.localPosition;
        _initialLocalRot = transform.localRotation;

        _characterAnimator = GetComponentInParent<Animator>(); // 부모 오브젝트에서 Animator 컴포넌트를 찾아 연결합니다.

        // 시작할 때 연결된 애니메이터에서 척추 뼈를 찾아옵니다.
        if (_characterAnimator != null)
        {
            _spineBone = _characterAnimator.GetBoneTransform(HumanBodyBones.Spine);
        }
    }

    private void Update()
    {
        // 기절 상태라면 모든 마우스 입력 처리를 무시 (return)
        if (_stunTimer > 0f)
        {
            _stunTimer -= Time.deltaTime;
            
            if (_stunTimer <= 0f)
            {
                Debug.Log("✋ [상호작용] 기절 종료, 조작 가능");
            }
        }

        // 1. 입력 받기 (A키: -1, D키: 1, 안 누르면 0)
        // 조작 불가능 상태일 때는 h를 0으로 만들어 스무스하게 중앙점(0)으로 복귀시킵니다.
        float h = 0f;
        if (_canLean && _stunTimer <= 0f)
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

        // 3. 상체 뼈대 목표 회전값 계산 (적용은 LateUpdate에서)
        float targetBodyLean = h * -_bodyLeanAngle;
        _currentBodyLean = Mathf.Lerp(_currentBodyLean, targetBodyLean, _leanSpeed * Time.deltaTime);
    }

    // 🟢 뼈대 조작은 무조건 애니메이터 작업이 끝난 직후인 LateUpdate에서!
    private void LateUpdate()
    {
        if (_spineBone == null) return;

        // Update에서 계산해둔 부드러운 기울기 값을 뼈대에 추가 회전으로 덮어씌웁니다.
        _spineBone.localRotation *= Quaternion.AngleAxis(_currentBodyLean, _leanAxis);
    }
}