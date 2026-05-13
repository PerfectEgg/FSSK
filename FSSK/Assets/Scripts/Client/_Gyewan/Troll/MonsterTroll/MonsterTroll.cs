using UnityEngine;
using Photon.Pun;
using Unity.VisualScripting; // 🟢 [멀티플레이] 포톤 네임스페이스 추가

public enum MonsterType { Kraken, Siren }
public enum MonsterState { Entering, Action, Exiting }

// 몬스터의 경우 (클릭 불가, 키보드로 방어)
public class MonsterTroll : TrollBase
{
    // 상태에 관한 변수들
    protected MonsterState _currentState = MonsterState.Entering;
    protected float _currentTime = 0f; // Update용 타이머

    protected virtual void Start()
    {
        if (GameEvents.IsGameOver) return;

        OnStateEnter(MonsterState.Entering);
    }
    
    protected virtual void Update()
    {
        // 🟢 [멀티플레이] 초기 스폰 연출 위치 계산은 주인(방장)만 수행합니다.
        if (!PhotonNetwork.IsMasterClient) return;
        if (GameEvents.IsGameOver) return;

        // 타이머 작동 (잡혀있지 않을 때만 시간이 흐름)
        _currentTime += Time.deltaTime;

        UpdateState();
    }

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void EndTroll()
    {
        if (TrollManager.ShouldPreserveWinningBlockerObject(gameObject))
        {
            return;
        }

        PhotonNetwork.Destroy(gameObject);
    }
    public override void ApplyEffect() { }

    // 🟢 [핵심 2] 상태 변경은 오직 주인(방장)만 지시하고, 결과를 모두에게 RPC로 뿌립니다!
    protected void ChangeState(MonsterState newState)
    {
        if (GameEvents.IsGameOver) return;

        if (PhotonNetwork.IsMasterClient) // ✅ 방장(주인)만 상태 변경 권한
        {
            // AllBuffered를 사용하면 늦게 들어온 클라이언트도 상태를 올바르게 동기화합니다.
            photonView.RPC("ChangeStateRPC", RpcTarget.AllBuffered, (int)newState);
        }
    }

    // 🟢 [핵심 3] 모든 클라이언트가 이 함수를 통해 동시에 상태에 진입합니다!
    // 이제 클라이언트의 애니메이터도 정상적으로 Exit 트리거를 받아 다음 애니메이션으로 넘어갑니다.
    [PunRPC]
    protected void ChangeStateRPC(int stateIndex)
    {
        if (GameEvents.IsGameOver) return;

        _currentState = (MonsterState)stateIndex;
        _currentTime = 0f;
        OnStateEnter(_currentState); 
    }

    // 상태에 막 진입했을 때 할 일 (무적 판정, 애니메이션 재생 등)
    protected virtual void OnStateEnter(MonsterState state) { }

    protected virtual void UpdateState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        switch(_currentState)
        {
            case MonsterState.Entering:
                break;
            case MonsterState.Action:
                break;
            case MonsterState.Exiting:
                EndTroll();
                break;
        }
    }
}
