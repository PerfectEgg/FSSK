using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Unity.Cinemachine;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
/// <summary>
/// 게임 씬 네트워크 통합 매니저 (싱글톤 + PunRPC).
/// 게임 전체 시간, 스폰/파괴, (트롤·환경 효과 broadcast), 종료 처리만 담당한다.
/// 오목 턴 진행/턴 타이머는 OmokTurnSystem, 착수 동기화는 OmokPhotonAuthorityAdapter 가 담당.
/// 게임 씬에서만 살아있고, 로비로 돌아가면 같이 파괴됨.
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
    [SerializeField] private float _gameDuration = 600f; // 게임 전체 시간 (턴 진행/타이머는 OmokTurnSystem 담당)

    // 추후 랭킹 계산 방법 논의
    [Header("Score Delta")]
    [SerializeField] private int _winScoreDelta = 10; // 플러스 점수
    [SerializeField] private int _loseScoreDelta = -10; // 마이너스 점수

    [Header("Result Panel")]
    [SerializeField] private GameResultPanel _resultPanel;

    [Header("Result Sequence")]
    [SerializeField] private bool _flashWinningLineBeforeResult = true;
    [SerializeField, Min(1)] private int _winningLineFlashCount = 2;
    [SerializeField, Min(0f)] private float _winningLineFlashOnSeconds = 0.45f;
    [SerializeField, Min(0f)] private float _winningLineFlashOffSeconds = 0.25f;

    [Header("Timeout Result Sequence")]
    [SerializeField] private bool _fadeScreenBeforeTimeoutResult = true;
    [SerializeField, Min(0f)] private float _timeoutFadeDuration = 0.85f;
    [SerializeField, Range(0f, 1f)] private float _timeoutFadeTargetAlpha = 0.72f;
    [SerializeField] private Color _timeoutFadeColor = Color.black;

    // 플레이어 스폰을 위한 변수
    [Header("Player Spawn")]
    [SerializeField] private Transform[] _spawnPoints; // 인스펙터에서 의자 2개 할당
    [SerializeField] private string _playerPrefabName = "Player/Player"; // Resources 폴더 안의 해적 프리팹 이름
    [SerializeField] private bool _spawnSoloOpponentCharacter = true;


    // ──────────────────────────────────────────────────────────────
    //  상태
    // ──────────────────────────────────────────────────────────────
    public float GameTimeLeft { get; private set; } // 게임 전체 남은 시간
    public float GameDuration => _gameDuration;
    public bool IsGameOver { get; private set; } // 게임 끝남 여부

    private const float TIME_SYNC_INTERVAL = 1f; // RPC 보내는 주기
    private const int DrawWinnerActorNumber = -1;
    private const int TimeoutWinnerActorNumber = -2;
    private const float WinningLineWaitTimeoutSeconds = 0.35f;
    private float _timeSyncAccum; // 동기화 시간 누적기
    private readonly HashSet<int> _claimedItemIds = new(); // 누군가 먹은 아이템(중복 x)
    private bool _spawnedMyPlayer;
    private bool _spawnedSoloOpponentCharacter;
    private bool _returningToLobby;
    private Coroutine _pendingResultSequence;
    private Image _timeoutFadeOverlayImage;

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
        if (CanSpawnMyPlayer())
        {
            SpawnMyPlayer();
        }

        if (CanSpawnSoloOpponentCharacter())
        {
            SpawnSoloOpponentCharacter();
        }

        if (PhotonNetwork.IsMasterClient)
            BeginGame();
        
    }

    // 플레이어 스폰 함수 추가 (안전한 나머지 연산자 사용)
    private void SpawnMyPlayer()
    {
        if (_spawnedMyPlayer)
        {
            return;
        }

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
        _spawnedMyPlayer = true;
        
        Debug.Log($"[NetworkGameManager] 내 캐릭터 소환 완료 (Actor: {actorNum}, 자리: {safeIdx}번)");
    }

    public override void OnJoinedRoom()
    {
        if (CanSpawnMyPlayer())
        {
            SpawnMyPlayer();
        }

        if (CanSpawnSoloOpponentCharacter())
        {
            SpawnSoloOpponentCharacter();
        }
    }

    private bool CanSpawnMyPlayer()
    {
        return !_spawnedMyPlayer &&
               PhotonNetwork.InRoom &&
               (PhotonNetwork.IsConnectedAndReady || PhotonNetwork.OfflineMode);
    }

    private bool CanSpawnSoloOpponentCharacter()
    {
        return _spawnSoloOpponentCharacter &&
               !_spawnedSoloOpponentCharacter &&
               PhotonNetwork.OfflineMode &&
               PhotonNetwork.InRoom;
    }

    private void SpawnSoloOpponentCharacter()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogError("[NetworkGameManager] Solo opponent spawn failed: spawn points are not assigned.");
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(_playerPrefabName);
        if (prefab == null)
        {
            Debug.LogError($"[NetworkGameManager] Solo opponent spawn failed: Resources/{_playerPrefabName} not found.");
            return;
        }

        int opponentSpawnIndex = _spawnPoints.Length > 1 ? 1 : 0;
        Transform spawnPoint = _spawnPoints[opponentSpawnIndex];
        GameObject opponent = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        opponent.name = $"{prefab.name}_SoloOpponent";
        ConfigureSoloOpponentCharacter(opponent);

        _spawnedSoloOpponentCharacter = true;
        Debug.Log($"[NetworkGameManager] Solo opponent character spawned (spawnIndex: {opponentSpawnIndex}).");
    }

    private static void ConfigureSoloOpponentCharacter(GameObject opponent)
    {
        if (opponent == null)
        {
            return;
        }

        foreach (CameraModeController component in opponent.GetComponentsInChildren<CameraModeController>(true))
        {
            component.enabled = false;
        }

        foreach (PlayerController component in opponent.GetComponentsInChildren<PlayerController>(true))
        {
            component.enabled = false;
        }

        foreach (PlayerInteraction component in opponent.GetComponentsInChildren<PlayerInteraction>(true))
        {
            component.enabled = false;
        }

        foreach (CinemachineCamera component in opponent.GetComponentsInChildren<CinemachineCamera>(true))
        {
            component.gameObject.SetActive(false);
        }

        foreach (CinemachineInputAxisController component in opponent.GetComponentsInChildren<CinemachineInputAxisController>(true))
        {
            component.enabled = false;
        }

        foreach (PhotonAnimatorView component in opponent.GetComponentsInChildren<PhotonAnimatorView>(true))
        {
            component.enabled = false;
        }

        foreach (PhotonTransformView component in opponent.GetComponentsInChildren<PhotonTransformView>(true))
        {
            component.enabled = false;
        }

        foreach (PhotonView component in opponent.GetComponentsInChildren<PhotonView>(true))
        {
            component.enabled = false;
        }
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
    //  1. 게임 시작 / 게임 전체 시간 동기화
    //     (턴 진행/턴 타이머는 OmokTurnSystem 이, 착수 동기화는 OmokPhotonAuthorityAdapter 가 담당)
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
        Debug.Log($"[NetworkGameManager] 게임 시작 (전체 시간: {_gameDuration:F0}s)");
    }

    private void TickTimers(float dt)
    {
        GameTimeLeft = Mathf.Max(0f, GameTimeLeft - dt);
        _timeSyncAccum += dt;
        if (_timeSyncAccum >= TIME_SYNC_INTERVAL)
        {
            _timeSyncAccum = 0f;
            photonView.RPC(nameof(SyncTimeRpc), RpcTarget.Others, GameTimeLeft);
            //Debug.Log($"[NetworkGameManager] 게임 남은 시간 (master): {GameTimeLeft:F1}s");
        }

        if (GameTimeLeft <= 0f && !IsGameOver)
        {
            Debug.Log("[NetworkGameManager] 게임 시간 종료");
            EndGameByTimeout();
        }
    }

    [PunRPC]
    private void SyncTimeRpc(float gameLeft)
    {
        GameTimeLeft = gameLeft;
        //Debug.Log($"[NetworkGameManager] 게임 남은 시간 (remote): {GameTimeLeft:F1}s");
    }

    // ──────────────────────────────────────────────────────────────
    //  2. 스폰 및 파괴 (트롤, 아이템 등) - 프리팹은 Resources 폴더 필요
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
    //  3. 애니메이션 / 위치 공유 (연속 동기화는 PhotonTransformView/AnimatorView,
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
    //  4. 환경 / 몬스터 효과 (마스터 결정 → 전체 동기화)
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

    public void BroadcastLightningStrike(int level, int patternIndex)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetworkGameManager] Lightning strike broadcast requested by non-master - ignored.");
            return;
        }

        int clampedLevel = Mathf.Clamp(level, 0, 3);
        int clampedPatternIndex = Mathf.Clamp(patternIndex, 1, 3);
        photonView.RPC(nameof(LightningStrikeRpc), RpcTarget.All, clampedLevel, clampedPatternIndex);
    }

    [PunRPC]
    private void LightningStrikeRpc(int level, int patternIndex)
    {
        Debug.Log($"[NetworkGameManager] Lightning strike received (level: {level}, pattern: {patternIndex})");
        TrollEvents.OnLightningStrikeRequested?.Invoke(level, patternIndex);
    }

    // ──────────────────────────────────────────────────────────────
    //  5. 게임 결과 기록 (점수 처리는 BackendRank 통해)
    // ──────────────────────────────────────────────────────────────
    public void EndGame(int winnerActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (IsGameOver) return;
        photonView.RPC(nameof(OnGameEndRpc), RpcTarget.AllBuffered, winnerActorNumber);
    }

    private void EndGameByTimeout()
    {
        EndGame(TimeoutWinnerActorNumber);
    }

    [PunRPC]
    private void OnGameEndRpc(int winnerActorNumber)
    {
        if (IsGameOver) return;
        IsGameOver = true;

        bool draw = winnerActorNumber == DrawWinnerActorNumber;
        bool timeout = winnerActorNumber == TimeoutWinnerActorNumber;
        if (timeout)
        {
            GameTimeLeft = 0f;
        }

        bool iWon = !draw && !timeout && winnerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
        int delta = iWon ? _winScoreDelta : _loseScoreDelta;
        GameResultType resultType = timeout
            ? GameResultType.Timeout
            : draw
                ? GameResultType.Draw
                : (iWon ? GameResultType.Win : GameResultType.Lose);

        Debug.Log($"[NetworkGameManager] 게임 종료 (winner: {winnerActorNumber}, 결과: {resultType}, delta: {delta:+#;-#;0})");

        GameEvents.TriggerGameOver();
        if (!draw)
        {
            GameEvents.TriggerGameOverResult(iWon);
        }

        if (draw || timeout)
        {
            OmokMatchManager match = FindFirstObjectByType<OmokMatchManager>();
            if (match != null) match.ForceEndMatchAsDraw();
        }

        int currentScore = (BackendManager.Instance != null && BackendManager.Instance.MyUserData != null)
            ? BackendManager.Instance.MyUserData.score
            : 0;

        if (_resultPanel != null)
        {
            if (ShouldDelayResultForWinningLine(resultType))
            {
                _resultPanel.Hide();
                if (_pendingResultSequence != null)
                {
                    StopCoroutine(_pendingResultSequence);
                }
                _pendingResultSequence = StartCoroutine(ShowResultAfterWinningLineFlash(resultType, currentScore, delta));
            }
            else if (ShouldDelayResultForTimeoutFade(resultType))
            {
                _resultPanel.Hide();
                if (_pendingResultSequence != null)
                {
                    StopCoroutine(_pendingResultSequence);
                }
                _pendingResultSequence = StartCoroutine(ShowResultAfterTimeoutFade(resultType, currentScore, delta));
            }
            else
            {
                ShowResultPanel(resultType, currentScore, delta);
            }
        }
        else
        {
            Debug.LogError("[NetworkGameManager] _resultPanel NullReference.");
        }
        ApplyMatchResult(delta);
    }

    private bool ShouldDelayResultForWinningLine(GameResultType resultType)
    {
        return _flashWinningLineBeforeResult &&
               (resultType == GameResultType.Win || resultType == GameResultType.Lose);
    }

    private bool ShouldDelayResultForTimeoutFade(GameResultType resultType)
    {
        return _fadeScreenBeforeTimeoutResult && resultType == GameResultType.Timeout;
    }

    private IEnumerator ShowResultAfterWinningLineFlash(GameResultType resultType, int currentScore, int delta)
    {
        float waitUntil = Time.time + WinningLineWaitTimeoutSeconds;
        OmokMatchManager match = null;
        OmokStoneDropper dropper = null;

        while (!TryGetWinningLineFlashTargets(out match, out dropper) && Time.time < waitUntil)
        {
            yield return null;
        }

        if (match != null && dropper != null)
        {
            yield return dropper.PlayWinningStoneFlash(
                match.WinningCoordinates,
                match.Winner,
                _winningLineFlashCount,
                _winningLineFlashOnSeconds,
                _winningLineFlashOffSeconds);
        }

        ShowResultPanel(resultType, currentScore, delta);
        _pendingResultSequence = null;
    }

    private IEnumerator ShowResultAfterTimeoutFade(GameResultType resultType, int currentScore, int delta)
    {
        Image overlay = EnsureTimeoutFadeOverlay();
        if (overlay == null)
        {
            ShowResultPanel(resultType, currentScore, delta);
            _pendingResultSequence = null;
            yield break;
        }

        overlay.transform.SetAsFirstSibling();
        overlay.gameObject.SetActive(true);
        SetTimeoutFadeOverlayAlpha(overlay, 0f);

        float duration = Mathf.Max(0f, _timeoutFadeDuration);
        float targetAlpha = Mathf.Clamp01(_timeoutFadeTargetAlpha);

        if (duration <= 0f)
        {
            SetTimeoutFadeOverlayAlpha(overlay, targetAlpha);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetTimeoutFadeOverlayAlpha(overlay, Mathf.SmoothStep(0f, targetAlpha, t));
                yield return null;
            }

            SetTimeoutFadeOverlayAlpha(overlay, targetAlpha);
        }

        ShowResultPanel(resultType, currentScore, delta);
        _pendingResultSequence = null;
    }

    private static bool TryGetWinningLineFlashTargets(out OmokMatchManager match, out OmokStoneDropper dropper)
    {
        match = FindFirstObjectByType<OmokMatchManager>();
        dropper = FindFirstObjectByType<OmokStoneDropper>();

        return match != null &&
               dropper != null &&
               match.Winner != OmokStoneColor.None &&
               match.WinningCoordinates != null &&
               match.WinningCoordinates.Count > 0;
    }

    private void ShowResultPanel(GameResultType resultType, int currentScore, int delta)
    {
        if (_resultPanel == null)
        {
            return;
        }

        _resultPanel.Show(resultType, currentScore, delta);
    }

    private Image EnsureTimeoutFadeOverlay()
    {
        if (_timeoutFadeOverlayImage != null)
        {
            return _timeoutFadeOverlayImage;
        }

        Canvas targetCanvas = _resultPanel != null
            ? _resultPanel.GetComponentInParent<Canvas>(true)
            : null;

        if (targetCanvas == null)
        {
            Debug.LogWarning("[NetworkGameManager] Timeout fade overlay skipped: result panel canvas missing.");
            return null;
        }

        GameObject overlayObject = new("Timeout Result Fade Overlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(targetCanvas.transform, false);
        overlayObject.transform.SetAsFirstSibling();

        RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        _timeoutFadeOverlayImage = overlayObject.GetComponent<Image>();
        _timeoutFadeOverlayImage.raycastTarget = true;
        SetTimeoutFadeOverlayAlpha(_timeoutFadeOverlayImage, 0f);
        overlayObject.SetActive(false);

        return _timeoutFadeOverlayImage;
    }

    private void SetTimeoutFadeOverlayAlpha(Image overlay, float alpha)
    {
        if (overlay == null)
        {
            return;
        }

        Color color = _timeoutFadeColor;
        color.a = Mathf.Clamp01(alpha);
        overlay.color = color;
    }

    private void ApplyMatchResult(int delta)
    {
        if (BackendRank.Instance == null) { Debug.LogError("[NetworkGameManager] BackendRank.Instance is null."); return; }
        if (BackendManager.Instance == null) { Debug.LogError("[NetworkGameManager] BackendManager.Instance is null."); return; }
        if (BackendManager.Instance.MyUserData == null) { Debug.LogError("[NetworkGameManager] BackendManager.MyUserData is null."); return; }


        int next = BackendManager.Instance.MyUserData.score + delta;
        BackendRank.Instance.UpdateMyScore(next,
            onSuccess: () =>
            {
                BackendManager.Instance.MyUserData.score = next;
                Debug.Log($"[NetworkGameManager] 점수 갱신 완료 ({next}점)");
            },
            onFail: err =>
            {
                Debug.LogError($"[NetworkGameManager] UpdateMyScore failed: {err}");
            });
    }

    public void ReturnToLobby()
    {
        if(_returningToLobby) return;
        _returningToLobby = true;

        PhotonNetwork.AutomaticallySyncScene = false;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }
        LoadLobbySceneDirect();
    }
    

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        if(!_returningToLobby) return;
        Debug.Log($"[NetworkGameManager] 방 떠남 (본인: {PhotonNetwork.LocalPlayer.NickName}, actor: {PhotonNetwork.LocalPlayer.ActorNumber})");
        LoadLobbySceneDirect();
    }

    private void LoadLobbySceneDirect()
    {
        if (BackendManager.Instance != null) BackendManager.Instance.LoadLobbyScene();
    }


    // ──────────────────────────────────────────────────────────────
    //  6. 예외 상황 처리 (상대 퇴장, 본인 끊김, 마스터 이전)
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
