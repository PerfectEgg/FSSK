using UnityEngine;

// 쥐
public class Rat : AnimalTroll
{
    [Header("쥐 설정")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _arrivalThreshold = 0.05f;

    [Header("비주얼 (입/손에 쥐는 돌)")]
    [SerializeField] private GameObject _heldGoldStoneVisual; // 입에 물고 있는 바둑돌 오브젝트 (평소엔 비활성화)
    [SerializeField] private GameObject _heldSilverStoneVisual; // 입에 물고 있는 바둑돌 오브젝트 (평소엔 비활성화)

    // 내부 상태 변수
    private Vector3 _startPosition;     // 시작 위치 (훔치고 돌아가기 위해 저장)
    private Transform _targetPosition;  // 훔칠 위치 (바둑 돌을 훔칠 위치)
    private int _targetStoneColor;      // 바둑돌의 색깔 (0: 빈칸[활용 X], 1: 금화(흑돌), 2: 은화(백돌))
    private bool _isTargetAssigned = false;

    void Start()
    {
        _waittingTime = 3f;
        _startPosition = transform.position; 

        if (_heldGoldStoneVisual != null) _heldGoldStoneVisual.SetActive(false);
        if (_heldSilverStoneVisual != null) _heldSilverStoneVisual.SetActive(false);
    }

    private void OnEnable()
    {
        // 매니저가 보내주는 타겟 정보를 수신 대기
        GameEvents.OnStoneTargetCallback += HandleTargetAssigned;
    }

    private void OnDisable()
    {
        GameEvents.OnStoneTargetCallback -= HandleTargetAssigned;
    }

    // 매니저로부터 목표 정보를 받았을 때 호출됨
    private void HandleTargetAssigned(int color, Transform pos)
    {
        // 이미 목표가 있거나 움직이는 중이면 무시
        if (_currentState != AnimalState.Action) return;

        _targetStoneColor = color;
        _targetPosition = pos;
        _isTargetAssigned = true;
        
        // Transform.position으로 방향 설정
        transform.LookAt(_targetPosition.position);
        Debug.Log($"🐀 [도둑쥐] 목표 설정 완료: {pos} (색상: {color})");
    }

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void EndTroll() 
    { 
        Destroy(gameObject, 1f); 
    }

    protected override void OnStateEnter(AnimalState state)
    {
        if(state == AnimalState.Entering || state == AnimalState.Action || state == AnimalState.Exiting)
            _isInteractable = false;
        else
            _isInteractable = true;

        if (state == AnimalState.Action)
        {
            _isTargetAssigned = false;
            GameEvents.TriggerRequestStoneToSteal();
        }
        else if (state == AnimalState.Exiting)
        {
            transform.LookAt(_startPosition);
        }
    }

    protected override void UpdateState()
    {
        switch(_currentState)
        {
            case AnimalState.Entering:
                if (_currentTime >= _enteringTime)
                    ChangeState(AnimalState.Waiting);
                break;
            case AnimalState.Waiting:
                if (_currentTime >= _waittingTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Action:
                // 🟢 타겟이 할당되었고, 타겟 Transform이 파괴되지 않고 존재하는지 체크
                if (_isTargetAssigned && _targetPosition != null)
                {
                    // _targetPosition.position을 참조하여 이동
                    transform.position = Vector3.MoveTowards(transform.position, _targetPosition.position, _moveSpeed * Time.deltaTime);

                    if (Vector3.Distance(transform.position, _targetPosition.position) <= _arrivalThreshold)
                    {
                        StealStone();
                    }
                }
                // 만약 쥐가 달려가고 있는데 돌이 사라졌다면? (플레이어가 먼저 주운 경우 등)
                else if (_isTargetAssigned && _targetPosition == null)
                {
                    Debug.Log("🐀 [도둑쥐] 목표물이 사라졌습니다! 빈손으로 돌아갑니다.");
                    ChangeState(AnimalState.Exiting);
                }
                break;
            case AnimalState.Exiting:
                transform.position = Vector3.MoveTowards(transform.position, _startPosition, _moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _startPosition) <= _arrivalThreshold)
                {
                    EndTroll();
                }
                break;
        }
    }

    // 🟢 돌을 훔치는 순간의 로직
    private void StealStone()
    {
        Debug.Log("🐀 [도둑쥐] 돌을 훔쳤습니다! 도망갑니다!");

        // 1. 매니저에게 이 좌표의 돌을 완전히 지워달라고 요청
        GameEvents.TriggerExecuteSteal();

        // 2. 비주얼 업데이트 (1: 금화/흑돌, 2: 은화/백돌)
        if (_targetStoneColor == 1 && _heldGoldStoneVisual != null)
            _heldGoldStoneVisual.SetActive(true);
        else if (_targetStoneColor == 2 && _heldSilverStoneVisual != null)
            _heldSilverStoneVisual.SetActive(true);

        // 3. 훔쳤으니 Exiting(도망가기) 상태로 전환
        ChangeState(AnimalState.Exiting);
    }
}