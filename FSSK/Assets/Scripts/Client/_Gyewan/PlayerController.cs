using UnityEngine;
using Unity.Cinemachine; // 시네머신 네임스페이스
using Photon.Pun;

public class PlayerController : MonoBehaviourPun, IPunObservable
{
    [Header("기울임(Lean) 설정")]
    [SerializeField] private CinemachineCamera _focusCam;           // 착수 카메라
    [SerializeField] private CinemachineCamera _expansionCamera;    // 확장 카메라 (좌우 기울임과 상체 기울임 모두 담당)
    [SerializeField] private float _leanDistance = 1.5f; // 좌우로 이동할 최대 거리
    [SerializeField] private float _leanAngle = 10f;     // 좌우로 갸우뚱거릴 최대 각도(Z축)
    [SerializeField] private float _leanSpeed = 8f;      // 기울어지는 속도 (부드러움 조절)

    [Header("캐릭터 상체 기울임 설정")]
    private Animator _animator; // 🟢 카메라가 캐릭터 뼈대를 찾을 수 있게 연결해주세요!
    [SerializeField] private float _bodyLeanAngle = 20f;  // 상체가 기울어질 최대 각도
    [SerializeField] private Vector3 _leanAxis = new Vector3(0, 1, 0); // 뼈대의 회전 축

    private Vector3 _initialLocalPos;

    private Transform _spineBone;
    
    private float _targetBodyLean = 0f; // 네트워크로 동기화할 목표 상체 기울기 값
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
        if (photonView.IsMine)
        {
            // 1. 기절 시간 갱신
            _stunTimer = Mathf.Max(_stunTimer, stunDuration);
        }
    }


    private void Start()
    {
        // 🟢 만약 인스펙터에서 카메라를 안 넣었다면, 자식 오브젝트 중에서 자동으로 메인 카메라를 찾아옵니다.
        if (!photonView.IsMine)
        {
            if (_focusCam != null) _focusCam.gameObject.SetActive(false);
            if (_expansionCamera != null) _expansionCamera.gameObject.SetActive(false);
        }
        else
        {
            if (_expansionCamera != null)
                _initialLocalPos = _expansionCamera.transform.localPosition;
            else
                Debug.LogError("🚨 내 캐릭터에 카메라가 연결되지 않았습니다!");
        }

        _animator = GetComponent<Animator>(); // 부모 오브젝트에서 Animator 컴포넌트를 찾아 연결합니다.

        // 시작할 때 연결된 애니메이터에서 척추 뼈를 찾아옵니다.
        if (_animator != null)
        {
            _spineBone = _animator.GetBoneTransform(HumanBodyBones.Spine);
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

        if(photonView.IsMine)
        {
            // 1. 입력 받기 (A키: -1, D키: 1, 안 누르면 0)
            // 조작 불가능 상태일 때는 h를 0으로 만들어 스무스하게 중앙점(0)으로 복귀시킵니다.
            float h = 0f;
            if (_canLean && _stunTimer <= 0f)
            {
                h = Input.GetAxis("Horizontal");
            }

            if(_expansionCamera != null)
            {
                // 2. 목표 위치 계산 (기준점 + 좌우 오프셋)
                Vector3 targetPos = _initialLocalPos + new Vector3(h * _leanDistance, 0, 0);
                // 부드러운 위치 이동 (Lerp)
                _expansionCamera.transform.localPosition = Vector3.Lerp(_expansionCamera.transform.localPosition, targetPos, _leanSpeed * Time.deltaTime);

                // 3. 목표 회전 계산 (Z축 회전)
                // 오른쪽(D)을 누르면 h가 양수이므로, Z축을 음수 방향으로 꺾어야 고개가 오른쪽으로 기울어집니다.
                float targetZRotation = -h * _leanAngle;
                // 부드러운 회전 적용 (Slerp)
                _expansionCamera.Lens.Dutch = Mathf.Lerp(_expansionCamera.Lens.Dutch, targetZRotation, _leanSpeed * Time.deltaTime);
            }

            _targetBodyLean = h * -_bodyLeanAngle;
        }

        _currentBodyLean = Mathf.Lerp(_currentBodyLean, _targetBodyLean, _leanSpeed * Time.deltaTime);
    }

    // 뼈대 조작은 무조건 애니메이터 작업이 끝난 직후인 LateUpdate에서!
    private void LateUpdate()
    {
        if (_spineBone == null) return;

        // Update에서 계산해둔 부드러운 기울기 값을 뼈대에 추가 회전으로 덮어씌웁니다.
        _spineBone.localRotation *= Quaternion.AngleAxis(_currentBodyLean, _leanAxis);
    }

    // 인터페이스의 규칙에 따라 반드시 구현해야 하는 함수!
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // [내 캐릭터일 때] 내 목표값을 네트워크로 송출
            stream.SendNext(_targetBodyLean);
        }
        else
        {
            // [남의 캐릭터일 때] 날아온 목표값을 수신
            _targetBodyLean = (float)stream.ReceiveNext();
        }
    }
}