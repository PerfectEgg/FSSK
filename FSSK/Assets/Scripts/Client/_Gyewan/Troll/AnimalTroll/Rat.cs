using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가

// 쥐
public class Rat : AnimalTroll
{
    [Header("쥐 설정")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _arrivalThreshold = 1f;

    [Header("비주얼 (입/손에 쥐는 돌)")]
    [SerializeField] private GameObject _heldGoldStoneVisual; // 입에 물고 있는 바둑돌 오브젝트 (평소엔 비활성화)
    [SerializeField] private GameObject _heldSilverStoneVisual; // 입에 물고 있는 바둑돌 오브젝트 (평소엔 비활성화)

    // 내부 상태 변수
    private Transform _targetPosition;  // 훔칠 위치 (바둑 돌을 훔칠 위치)
    private int _targetStoneColor;      // 바둑돌의 색깔 (0: 빈칸[활용 X], 1: 금화(흑돌), 2: 은화(백돌))
    private bool _isTargetAssigned = false;

    protected override void Start()
    {
        base.Start();

        // 애니메이터 컴포넌트 캐싱
        _animator = GetComponent<Animator>();
        

        if (_heldGoldStoneVisual != null) _heldGoldStoneVisual.SetActive(false);
        if (_heldSilverStoneVisual != null) _heldSilverStoneVisual.SetActive(false);

        // 🟢 [멀티플레이 핵심] 계산은 오직 방장(주인)만!
        if (!photonView.IsMine) return;

        _waittingTime = 3f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // 매니저가 보내주는 타겟 정보를 수신 대기
        TrollEvents.OnStoneTargetCallback += HandleTargetAssigned;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        TrollEvents.OnStoneTargetCallback -= HandleTargetAssigned;
    }

    // 매니저로부터 목표 정보를 받았을 때 호출됨
    private void HandleTargetAssigned(int color, Transform pos)
    {
        // 🟢 [멀티플레이 핵심] 계산은 오직 방장(주인)만!
        if (!photonView.IsMine) return;

        // 이미 목표가 있거나 움직이는 중이면 무시
        if (_currentState != AnimalState.Action) return;

        if (pos == null || color == 0)
        {
            _targetStoneColor = 0;
            _targetPosition = null;
            _isTargetAssigned = false;
            ChangeState(AnimalState.Hiding);
            return;
        }

        _targetStoneColor = color;
        _targetPosition = pos;
        _isTargetAssigned = true;
        
        // Transform.position으로 방향 설정
        transform.LookAt(_targetPosition.position);

        SendAnimationTrigger(_enterTrigger);
        Debug.Log($"🐀 [도둑쥐] 목표 설정 완료: {pos} (색상: {color})");
    }

    protected override void OnStateEnter(AnimalState state)
    {
        // 🟢 [멀티플레이 핵심] 계산은 오직 방장(주인)만!
        if (!photonView.IsMine) return;

        if(state == AnimalState.Entering || state == AnimalState.Action || state == AnimalState.Action2 || state == AnimalState.Exiting)
            _isInteractable = false;
        else
            _isInteractable = true;

        if (state == AnimalState.Action)
        {
            _isTargetAssigned = false;
            TrollEvents.TriggerRequestStoneToSteal();
        }
        
        if (state == AnimalState.Action2)
        {
            transform.LookAt(_finalSpawnPos);
        }

        // 2. 🟢 상태에 맞는 애니메이션 트리거 단 한 번 실행
        if (_animator != null)
        {
            // 사용하시는 트리거 변수들을 여기서 모두 Reset 해줍니다.
            SendAnimationReset(_enterTrigger);
            SendAnimationReset(_exitTrigger);
            
            switch(state)
            {
                case AnimalState.Hiding:
                    SendAnimationTrigger(_exitTrigger);
                    break;
            }
        }
    }

    protected override void UpdateState()
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
                // 🟢 타겟이 할당되었고, 타겟 Transform이 파괴되지 않고 존재하는지 체크
                if ( _targetPosition != null && _isTargetAssigned)
                {
                    // 🟢 수정: 목표 위치를 가져오되, Y축은 현재 쥐의 높이로 고정!
                    Vector3 moveTarget = _targetPosition.position;
                    moveTarget.y = transform.position.y;

                    // _targetPosition.position을 참조하여 이동
                    transform.position = Vector3.MoveTowards(transform.position, moveTarget, _moveSpeed * Time.deltaTime);

                    if (Vector3.Distance(transform.position, moveTarget) <= _arrivalThreshold)
                    {
                        StealStone();
                    }
                }
                // 만약 쥐가 달려가고 있는데 돌이 사라졌다면? (플레이어가 먼저 주운 경우 등)
                else if (_targetPosition == null && _isTargetAssigned)
                {
                    Debug.Log("🐀 [도둑쥐] 목표물이 사라졌습니다! 빈손으로 돌아갑니다.");
                    ChangeState(AnimalState.Action2);
                }
                break;
            case AnimalState.Action2:
                // 🟢 목표 위치로 도망가는 중 (Action2 상태)
                transform.position = Vector3.MoveTowards(transform.position, _finalSpawnPos, _moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _finalSpawnPos) <= _arrivalThreshold)
                {
                    ChangeState(AnimalState.Hiding);
                }
                break;
            case AnimalState.Hiding:
                SetHide();
                HideAction();
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }

    // 🟢 돌을 훔치는 순간의 로직
    private void StealStone()
    {
        Debug.Log("🐀 [도둑쥐] 돌을 훔쳤습니다! 도망갑니다!");

        // 1. 매니저에게 이 좌표의 돌을 완전히 지워달라고 요청
        TrollEvents.TriggerExecuteSteal();

        // 2. 🟢 [멀티플레이] 나뿐만 아니라 모두의 화면에서 쥐의 입에 돌이 물리도록 RPC 발송!
        photonView.RPC("SyncStoneVisualRPC", RpcTarget.All, _targetStoneColor);

        // 3. 훔쳤으니 Hiding(숨기) 상태로 전환
        ChangeState(AnimalState.Action2);
    }

    // 🟢 [멀티플레이 추가] 모든 클라이언트에서 실행되어 쥐의 비주얼을 맞춰주는 함수
    [PunRPC]
    private void SyncStoneVisualRPC(int color)
    {
        if (color == 1 && _heldGoldStoneVisual != null)
            _heldGoldStoneVisual.SetActive(true);
        else if (color == 2 && _heldSilverStoneVisual != null)
            _heldSilverStoneVisual.SetActive(true);
    }
}
