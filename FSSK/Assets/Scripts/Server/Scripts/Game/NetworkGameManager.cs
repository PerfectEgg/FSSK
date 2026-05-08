using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// 게임 씬 네트워크 통합 매니저 (싱글톤 + PunRPC).
/// 권위 처리(턴 진행, 검증, 스폰 결정, 결과 판정)는 모두 마스터 클라이언트가 수행하고
/// 결과만 RPC로 전체 전파한다. 게임 씬에서만 살아있고, 로비로 돌아가면 같이 파괴됨.
///
/// ⚠️ 같은 GameObject에 PhotonView 컴포넌트가 반드시 붙어있어야 RPC가 동작.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class NetworkGameManager : MonoBehaviourPunCallbacks
{
    public static NetworkGameManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    //  공통 설정
    // ──────────────────────────────────────────────────────────────
    [Header("Game Timing")]
    [SerializeField] private float _turnDuration = 30f; // 턴 시간
    [SerializeField] private float _gameDuration = 600f; // 게임 시간

    // 추후 랭킹 계산 방법 논의
    [Header("Score Delta")]
    [SerializeField] private int _winScoreDelta = 10; // 플러스 점수
    [SerializeField] private int _loseScoreDelta = -10; // 마이너스 점수

    // 플레이어 스폰을 위한 변수
    [Header("Player Spawn")]
    [SerializeField] private Transform[] _spawnPoints; // 인스펙터에서 의자 2개 할당
    [SerializeField] private string _playerPrefabName = "Player/Player"; // Resources 폴더 안의 해적 프리팹 이름

    // ──────────────────────────────────────────────────────────────
    //  상태
    // ──────────────────────────────────────────────────────────────
    public int CurrentTurnActor { get; private set; } = -1; // 현재 플레이어 턴
    public float TurnTimeLeft { get; private set; } // 턴 시간
    public float GameTimeLeft { get; private set; } // 게임 시간
    public bool IsGameOver { get; private set; } // 게임 끝남 여부

    // 내 턴 여부
    public bool IsMyTurn =>
        PhotonNetwork.LocalPlayer != null &&
        CurrentTurnActor == PhotonNetwork.LocalPlayer.ActorNumber;

    private const float TIME_SYNC_INTERVAL = 1f; // RPC 보내는 주기
    private float _timeSyncAccum; // 동기화 시간 누적기
    private readonly HashSet<int> _claimedItemIds = new(); // 누군가 먹은 아이템(중복 x)

    // ──────────────────────────────────────────────────────────────
    //  Unity 라이프사이클
    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkGameManager] Duplicate instance detected, destroying.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 방에 들어온 '모든 클라이언트'는 무조건 자신의 캐릭터를 스폰합니다.
        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
        {
            SpawnMyPlayer();
        }

        if (PhotonNetwork.IsMasterClient)
            BeginGame();
    }

    // 플레이어 스폰 함수 추가 (안전한 나머지 연산자 사용)
    private void SpawnMyPlayer()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogError("🚨 [NetworkGameManager] 인스펙터에 스폰 포인트가 설정되지 않았습니다!");
            return;
        }

        int actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
        int safeIdx = (actorNum - 1) % _spawnPoints.Length; // 1번 유저는 0번 자리, 2번 유저는 1번 자리
        Transform spawnPoint = _spawnPoints[safeIdx];

        // ⚠️ 주의: 프리팹은 반드시 'Resources' 폴더 안에 있어야 합니다.
        PhotonNetwork.Instantiate(_playerPrefabName, spawnPoint.position, spawnPoint.rotation);
        
        Debug.Log($"[NetworkGameManager] 내 캐릭터 소환 완료 (Actor: {actorNum}, 자리: {safeIdx}번)");
    }

    void Update()
    {
        if (IsGameOver) return;
        if (!PhotonNetwork.IsMasterClient) return;

        TickTimers(Time.deltaTime);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────────────────────────────────────────────────────────
    //  1. 플레이어 턴 관리
    // ──────────────────────────────────────────────────────────────
    private void BeginGame()
    {
        var players = PhotonNetwork.PlayerList;
        if (players.Length == 0)
        {
            Debug.LogError("[NetworkGameManager] No players in room - cannot start game.");
            return;
        }

        GameTimeLeft = _gameDuration;
        IsGameOver = false;

        int firstActor = players[0].ActorNumber;
        photonView.RPC(nameof(StartTurnRpc), RpcTarget.AllBuffered, firstActor, _turnDuration);
        Debug.Log($"[NetworkGameManager] 게임 시작 - 첫 턴 actor: {firstActor}");
    }

    // 플레이어 턴 끝날 시
    public void RequestEndTurn()
    {
        if (!IsMyTurn)
        {
            Debug.Log("[NetworkGameManager] 턴 종료 요청 무시 - 내 턴이 아님");
            return;
        }
        photonView.RPC(nameof(RequestEndTurnRpc), RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    private void RequestEndTurnRpc(int requesterActor)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (requesterActor != CurrentTurnActor)
        {
            Debug.LogWarning($"[NetworkGameManager] EndTurn rejected - actor {requesterActor} is not current ({CurrentTurnActor}).");
            return;
        }
        AdvanceTurn();
    }

    // 턴 넘기기
    private void AdvanceTurn()
    {
        var players = PhotonNetwork.PlayerList;
        if (players.Length == 0) return;

        int currentIdx = System.Array.FindIndex(players, p => p.ActorNumber == CurrentTurnActor);
        int nextIdx = (currentIdx + 1) % players.Length;
        int nextActor = players[nextIdx].ActorNumber;

        photonView.RPC(nameof(StartTurnRpc), RpcTarget.AllBuffered, nextActor, _turnDuration);
        Debug.Log($"[NetworkGameManager] 턴 전환 {CurrentTurnActor} → {nextActor}");
    }

    [PunRPC]
    private void StartTurnRpc(int actorNumber, float duration)
    {
        CurrentTurnActor = actorNumber;
        TurnTimeLeft = duration;
        Debug.Log($"[NetworkGameManager] 턴 시작 (actor: {actorNumber}, duration: {duration:F1}s)");
        // TODO: UI 턴 표시 / 입력 활성화 분기 => 이벤트 함수 추가 필요
    }

    // ──────────────────────────────────────────────────────────────
    //  2. 게임 타이머 동기화 (마스터가 1초마다 클라들에 보정값 전파)
    // ──────────────────────────────────────────────────────────────
    private void TickTimers(float dt)
    {
        TurnTimeLeft = Mathf.Max(0f, TurnTimeLeft - dt);
        GameTimeLeft = Mathf.Max(0f, GameTimeLeft - dt);
        _timeSyncAccum += dt;

        // 시간 동기화 유지
        if (_timeSyncAccum >= TIME_SYNC_INTERVAL)
        {
            _timeSyncAccum = 0f;
            photonView.RPC(nameof(SyncTimeRpc), RpcTarget.Others, TurnTimeLeft, GameTimeLeft);
        }

        if (TurnTimeLeft <= 0f)
        {
            Debug.Log($"[NetworkGameManager] 턴 시간 초과 (actor: {CurrentTurnActor}) - 강제 전환");
            AdvanceTurn();
        }

        if (GameTimeLeft <= 0f && !IsGameOver)
        {
            Debug.Log("[NetworkGameManager] 게임 시간 종료");
            EndGameByTimeout();
        }
    }

    [PunRPC]
    private void SyncTimeRpc(float turnLeft, float gameLeft)
    {
        TurnTimeLeft = turnLeft;
        GameTimeLeft = gameLeft;
    }

    // ──────────────────────────────────────────────────────────────
    //  3. 착수 (좌표 → 마스터 검증 → 전체 전파)
    // ──────────────────────────────────────────────────────────────
    public void RequestPlaceStone(int x, int y)
    {
        if (!IsMyTurn)
        {
            Debug.Log("[NetworkGameManager] 착수 거부 - 내 턴이 아님");
            return;
        }
        photonView.RPC(nameof(RequestPlaceStoneRpc), RpcTarget.MasterClient,
            x, y, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    private void RequestPlaceStoneRpc(int x, int y, int requesterActor)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (requesterActor != CurrentTurnActor)
        {
            Debug.LogWarning($"[NetworkGameManager] Stone request rejected - actor {requesterActor} not current.");
            return;
        }

        // TODO: 보드 상태 검증(이미 착수된 자리, 놓을수 없는 자리 등) - 마스터 권위 처리
        bool valid = true;
        if (!valid) return;

        photonView.RPC(nameof(OnStonePlacedRpc), RpcTarget.AllBuffered, x, y, requesterActor);
    }

    [PunRPC]
    private void OnStonePlacedRpc(int x, int y, int actorNumber)
    {
        Debug.Log($"[NetworkGameManager] 착수 ({x}, {y}) by actor {actorNumber}");
        // TODO: 보드에서 (x, y) 좌표 돌 생성 + 승리 조건 체크 + 착수 소리 => 이벤트 함수 추가 필요
    }

   

    // ──────────────────────────────────────────────────────────────
    //  4. 스폰 및 파괴 (트롤, 아이템 등) - 프리팹은 Resources 폴더 필요
    // ──────────────────────────────────────────────────────────────
    public void SpawnNetworkObject(string prefabName, Vector3 pos, Quaternion rot)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetworkGameManager] Spawn requested by non-master - ignored.");
            return;
        }
        var obj = PhotonNetwork.Instantiate(prefabName, pos, rot);
        var pv = obj.GetComponent<PhotonView>();
        Debug.Log($"[NetworkGameManager] 스폰 '{prefabName}' (viewId: {(pv != null ? pv.ViewID : -1)})");
    }

    public void DestroyNetworkObject(GameObject obj)
    {
        if (obj == null) return;
        var pv = obj.GetComponent<PhotonView>();
        if (pv == null)
        {
            Debug.LogError("[NetworkGameManager] Destroy target has no PhotonView.");
            return;
        }

        if (PhotonNetwork.IsMasterClient || pv.IsMine)
        {
            PhotonNetwork.Destroy(obj);
            Debug.Log($"[NetworkGameManager] 파괴 (viewId: {pv.ViewID})");
        }
        else
        {
            photonView.RPC(nameof(RequestDestroyRpc), RpcTarget.MasterClient, pv.ViewID);
        }
    }

    [PunRPC]
    private void RequestDestroyRpc(int viewId)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        var pv = PhotonView.Find(viewId);
        if (pv != null) PhotonNetwork.Destroy(pv.gameObject);
    }

    // ──────────────────────────────────────────────────────────────
    //  5. 애니메이션 / 위치 공유 (연속 동기화는 PhotonTransformView/AnimatorView,
    //     이벤트성 트리거만 RPC)
    // ──────────────────────────────────────────────────────────────
    public void BroadcastAnimationTrigger(int viewId, string triggerName)
    {
        photonView.RPC(nameof(PlayAnimationTriggerRpc), RpcTarget.All, viewId, triggerName);
    }

    [PunRPC]
    private void PlayAnimationTriggerRpc(int viewId, string triggerName)
    {
        var pv = PhotonView.Find(viewId);
        if (pv == null) return;
        var animator = pv.GetComponent<Animator>();
        if (animator != null) animator.SetTrigger(triggerName);
    }

    // ──────────────────────────────────────────────────────────────
    //  6. 아이템 상호작용 검증 (먼저 잡은 사람 우선)
    //     수신 시 TrollEvents.OnItemCollected 발사 → 기존 클라 로직 자동 연동
    // ──────────────────────────────────────────────────────────────
    public void RequestPickupItem(int itemViewId, string itemTag)
    {
        photonView.RPC(nameof(RequestPickupItemRpc), RpcTarget.MasterClient,
            itemViewId, PhotonNetwork.LocalPlayer.ActorNumber, itemTag);
    }

    [PunRPC]
    private void RequestPickupItemRpc(int itemViewId, int actorNumber, string itemTag)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (_claimedItemIds.Contains(itemViewId))
        {
            Debug.Log($"[NetworkGameManager] 아이템 획득 거부 - 이미 처리됨 (item: {itemViewId}, late actor: {actorNumber})");
            return;
        }
        _claimedItemIds.Add(itemViewId);

        photonView.RPC(nameof(OnItemPickedRpc), RpcTarget.AllBuffered, itemViewId, actorNumber, itemTag);
    }

    [PunRPC]
    private void OnItemPickedRpc(int itemViewId, int actorNumber, string itemTag)
    {
        Debug.Log($"[NetworkGameManager] 아이템 획득 (item: {itemViewId}, actor: {actorNumber}, tag: {itemTag})");
        var pv = PhotonView.Find(itemViewId);
        if (pv != null)
            TrollEvents.TriggerItemCollected(itemTag, pv.gameObject);
    }

    // ──────────────────────────────────────────────────────────────
    //  7. 환경 / 몬스터 효과 (마스터 결정 → 전체 동기화)
    //     RPC 수신부에서 TrollEvents.* 발사 → 기존 클라 코드(트롤 AI, UI, 카메라) 자동 연동
    //     특정 플레이어 대상 효과(Stun, Siren)는 actorNumber 비교로 본인만 적용
    // ──────────────────────────────────────────────────────────────

    // --- 기절 (특정 플레이어 대상/크라켄, 세이렌) ---
    public void BroadcastStun(int targetActor, float duration)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetworkGameManager] Stun broadcast requested by non-master - ignored.");
            return;
        }
        photonView.RPC(nameof(StunRpc), RpcTarget.All, targetActor, duration);
    }

    [PunRPC]
    private void StunRpc(int targetActor, float duration)
    {
        Debug.Log($"[NetworkGameManager] 기절 수신 (target: {targetActor}, duration: {duration:F1}s)");
        if (PhotonNetwork.LocalPlayer == null || PhotonNetwork.LocalPlayer.ActorNumber != targetActor) return;
        TrollEvents.OnStunEffect?.Invoke(duration);
    }

    // --- 세이렌 매혹 (전체 플레이어 대상, source는 viewId로 전달) ---
    public void BroadcastSirenEffect(bool active, int sourceViewId)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetworkGameManager] Siren broadcast requested by non-master - ignored.");
            return;
        }
        photonView.RPC(nameof(SirenEffectRpc), RpcTarget.All, active, sourceViewId);
    }

    [PunRPC]
    private void SirenEffectRpc(bool active, int sourceViewId)
    {
        Debug.Log($"[NetworkGameManager] 세이렌 효과 수신 (active: {active}, source: {sourceViewId})");

        Transform sourceTransform = null;
        if (sourceViewId > 0)
        {
            var pv = PhotonView.Find(sourceViewId);
            if (pv != null) sourceTransform = pv.transform;
        }
        TrollEvents.OnSirenEffect?.Invoke(active, sourceTransform);
    }

    // --- 웨이브 단계 (전체 공통) ---
    public void BroadcastWaveStage(int stage)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetworkGameManager] WaveStage broadcast requested by non-master - ignored.");
            return;
        }
        photonView.RPC(nameof(WaveStageRpc), RpcTarget.AllBuffered, stage);
    }

    [PunRPC]
    private void WaveStageRpc(int stage)
    {
        Debug.Log($"[NetworkGameManager] 웨이브 단계 수신 ({stage})");
        TrollEvents.OnWaveStageChanged?.Invoke(stage);
    }

    // --- 환경 효과 레벨(0~3) - Rain / Wind / Lightning ---
    public void BroadcastRainLevel(int level)
    {
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[NetworkGameManager] Rain broadcast non-master - ignored."); return; }
        photonView.RPC(nameof(RainLevelRpc), RpcTarget.AllBuffered, level);
    }

    [PunRPC]
    private void RainLevelRpc(int level)
    {
        Debug.Log($"[NetworkGameManager] 비 레벨 수신 ({level})");
        TrollEvents.OnRainLevelChanged?.Invoke(level);
    }

    public void BroadcastWindLevel(int level)
    {
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[NetworkGameManager] Wind broadcast non-master - ignored."); return; }
        photonView.RPC(nameof(WindLevelRpc), RpcTarget.AllBuffered, level);
    }

    [PunRPC]
    private void WindLevelRpc(int level)
    {
        Debug.Log($"[NetworkGameManager] 바람 레벨 수신 ({level})");
        TrollEvents.OnWindLevelChanged?.Invoke(level);
    }

    public void BroadcastLightningLevel(int level)
    {
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[NetworkGameManager] Lightning broadcast non-master - ignored."); return; }
        photonView.RPC(nameof(LightningLevelRpc), RpcTarget.AllBuffered, level);
    }

    [PunRPC]
    private void LightningLevelRpc(int level)
    {
        Debug.Log($"[NetworkGameManager] 번개 레벨 수신 ({level})");
        TrollEvents.OnLightningLevelChanged?.Invoke(level);
    }

    // ──────────────────────────────────────────────────────────────
    //  8. 게임 결과 기록 (점수 처리는 BackendRank 통해)
    // ──────────────────────────────────────────────────────────────
    public void EndGame(int winnerActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (IsGameOver) return;
        photonView.RPC(nameof(OnGameEndRpc), RpcTarget.AllBuffered, winnerActorNumber);
    }

    private void EndGameByTimeout()
    {
        // -1 = 무승부. 시간 초과 규칙(점수 우위 등)은 추후 정의
        EndGame(-1);
    }

    [PunRPC]
    private void OnGameEndRpc(int winnerActorNumber)
    {
        if (IsGameOver) return;
        IsGameOver = true;

        bool draw = winnerActorNumber == -1;
        bool iWon = winnerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;

        Debug.Log($"[NetworkGameManager] 게임 종료 (winner: {winnerActorNumber}, 결과: {(draw ? "무승부" : iWon ? "승" : "패")})");

        if (draw)
        {
            ReturnToLobby();
            return;
        }

        ApplyMatchResult(iWon ? _winScoreDelta : _loseScoreDelta);
    }

    private void ApplyMatchResult(int delta)
    {
        if (BackendRank.Instance == null || BackendManager.Instance == null || BackendManager.Instance.MyUserData == null)
        {
            Debug.LogError("[NetworkGameManager] BackendRank/BackendManager not ready.");
            ReturnToLobby();
            return;
        }

        int next = BackendManager.Instance.MyUserData.score + delta;
        BackendRank.Instance.UpdateMyScore(next,
            onSuccess: () =>
            {
                BackendManager.Instance.MyUserData.score = next;
                Debug.Log($"[NetworkGameManager] 점수 갱신 완료 ({next}점)");
                ReturnToLobby();
            },
            onFail: err =>
            {
                Debug.LogError($"[NetworkGameManager] UpdateMyScore failed: {err}");
                ReturnToLobby();
            });
    }

    private void ReturnToLobby()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.LeaveRoom();
        }
        if (BackendManager.Instance != null) BackendManager.Instance.LoadLobbyScene();
    }

    // ──────────────────────────────────────────────────────────────
    //  9. 예외 상황 처리 (상대 퇴장, 본인 끊김, 마스터 이전)
    // ──────────────────────────────────────────────────────────────
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[NetworkGameManager] 상대 퇴장 '{otherPlayer.NickName}' (actor: {otherPlayer.ActorNumber})");
        if (IsGameOver) return;

        // 1대1 가정 - 잔류자가 마스터가 되어 본인 승리 처리
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            int winner = PhotonNetwork.LocalPlayer.ActorNumber;
            Debug.Log($"[NetworkGameManager] 1인 잔류 - 잔류자 승리 처리 (actor: {winner})");
            EndGame(winner);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[NetworkGameManager] Disconnected (cause: {cause})");
        if (IsGameOver) return;
        IsGameOver = true;

        // 본인 끊김 - 패배 점수만 반영하고 로비 복귀 (RPC는 이미 못 보냄)
        if (BackendRank.Instance != null && BackendManager.Instance != null && BackendManager.Instance.MyUserData != null)
        {
            int next = BackendManager.Instance.MyUserData.score + _loseScoreDelta;
            BackendRank.Instance.UpdateMyScore(next,
                onSuccess: () =>
                {
                    BackendManager.Instance.MyUserData.score = next;
                    if (BackendManager.Instance != null) BackendManager.Instance.LoadLobbyScene();
                },
                onFail: err =>
                {
                    Debug.LogError($"[NetworkGameManager] Disconnect penalty failed: {err}");
                    if (BackendManager.Instance != null) BackendManager.Instance.LoadLobbyScene();
                });
        }
        else if (BackendManager.Instance != null)
        {
            BackendManager.Instance.LoadLobbyScene();
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[NetworkGameManager] 마스터 클라 변경 → '{newMasterClient.NickName}' (actor: {newMasterClient.ActorNumber})");
        // 새 마스터로 권한 자동 이전
    }
}
