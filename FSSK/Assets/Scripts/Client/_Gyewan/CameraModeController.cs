using UnityEngine;
using Unity.Cinemachine;
using Photon.Pun; // 🟢 포톤 네임스페이스 추가!
using System; // 시네머신 3 네임스페이스

public class CameraModeController : MonoBehaviourPun
{
    [Header("카메라 세팅")]
    [SerializeField] private CinemachineCamera _focusCam;       // 70도 고정 착수 카메라
    [SerializeField] private CinemachineCamera _expansionCam;   // Pan Tilt 회전 제한 확장 카메라

    [Header("세이렌 이펙트 세팅")]
    [SerializeField] private float _sirenPullSpeed = 1.5f;        // 시선이 세이렌 쪽으로 끌려가는 속도

    // 내부 캐싱 및 상태 변수
    private CinemachinePanTilt _panTilt;
    private bool _isExpansionMode = false;

    // 세이렌 관련 변수
    private bool _isSirenSinging = false;
    private Transform _sirenTarget;

    // 기절 상태 추적 변수
    private float _stunTimer = 0f;  // 기절 시간
    private float _lockedPan = 0f;  // 기절 시 고정될 좌우 각도
    private float _lockedTilt = 0f; // 기절 시 고정될 상하 각도

    private void OnEnable()
    {
        TrollEvents.OnSirenEffect += HandleSirenEffect;  // 세이렌 이벤트 구독
        TrollEvents.OnStunEffect += HandleStunEffect;   // 기절 이벤트 구독
    }

    private void OnDisable()
    {
        TrollEvents.OnSirenEffect -= HandleSirenEffect;
        TrollEvents.OnStunEffect -= HandleStunEffect;
    }

    private void HandleSirenEffect(bool isSinging, Transform target)
    {
        // 🟢 남의 캐릭터는 이 이벤트에 반응해서 내 화면 카메라를 돌리면 안 됩니다!
        if (!photonView.IsMine) return;

        _isSirenSinging = isSinging;
        _sirenTarget = target;

        // 세이렌이 노래를 시작하면, 이전 상태를 잊고 무조건 '확장 모드'로 강제 덮어쓰기!
        if (isSinging)
        {
            _isExpansionMode = true;
        }

        SetCameraMode(_isExpansionMode);
    }

    private void HandleStunEffect(float stunDuration)
    {
        // 🟢 남의 캐릭터는 이 이벤트에 반응해서 내 화면 카메라를 돌리면 안 됩니다!
        if (!photonView.IsMine) return;

        // 🟢 방금 막 기절에 걸린 순간이라면, 현재 시네머신의 각도를 자물쇠 변수에 박제합니다!
        if (_stunTimer <= 0f && _panTilt != null)
        {
            _lockedPan = _panTilt.PanAxis.Value;
            _lockedTilt = _panTilt.TiltAxis.Value;
        }

        // 기절 중 기절이 더 걸릴 경우, 더 긴 기절 시간을 지정
        _stunTimer = Mathf.Max(_stunTimer, stunDuration);
    }

    void Awake()
    {
        if (_expansionCam != null)
        {
            _panTilt = _expansionCam.GetComponent<CinemachinePanTilt>();
        }
    }

    void Start()
    {
        // 🟢 내 캐릭터일 때만 초기 착수 모드 세팅을 진행합니다.
        if (photonView.IsMine)
        {
            SetCameraMode(false);
        }
    }

    void Update()
    {
        // 🟢 가장 중요한 핵심! 내 캐릭터가 아니면 키보드 입력도 받지 말고, 각도 계산도 하지 마!
        if (!photonView.IsMine) return;

        if(_stunTimer > 0)
        {
            _stunTimer -= Time.deltaTime;

            // 🟢 [핵심] 시네머신이 마우스 입력을 받아 움직이려고 해도, 우리가 박제한 각도로 매 프레임 강제로 끌어내립니다. (완벽한 화면 동결)
            if (_panTilt != null)
            {
                _panTilt.PanAxis.Value = _lockedPan;
                _panTilt.TiltAxis.Value = _lockedTilt;
            }
            
            // 기절이 방금 딱 끝난 순간!
            if (_stunTimer <= 0f)
            {
                Debug.Log("🎥 [카메라] 기절 종료! 조작 권한 복구");
                // 밀려있던 카메라 모드(확장/착수)를 현재 상황에 맞게 갱신해 줍니다.
                SetCameraMode(_isExpansionMode || _isSirenSinging);
            }

            return;
        }

        // 세이렌에게 홀린 상태: 시선 강제 이동 (조작 불가)
        if (_isSirenSinging && _sirenTarget != null && _panTilt != null)
        {
            ForceLookAtSiren();
        }
        // 정상 상태: Space 키로 자유롭게 모드 전환
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            _isExpansionMode = !_isExpansionMode;

            if (_isExpansionMode && _panTilt != null)
            {
                _panTilt.PanAxis.Value = 0f;
                _panTilt.TiltAxis.Value = 20f;
            }

            SetCameraMode(_isExpansionMode);
        }
    }

    private void SetCameraMode(bool expansion)
    {
        if (expansion)
        {
            _focusCam.Priority = 10;
            _expansionCam.Priority = 20;
            Cursor.lockState = CursorLockMode.Locked; 
        }
        else
        {
            // 착수 모드: 고정 카메라 활성화, 커서 해제
            _focusCam.Priority = 20;
            _expansionCam.Priority = 10;
            Cursor.lockState = CursorLockMode.None;
        }

        // UI나 다른 스크립트에도 현재 상태 방송
        TrollEvents.TriggerExpansionMode(expansion);
    }

    // 세이렌을 강제로 바라보게 Pan/Tilt 값을 조작하는 함수
    private void ForceLookAtSiren()
    {
        Vector3 direction = _sirenTarget.position - _expansionCam.transform.position;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            
            // Quaternion에서 좌우 회전값(Y축 회전 = Pan)만 추출합니다.
            float targetPan = targetRot.eulerAngles.y;

            // Mathf.LerpAngle을 사용하되, _sirenPullSpeed를 낮추어 '늪에 빠지듯' 천천히 돌아가게 만듭니다.
            _panTilt.PanAxis.Value = Mathf.LerpAngle(_panTilt.PanAxis.Value, targetPan, Time.deltaTime * _sirenPullSpeed);
            _panTilt.TiltAxis.Value = Mathf.LerpAngle(_panTilt.TiltAxis.Value, 0f, Time.deltaTime * _sirenPullSpeed);
        }
    }
}