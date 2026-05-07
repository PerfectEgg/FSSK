using UnityEngine;


// 크라켄
public class Kraken : MonsterTroll
{
    [SerializeField] private float _enterSpeed = 20f;  // 등장 속도

    [Header("크라켄 설정")]
    [SerializeField] private float _warningTime = 3f; // 경고 및 대기 시간 (3초)
    [SerializeField] private float _penaltyDuration = 3f; // 스턴 및 암전 시간 (3초)
    

    private bool _hasAttacked = false;

    // 연재 착수 모드인지 확인하는 플래그 (기본값은 true라고 가정하거나 초기화 필요)
    private bool _isExpansionMode = false;

    // 이벤트 구독 및 해제
    private void OnEnable() => TrollEvents.OnExpansionModeChanged += HandleModeChanged;
    private void OnDisable() => TrollEvents.OnExpansionModeChanged -= HandleModeChanged;

    private void HandleModeChanged(bool expansionModeActive)
    {
        _isExpansionMode = expansionModeActive;
    }

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void ApplyEffect()
    {
        Debug.Log("💥 [기절] 크라켄에게 한 대 맞았습니다!! 스턴 및 어지러움!");
        
        // TODO: UI 매니저 호출하여 3초 스턴 및 시야 암전 이벤트 발생
        TrollEvents.OnStunEffect?.Invoke(_penaltyDuration);
    }
    
    public override void EndTroll()
    {
        // 연출 종료 후 파괴
        Destroy(gameObject, 2f);
    }

    void OnDestroy()
    {
        // 트롤이 제거될 때 매니저에게 종료 알림
        TrollEvents.TriggerTrollFinished();
    }


    void Start()
    {
        Debug.Log("크라켄 등장!! 유의하세요!!");
    }

    private void Update() {
        if(transform.position.y <= 0f)
        {
            Vector3 targetHeight = new Vector3(transform.position.x, 0f, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetHeight, _enterSpeed * Time.deltaTime);
        }

        if (!_hasAttacked)
        {
            _currentTime += Time.deltaTime;

            // 2. 타이머가 3초에 도달하는 순간!
            if (_currentTime >= _warningTime)
            {
                _hasAttacked = true; // 더 이상 Update에서 실행되지 않도록 잠금
                EvaluateDodge();    // 회피 판정 시행
            }
        }
    }

    private void EvaluateDodge()
    {
        // 크라켄의 위치 판별
        bool isKrakenOnRight = transform.position.x > 0;
        float playerInput = 0f;

        // 플레이어의 현재 입력 상태 (-1: 왼쪽, 1: 오른쪽, 0: 입력 없음)
        if(_isExpansionMode)
        {
            playerInput = Input.GetAxisRaw("Horizontal"); 
        }

        bool isPressingLeft = playerInput < 0;
        bool isPressingRight = playerInput > 0;

        // 회피 성공 조건 판별
        bool isDodgeSuccess = (isKrakenOnRight && isPressingLeft) || (!isKrakenOnRight && isPressingRight);

        // 결과 처리
        if (isDodgeSuccess)
        {
            Debug.Log("✨ [회피 성공] 크라켄의 공격을 피했습니다!");
        }
        else
        {
            Debug.Log("💥 [회피 실패] 공격에 적중당했습니다! 3초 스턴 및 시야 암전!");
            ApplyEffect();
        }

        EndTroll(); // 공격 판정 후 트롤 종료
    }
}