using UnityEngine;

// 쥐
public class Rat : AnimalTroll
{
    [Header("쥐 설정")]
    [SerializeField] private float _moveSpeed = 5f;


    [Header("비주얼 (입/손에 쥐는 돌)")]
    [SerializeField] private GameObject _heldGoldStoneVisual; // 입에 물고 있는 바둑돌 오브젝트 (평소엔 비활성화)
    [SerializeField] private GameObject _heldSilverStoneVisual; // 입에 물고 있는 바둑돌 오브젝트 (평소엔 비활성화)

    // 내부 상태 변수
    private Vector3 _startPosition;     // 시작 위치 (훔치고 돌아가기 위해 저장)
    private Transform _targetPosition;  // 훔칠 위치 (바둑 돌을 훔칠 위치)
    private int _targetStoneColor;      // 바둑돌의 색깔 (0: 빈칸[활용 X], 1: 금화(흑돌), 2: 은화(백돌))

    void Start()
    {
        _waittingTime = 3f;
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
        
        transform.LookAt(_targetPosition);
        _currentState = AnimalState.Action;
        Debug.Log($"🐀 [도둑쥐] 목표 설정 완료: {pos} (색상: {color})");
    }

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void EndTroll() 
    { 
        Destroy(gameObject, 1.5f); 
    }

    protected override void OnStateEnter(AnimalState state)
    {
        if(state == AnimalState.Entering || state == AnimalState.Action || state == AnimalState.Exiting)
            _isInteractable = false;
        else
            _isInteractable = true;
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
                
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }
}