using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가
using IEnumerator = System.Collections.IEnumerator;
using Mono.Cecil.Cil;

public enum AnimalState { Entering, Waiting, Action, Action2, Hiding, Exiting }

// 동물의 경우 (마우스로 치울 수 있음)
public abstract class AnimalTroll : TrollBase, IDraggable 
{
    [Header("스폰 연출 설정 (오크통 등장)")]
    [SerializeField] protected float _spawnDepth = 2f;    // 오크통 아래로 파고들어 있을 깊이
    [SerializeField] protected float _spawnTiltAngle = -90f; // 🟢 비스듬히 숨어있을 X축 각도

    [Header("스폰 설정")]
    [SerializeField] protected float _spawnY = 0f;        // 스폰 시 Y축 높이 (책상 위에 뜨도록)

    // 애니메이션 캐싱용 변수
    protected Animator _animator;
    protected static readonly int _enterTrigger = Animator.StringToHash("Enter");
    protected static readonly int _exitTrigger = Animator.StringToHash("Exit");

    // 위치 캐싱용 변수
    protected Vector3 _finalSpawnPos;       // 최종적으로 등장할 위치 (책상 위)
    protected Vector3 _hiddenSpawnPos;      // 등장 연출을 위해 잠시 숨겨질 위치 (책상 아래)
    protected Vector3 _finalHidingPos;      // 최종적으로 숨을 위치 (책상 위)
    protected Vector3 _hiddenHidingPos;     // 숨는 연출을 위해 잠시 숨겨질 위치 (책상 아래)

    // 회전 캐싱용 변수
    protected Quaternion _finalSpawnRot;
    protected Quaternion _hiddenSpawnRot;
    protected Quaternion _finalHidingRot;

    protected Rigidbody _rb;
    protected bool _isGrabbed = false;   // 드래그 중인지 여부를 체크하는 변수
    protected bool _isOnTable = true;    // 현재 판(책상) 위에 있는지 여부를 체크하는 변수
    protected bool _isInteractable = false;  // 드래그 가능 여부

    // 상태에 관한 변수들
    protected AnimalState _currentState = AnimalState.Entering;
    protected float _currentTime = 0f;   // 총합 행동 시간
    protected float _enteringTime = 1f;  // 기본 진입 대기 시간 (상호작용 불가)
    protected float _waittingTime = 1f;  // 행동 직전 대기 시간 (상호작용 가능)

    [Header("Animal Settings")]
    [SerializeField] protected float _throwForce = 20f; // 던지는 힘
    private Vector3 _originalPosition;   // 움직이는 위치를 저장하고 되돌릴 때 사용할 변수

    protected virtual void OnEnable() => TrollEvents.OnTrollInteraction += HandleTrollInteraction;
    protected virtual void OnDisable() => TrollEvents.OnTrollInteraction -= HandleTrollInteraction;

    private void HandleTrollInteraction(bool isGrabbedEvent, GameObject target)
    {
        if (target != gameObject) return;

        if (isGrabbedEvent) OnDragStart();  // 잡힘 이벤트 -> 잡기 로직 실행
        else OnDragEnd();                   // 놓임 이벤트 -> 놓기 로직 실행
    }

    protected virtual void Start()
    {
        // 🟢 [멀티플레이] 초기 스폰 연출 위치 계산은 주인(방장)만 수행합니다.
        if (!photonView.IsMine) return;

        Vector3 spawnPoint = transform.position;
        spawnPoint.y = _spawnY; // Y축 높이 조정
        transform.position = spawnPoint;

        // 1. 최종 위치 및 회전값 기억
        _finalSpawnPos = transform.position;

        // 🟢 추가: 무조건 바둑판 중앙(예: Vector3.zero)을 바라보는 회전값을 '정면'으로 설정
        Vector3 lookTarget = new Vector3(0, transform.position.y, transform.position.z); // 바둑판 중앙 좌표

        // 내 원래 회전값이 아니라, 중앙을 바라보는 회전값을 최종 목표로 덮어씌움!
        _finalSpawnRot = Quaternion.LookRotation(lookTarget - transform.position);
        
        // 2. 숨어있을 위치(아래)와 회전값(X축 꺾임) 계산
        _hiddenSpawnPos = _finalSpawnPos + Vector3.down * _spawnDepth;
        _hiddenSpawnRot = _finalSpawnRot * Quaternion.Euler(_spawnTiltAngle, 0, 0);

        // 3. 땅속 초기 세팅
        transform.position = _hiddenSpawnPos;
        transform.rotation = _hiddenSpawnRot;
    }

    protected virtual void SetHide()
    {
        // 1. 최종 숨을 위치 및 회전값 기억
        _finalHidingPos = transform.position;

        // 2. 숨는 연출을 위해 잠시 숨겨질 위치(아래)와 회전값(X축 꺾임) 계산
        _hiddenHidingPos = _finalHidingPos + Vector3.down * _spawnDepth;
    }

    private void Awake() => _rb = GetComponent<Rigidbody>();

    private void Update()
    {
        // 🟢 [멀티플레이] 초기 스폰 연출 위치 계산은 주인(방장)만 수행합니다.
        if (!photonView.IsMine) return;

        // 잡힌 순간 타이머 작동 X
        if (_isGrabbed) return;

        // 타이머 작동 (잡혀있지 않을 때만 시간이 흐름)
        _currentTime += Time.deltaTime;

        _originalPosition = transform.position;

        UpdateState();
    }

