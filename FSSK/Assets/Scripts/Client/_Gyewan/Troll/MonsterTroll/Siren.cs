using UnityEngine;


// 세이렌
public class Siren : MonsterTroll
{
    [Header("세이렌 설정")]
    [SerializeField] private float _fadeInTime = 2f;    // 등장 대기 시간
    [SerializeField] private float _activeTime = 6f;    // 효과 적용 시간 (총 8초)

    [Header("매혹 및 기절 설정")]
    [SerializeField] private float _stunThreshold = 3f; // 3초 누적 시 스턴
    [SerializeField] private float _stunDuration = 3f;  // 기절 유지 시간 (3초)
    [SerializeField] private float _immunityDuration = 3f; // 뺨 때리기 면역 시간

    // 내부 상태 변수
    private float _seductionTimer = 0f;
    private float _immunityTimer = 0f;

    
    private float _stunTimer = 0f;          // 타이머를 사용하여 기절 반복 구현
    private bool _isCameraPulled = false;   // 현재 시선이 강탈중인지 추적

    private void Start()
    {
        Debug.Log("🧜‍♀️ [세이렌 등장] 2초 뒤 매혹적인 노래가 시작됩니다!");
        // TODO: 페이드인 애니메이션 또는 사운드 실행
    }
    

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void ApplyEffect()
    {
        Debug.Log("💥 [매혹됨] 세이렌에게 완전히 홀렸습니다! 스턴 및 암전!");
        
        _stunTimer = _stunDuration;
        
        // TODO: UI 매니저 호출하여 3초 스턴 및 시야 암전 이벤트 발생
        GameEvents.OnStunEffect?.Invoke(_stunDuration);
    }

    public override void EndTroll() { }

    void Update()
    {
        _currentTime += Time.deltaTime;

        // 종료 시점 최우선 판정
        if(_currentTime > _fadeInTime + _activeTime)
        {
            EndSiren();
            return;
        }

        // 🟢 2. 효과 적용 구간 (2초 ~ 8초)
        if (_currentTime >= _fadeInTime)
        {
            // [상태 1] 기절(스턴) 중일 때
            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                
                if (_stunTimer <= 0f)
                {
                    Debug.Log("💫 [기절 종료] 정신을 차렸지만 아직 세이렌이 노래하고 있습니다!");
                    _seductionTimer = 0f; // 스턴이 끝나면 매혹 수치 다시 0부터 누적
                }
                return; // 기절 중에는 뺨 때리기(저항) 및 매혹 로직 무시
            }

            // [상태 2] 면역 상태 타이머 처리
            if (_immunityTimer > 0f)
            {
                _immunityTimer -= Time.deltaTime;
                
                if (_immunityTimer <= 0f)
                {
                    Debug.Log("🧜‍♀️ [면역 종료] 다시 세이렌의 노래에 홀리기 시작합니다!");
                    PullCamera(true); // 면역이 끝났으므로 다시 시선 강탈
                }
                return; // 면역 중에는 매혹 수치가 오르지 않음
            }

            // 이 구간에 진입했는데 아직 시선을 안 뺏었다면 뺏기 시작 (최초 1회 실행 보장)
            if (!_isCameraPulled)
            {
                PullCamera(true);
            }
            
            // [핵심] A키와 D키가 동시에 눌려있는지 확인 (정확한 타격을 위해 GetKey 사용)
            if (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D))
            {
                SlapCheek();
            }
            else
            {
                // 안 누르고 있다면 스턴 게이지 누적
                _seductionTimer += Time.deltaTime;
                if (_seductionTimer >= _stunThreshold)
                {
                    ApplyEffect();
                }
            }
        }
    }

    // 스스로 뺨을 때려 정신을 차리는 함수
    private void SlapCheek()
    {
        Debug.Log("👋 [정신 차리기!] 스스로 뺨을 때렸습니다! 3초간 세이렌 면역!");
        
        _immunityTimer = _immunityDuration; // 면역 3초 부여
        _seductionTimer = 0f;               // 누적된 스턴 수치 초기화
        
        PullCamera(false); // 면역이므로 시선 강탈 해제 (자유 시점 복구)
    }

    private void EndSiren()
    {
        Debug.Log("🧜‍♀️ [세이렌 퇴장] 노래가 끝났습니다. 조작이 정상화됩니다.");
        
        PullCamera(false); 
        Destroy(gameObject);
    }

    // 카메라 강제 이동 이벤트를 켜고 끄는 헬퍼 함수
    private void PullCamera(bool isPulling)
    {
        // 🟢 중복 호출 방지 (Update에서의 이벤트 스팸 방지)
        if (_isCameraPulled == isPulling) return;

        _isCameraPulled = isPulling;
        GameEvents.OnSirenEffect?.Invoke(isPulling, isPulling ? transform : null);
    }
}