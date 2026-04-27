using UnityEngine;
using Unity.Cinemachine; // 시네머신 3 네임스페이스

public class CameraModeController : MonoBehaviour
{
    [Header("Cameras")]
    public CinemachineCamera focusCam;              // 70도 고정 착수 카메라
    public CinemachineCamera expansionCam;          // Pan Tilt 회전 제한 확장 카메라

    private CinemachinePanTilt _panTilt;

    private bool isExpansionMode = false;

    void Awake()
    {
        if (expansionCam != null)
        {
            _panTilt = expansionCam.GetComponent<CinemachinePanTilt>();
        }
    }

    void Start()
    {
        // 초기 상태: 착수 모드
        SetCameraMode(false);
    }

    void Update()
    {
        // Space 키로 모드 전환
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isExpansionMode = !isExpansionMode;
            SetCameraMode(isExpansionMode);
        }
    }

    private void SetCameraMode(bool expansion)
    {
        if (expansion)
        {
            // 확장 모드: Pan Tilt 카메라 활성화, 커서 고정
            if (_panTilt != null)
            {
                _panTilt.PanAxis.Value = 0f;
                _panTilt.TiltAxis.Value = 20f;
            }
            focusCam.Priority = 10;
            expansionCam.Priority = 20;
            Cursor.lockState = CursorLockMode.Locked; 
        }
        else
        {
            // 착수 모드: 고정 카메라 활성화, 커서 해제
            focusCam.Priority = 20;
            expansionCam.Priority = 10;
            Cursor.lockState = CursorLockMode.None;
        }

        GameEvents.TriggerExpansionMode(expansion);
    }
}