    protected void ChangeState(AnimalState newState)
    {
        _currentState = newState;
        _currentTime = 0;        // 타이머 0으로 세팅
        OnStateEnter(newState); // 상태 진입 순간 1회 실행
    }

    // 상태에 막 진입했을 때 할 일 (무적 판정, 애니메이션 재생 등)
    protected virtual void OnStateEnter(AnimalState state)
    {
        if (state == AnimalState.Entering || state == AnimalState.Exiting)
            _isInteractable = false;
        else
            _isInteractable = true;
    }

    protected virtual void UpdateState()
    {
        switch(_currentState)
        {
            case AnimalState.Entering:
                EnterAction();
                break;
            case AnimalState.Waiting:
                if (_currentTime >= _waittingTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Action:
                break;
            case AnimalState.Hiding:
                HideAction();
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }

    protected virtual void OnDestroy()
    {
        // 🟢 [멀티플레이] 트롤 파괴 시 이벤트는 '주인(마지막으로 들고 있던 사람)'만 쏩니다.
        // 안 그러면 모든 유저의 컴퓨터에서 중복으로 이벤트가 발생해 웨이브 타이머가 꼬일 수 있습니다.
        if (photonView.IsMine)
        {
            TrollEvents.TriggerTrollFinished();
        }
    }


    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void ApplyEffect() {  }
    public override void EndTroll() 
    { 
        // 🟢 [멀티플레이 핵심 3] 일반 Destroy 대신 포톤 네트워크 파괴를 사용하되, 코루틴으로 3초 지연시킵니다.
        if (photonView.IsMine)
        {
            StartCoroutine(DelayedNetworkDestroy(3f));
        }
    }
    
    private IEnumerator DelayedNetworkDestroy(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        PhotonNetwork.Destroy(gameObject);
    }

    protected virtual void EnterAction()
    {
        // 0.0 ~ 1.0 사이의 진행률 계산
        float progress = _currentTime / _enteringTime;

        // 🟢 위치: 땅속에서 위로 부드럽게 상승
        transform.position = Vector3.Lerp(_hiddenSpawnPos, _finalSpawnPos, progress);
        
        // 🟢 회전: 90도로 숙인 상태에서 0도(원래 각도)로 부드럽게 세워짐
        transform.rotation = Quaternion.Slerp(_hiddenSpawnRot, _finalSpawnRot, progress);

        if (_currentTime >= _enteringTime)
        {
            // 🟢 오차 보정: 정확한 최종 위치/회전으로 딱 맞춰줌
            transform.position = _finalSpawnPos; 
            transform.rotation = _finalSpawnRot;
            
            ChangeState(AnimalState.Waiting);
        }
    }

    protected virtual void HideAction()
    {
        // 0.0 ~ 1.0 사이의 진행률 계산 (들어갈 때도 _enteringTime 활용)
        float progress = _currentTime / _enteringTime;

        // 🟢 위치: 위에서 땅속으로 부드럽게 하강 (Lerp 순서 반대)
        transform.position = Vector3.Lerp(_finalHidingPos, _hiddenHidingPos, progress);

        if (_currentTime >= _enteringTime)
        {
            // 🟢 오차 보정: 정확한 숨김 위치/회전으로 딱 맞춰줌
            transform.position = _hiddenHidingPos; 
            
            // 완전히 숨었으니 파괴! (OnDestroy에서 매니저에게 알림)
            ChangeState(AnimalState.Exiting);
        }
    }

    // --- IDraggable(인터페이스)의 메서드 구현 ---
    public void OnDragStart()
    {
        // 상호작용 불가능 상태면 리턴
        if(!_isInteractable) return;

        _isGrabbed = true;

        transform.position += Vector3.up * 1f;
        
    }

    public void OnDragEnd()
    {
        _isGrabbed = false;

        if (photonView.IsMine)
        {
            CheckDropLocation();
        }
    }

    private void CheckDropLocation()
    {
        // 드롭 시점에 bool 값만 확인하면 끝!
        if (!_isOnTable)
        {
            Debug.Log("장외로 치우기 성공!");

            // 물리 방어막을 해제해서 힘을 받을 수 있게 만듦
            if (_rb != null) 
            {
                _rb.isKinematic = false; 
                _rb.useGravity = true; // 중력도 필요하다면 켭니다.
            }

            Throw(Camera.main.transform.forward);
            
            ChangeState(AnimalState.Exiting);
        }
        else
        {
            Debug.Log("아직 책상 위입니다! 방해 계속 진행");
            transform.position = _originalPosition;     // 원래 위치로 되돌리기
        }
    }

    // --- Trigger 이벤트로 상태 스위칭 ---
    private void OnTriggerEnter(Collider other)
    {
        // "Table" 태그를 가진 책상 콜라이더 영역에 들어왔을 때
        if (other.CompareTag("Table"))
        {
            _isOnTable = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // "Table" 콜라이더 영역 밖으로 완전히 나갔을 때
        if (other.CompareTag("Table"))
        {
            _isOnTable = false;
        }
    }

    private void Throw(Vector3 direction)
    {
        _rb.AddForce(direction * _throwForce, ForceMode.Impulse);
    }
}

