using UnityEngine;
using Photon.Pun; 

public class Siren : MonsterTroll
{
    [Header("세이렌 설정")]
    [SerializeField] private float _fadeInTime = 2f;    
    [SerializeField] private float _activeTime = 6f;    

    [Header("매혹 및 기절 설정")]
    [SerializeField] private float _stunThreshold = 3f; 
    [SerializeField] private float _stunDuration = 3f;  
    [SerializeField] private float _immunityDuration = 3f; 

    [Header("사운드 설정")]
    [SerializeField] private AudioClip _enterSound;

    private float _seductionTimer = 0f;
    private float _immunityTimer = 0f;
    private float _stunTimer = 0f;          
    private bool _isCameraPulled = false;   
    
    // 🟢 로컬에서 노래가 들리는 중인지 판별하는 플래그
    private bool _isSingingLocally = false; 

    protected override void Start()
    {
        base.Start();

        if (!PhotonNetwork.IsMasterClient) return;

        // 🟢 [핵심] 소환된 위치에 따른 방향 가중치 계산
        // 오른쪽에 있으면 1, 왼쪽에 있으면 -1
        float rotationMultiplier = (transform.position.x > 0) ? 180f : 0f;

        Vector3 lookDirection = new Vector3(0, rotationMultiplier, 0);
        transform.rotation = Quaternion.Euler(lookDirection);

        Debug.Log("세이렌! 등장!! 유의하세요!!");
    }

    // --- 1. [방장 & 클라이언트 공통] 상태 진입 시 연출 ---
    protected override void OnStateEnter(MonsterState state)
    {
        if (state == MonsterState.Entering)
        {
            Debug.Log("🧜‍♀️ [세이렌 등장] 2초 뒤 매혹적인 노래가 시작됩니다!");

            Debug.Log($"🧜‍♀️ [세이렌 등장] 등장 사운드 재생 요청 발사!");
            photonView.RPC("RPC_EnterSound", RpcTarget.All);
            
        }
        
        if (state == MonsterState.Action)
        {
            // 방장이 상태를 Action으로 넘기면, 모두에게 "노래 시작!" 방송
            if (PhotonNetwork.IsMasterClient) 
                photonView.RPC("RPC_StartSinging", RpcTarget.All);
        }
        
        if (state == MonsterState.Exiting)
        {
            // 방장이 상태를 Exiting으로 넘기면, 모두에게 "노래 끝!" 방송
            if (PhotonNetwork.IsMasterClient) 
                photonView.RPC("RPC_StopSinging", RpcTarget.All);
        }
    }

    // --- 2. [방장 전용] 글로벌 상태 관리 및 생명주기 통제 ---
    protected override void UpdateState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        switch(_currentState)
        {
            case MonsterState.Entering:
                if (_currentTime >= _fadeInTime) 
                    ChangeState(MonsterState.Action);
                break;
            
            case MonsterState.Action:
                // 페이드인 시간 + 노래 부르는 시간이 지나면 퇴장
                if (_currentTime >= _activeTime) 
                    ChangeState(MonsterState.Exiting);
                break;
            
            case MonsterState.Exiting:
                EndTroll(); // 방장만 안전하게 PhotonNetwork.Destroy 실행
                break;
        }
    }

    // --- 3. [RPC] 클라이언트 로컬 스위치 ON/OFF ---
    [PunRPC]
    private void RPC_EnterSound()
    {
        Debug.Log($"🧜‍♀️ [세이렌 등장] 등장 사운드 재생 요청 받음");

        if (TrollEvents.IsGameplayEventBlocked) return;

        if (_enterSound == null) 
        {
            Debug.LogError("🚨 [Siren] _enterSound 클립이 비어있습니다! 인스펙터를 확인하세요.");
            return;
        }

        SoundEvents.Play3DSFX_Cut?.Invoke(_enterSound, transform.position, 0.45f, 8f, 2f); // 등장 사운드 재생
    }

    [PunRPC]
    private void RPC_StartSinging()
    {
        if (TrollEvents.IsGameplayEventBlocked)
        {
            _isSingingLocally = false;
            _isCameraPulled = false;
            return;
        }

        _isSingingLocally = true;
        PullCamera(true);
    }

    [PunRPC]
    private void RPC_StopSinging()
    {
        _isSingingLocally = false;
        PullCamera(false);
        Debug.Log("🧜‍♀️ [세이렌 퇴장] 노래가 끝났습니다. 조작이 정상화됩니다.");
    }

    // --- 4. [클라이언트 로컬] 내 뺨 때리기 및 기절 판정 (독립 루프) ---
    protected override void Update()
    {
        base.Update(); // 🟢 [핵심] 부모의 타이머 로직(_currentTime)과 UpdateState()를 실행시킵니다!

        // 방장의 허락(RPC)이 떨어졌을 때만 판정 시작
        if (!_isSingingLocally) return;
        if (TrollEvents.IsGameplayEventBlocked)
        {
            _isSingingLocally = false;
            _isCameraPulled = false;
            return;
        }

        // [상태 1] 기절 중
        if (_stunTimer > 0f)
        {
            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0f)
            {
                Debug.Log("💫 [기절 종료] 정신을 차렸지만 여전히 노래가 들립니다!");
                _seductionTimer = 0f; 
            }
            return; 
        }

        // [상태 2] 면역 중
        if (_immunityTimer > 0f)
        {
            _immunityTimer -= Time.deltaTime;
            if (_immunityTimer <= 0f)
            {
                Debug.Log("🧜‍♀️ [면역 종료] 다시 세이렌의 노래에 홀립니다!");
                PullCamera(true); 
            }
            return; 
        }
        
        // [상태 3] 정상 상태 (입력 판정 및 스턴 게이지 누적)
        if (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D))
        {
            SlapCheek();
        }
        else
        {
            _seductionTimer += Time.deltaTime;
            if (_seductionTimer >= _stunThreshold)
            {
                ApplyEffect();
            }
        }
    }

    // --- 이벤트 연출 메서드 ---
    public override void ApplyEffect()
    {
        Debug.Log("💥 [매혹됨] 세이렌에게 완전히 홀렸습니다! 스턴 및 암전!");
        _stunTimer = _stunDuration;
        TrollEvents.TriggerStunEffect(_stunDuration);
    }

    private void SlapCheek()
    {
        Debug.Log("👋 [정신 차리기!] 스스로 뺨을 때렸습니다! 3초간 세이렌 면역!");
        _immunityTimer = _immunityDuration; 
        _seductionTimer = 0f;               
        PullCamera(false); 
    }

    private void PullCamera(bool isPulling)
    {
        if (TrollEvents.IsGameplayEventBlocked && isPulling) return;
        if (_isCameraPulled == isPulling) return;
        _isCameraPulled = isPulling;
        TrollEvents.TriggerSirenEffect(isPulling, isPulling ? transform : null);
    }

    void OnDestroy()
    {
        if (PhotonNetwork.IsMasterClient)
            TrollEvents.TriggerTrollFinished();
    }
}
