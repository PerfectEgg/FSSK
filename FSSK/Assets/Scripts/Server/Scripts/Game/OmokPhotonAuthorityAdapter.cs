using Photon.Pun;
using UnityEngine;

/// <summary>
/// OmokTurnSystem 과 Photon 사이의 다리.
/// 1·2단계: 씬 진입 시 Photon 방 정보로 게임모드(Host/Remote)와 로컬 돌 색을 자동 세팅.
/// 3단계: 마스터에서 돌이 놓이면 RPC 로 리모트들에게 broadcast.
/// 4단계: 리모트의 착수 요청을 RPC 로 마스터에게 송신.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public class OmokPhotonAuthorityAdapter : OmokTurnAuthorityAdapter
{
    [Header("Photon Match Setup")]
    [Tooltip("씬 진입 시 Photon 방 정보로 게임모드/색깔을 자동 설정")]
    [SerializeField] private bool autoConfigureOnEnable = true;

    private PhotonView _photonView;
    private OmokStoneDropper _stoneDropper;
    private OmokMatchManager _matchManager;
    private OmokGrid _grid;
    private OmokStoneDropper _subscribedStoneDropper;
    private OmokMatchManager _subscribedMatchManager;

    protected override void OnEnable()
    {
        base.OnEnable();

        EnsurePhotonView();
        EnsureCachedReferences();
        SubscribeToStoneDropper();

        if (autoConfigureOnEnable)
        {
            TryConfigureMatchFromPhoton();
        }
    }

    protected override void OnDisable()
    {
        UnsubscribeFromStoneDropper();
        base.OnDisable();
    }

    /// <summary>
    /// Photon 방 정보 → OmokTurnSystem 모드/색깔 동기화.
    /// 외부(NetworkGameManager 등)에서 매칭 완료 후 다시 호출할 수도 있음.
    /// </summary>
    public bool TryConfigureMatchFromPhoton()
    {
        if (TurnSystem == null)
        {
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] TurnSystem missing - skip configure.", this);
            return false;
        }

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] Not in room - skip configure.", this);
            return false;
        }

        if (PhotonNetwork.OfflineMode)
        {
            TurnSystem.SetGameMode(OmokTurnGameMode.SingleLocalVsAi);
            TurnSystem.SetLocalPlayerColor(OmokStoneColor.Gold);
            TurnSystem.SetSelectedAiType(SoloPlaySettings.SelectedAiType);
            Debug.Log("[OmokPhotonAuthorityAdapter] Offline solo mode configured.", this);
            return true;
        }

        bool isMaster = PhotonNetwork.IsMasterClient;
        OmokTurnGameMode mode = isMaster
            ? OmokTurnGameMode.MultiplayerHost
            : OmokTurnGameMode.MultiplayerRemote;
        OmokStoneColor localColor = isMaster ? OmokStoneColor.Gold : OmokStoneColor.Silver;

        TurnSystem.SetGameMode(mode);
        TurnSystem.SetLocalPlayerColor(localColor);

        int actorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        Debug.Log($"[OmokPhotonAuthorityAdapter] 매치 세팅 완료 - 모드: {mode}, 내 색: {localColor}, ActorNumber: {actorNumber}, IsMaster: {isMaster}");
        return true;
    }

    // ──────────────────────────────────────────────
    //  3단계: 마스터 → 리모트 (결과 broadcast)
    // ──────────────────────────────────────────────

    private void HandleStonePlacedOnAuthority(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom) return;
        if (!EnsurePhotonView()) return;

        Debug.Log($"[OmokPhotonAuthorityAdapter] 마스터→리모트 착수 결과 broadcast: ({coordinate.x},{coordinate.y}) {stoneColor}");
        _photonView.RPC(nameof(RpcApplyPlacementResult), RpcTarget.Others,
            coordinate.x, coordinate.y, (int)stoneColor);
    }

    [PunRPC]
    private void RpcApplyPlacementResult(int x, int y, int stoneColorInt)
    {
        if (PhotonNetwork.IsMasterClient) return;

        EnsureCachedReferences();

        OmokStoneColor color = (OmokStoneColor)stoneColorInt;
        Vector2Int coordinate = new(x, y);

        Debug.Log($"[OmokPhotonAuthorityAdapter] 리모트가 마스터 결과 수신: ({x},{y}) {color}");

        OmokStonePlacementRequest request = new OmokStonePlacementRequest(
            stoneColor: color,
            targetCoordinate: coordinate,
            releasePosition: ResolveRemoteReleasePosition(coordinate),
            lockToTargetCoordinate: true,
            forceBoardPlacement: true);

        // 1) 시각화 (돌 떨어뜨림 시작)
        if (!TryApplyPlacementVisual(request))
        {
            Debug.LogWarning($"[OmokPhotonAuthorityAdapter] Remote visual placement failed at ({x},{y}) {color}.", this);
        }

        // 2) 보드 상태/턴/승패 즉시 동기화 (시각 착지를 기다리지 않음)
        if (!TryApplyPlacementResult(coordinate, color))
        {
            Debug.LogWarning($"[OmokPhotonAuthorityAdapter] Remote result registration failed at ({x},{y}) {color}.", this);
        }
    }

    private void HandleStoneBlockedOnAuthority(OmokBlockedStoneResult blockedResult)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom) return;
        if (!EnsurePhotonView()) return;

        Debug.Log($"[OmokPhotonAuthorityAdapter] Master blocked placement broadcast: ({blockedResult.TargetCoordinate.x},{blockedResult.TargetCoordinate.y}) {blockedResult.StoneColor}");
        _photonView.RPC(nameof(RpcApplyBlockedPlacementResult), RpcTarget.Others,
            blockedResult.TargetCoordinate.x,
            blockedResult.TargetCoordinate.y,
            (int)blockedResult.StoneColor,
            blockedResult.ConsecutiveSameColorStackCount,
            blockedResult.HasBlockerSnapshot,
            blockedResult.BlockerViewId,
            blockedResult.BlockerLocalPosition,
            blockedResult.BlockerLocalRotation,
            blockedResult.ReleasePosition);
    }

    [PunRPC]
    private void RpcApplyBlockedPlacementResult(
        int x,
        int y,
        int stoneColorInt,
        int consecutiveSameColorStackCount,
        bool hasBlockerSnapshot,
        int blockerViewId,
        Vector3 blockerLocalPosition,
        Quaternion blockerLocalRotation,
        Vector3 releasePosition)
    {
        if (PhotonNetwork.IsMasterClient) return;

        EnsureCachedReferences();

        OmokStoneColor color = (OmokStoneColor)stoneColorInt;
        Vector2Int coordinate = new(x, y);

        Debug.Log($"[OmokPhotonAuthorityAdapter] Remote blocked result received: ({x},{y}) {color}");

        OmokBlockedStoneResult blockedResult = new OmokBlockedStoneResult(
            color,
            null,
            consecutiveSameColorStackCount,
            coordinate,
            hasBlockerSnapshot,
            blockerViewId,
            blockerLocalPosition,
            blockerLocalRotation,
            releasePosition);

        bool visualApplied = _stoneDropper != null &&
                             _stoneDropper.TryApplyBlockedPlacementVisual(blockedResult);
        if (!visualApplied)
        {
            Debug.LogWarning(
                $"[OmokPhotonAuthorityAdapter] Remote blocked visual placement skipped at ({x},{y}) {color}: authoritative blocker snapshot is unavailable.",
                this);
        }

        if (!TryApplyBlockedResult(blockedResult))
        {
            Debug.LogWarning($"[OmokPhotonAuthorityAdapter] Remote blocked result registration failed at ({x},{y}) {color}.", this);
        }
    }

    private void HandleStoneRemovedOnAuthority(OmokStoneRemovalResult removalResult)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom) return;
        if (!EnsurePhotonView()) return;

        Debug.Log($"[OmokPhotonAuthorityAdapter] Master removal broadcast: ({removalResult.Coordinate.x},{removalResult.Coordinate.y}) {removalResult.StoneColor}");
        _photonView.RPC(nameof(RpcApplyRemovalResult), RpcTarget.Others,
            removalResult.Coordinate.x,
            removalResult.Coordinate.y,
            (int)removalResult.StoneColor);
    }

    [PunRPC]
    private void RpcApplyRemovalResult(int x, int y, int stoneColorInt)
    {
        if (PhotonNetwork.IsMasterClient) return;

        EnsureCachedReferences();

        OmokStoneColor color = (OmokStoneColor)stoneColorInt;
        OmokStoneRemovalResult removalTarget = new OmokStoneRemovalResult(new Vector2Int(x, y), color);

        if (!TryApplyRemoval(removalTarget, out _))
        {
            Debug.LogWarning($"[OmokPhotonAuthorityAdapter] Remote removal failed at ({x},{y}) {color}.", this);
        }
    }

    // ──────────────────────────────────────────────
    //  4단계: 리모트 → 마스터 (요청 송신)
    // ──────────────────────────────────────────────

    /// <summary>
    /// base 클래스의 후크 override.
    /// TurnSystem 이 RemoteClient 모드일 때 사용자가 입력하면 호출됨.
    /// </summary>
    protected override void SendPlacementRequestToAuthority(OmokStonePlacementRequest request)
    {
        if (!EnsurePhotonView()) return;

        if (PhotonNetwork.IsMasterClient)
        {
            // 정상 흐름이라면 마스터는 RemoteClient 모드가 아니라서 여기 안 옴
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] Master attempted to submit placement to authority - ignored.", this);
            return;
        }

        Debug.Log($"[OmokPhotonAuthorityAdapter] 리모트→마스터 착수 요청: ({request.TargetCoordinate.x},{request.TargetCoordinate.y}) {request.StoneColor}");
        _photonView.RPC(nameof(RpcSubmitPlacementRequest), RpcTarget.MasterClient,
            request.TargetCoordinate.x,
            request.TargetCoordinate.y,
            (int)request.StoneColor,
            request.ReleasePosition.x,
            request.ReleasePosition.y,
            request.ReleasePosition.z,
            request.LockToTargetCoordinate,
            request.AllowBlockedCoordinateForBlocker,
            request.HasBlockerSnapshot,
            request.BlockerViewId,
            request.BlockerLocalPosition,
            request.BlockerLocalRotation,
            request.BlockerConsumesTurnWhenBlocked,
            request.BlockerCountsForStackWin);
    }

    protected override void SendRandomRemovalRequestToAuthority()
    {
        if (!EnsurePhotonView()) return;

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] Master attempted to request random removal from authority - ignored.", this);
            return;
        }

        _photonView.RPC(nameof(RpcRequestRandomRemoval), RpcTarget.MasterClient);
    }

    protected override void SendRemovalConfirmationToAuthority(OmokStoneRemovalResult removalTarget)
    {
        if (!EnsurePhotonView()) return;

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] Master attempted to confirm removal to authority - ignored.", this);
            return;
        }

        _photonView.RPC(nameof(RpcConfirmRemovalRequest), RpcTarget.MasterClient,
            removalTarget.Coordinate.x,
            removalTarget.Coordinate.y,
            (int)removalTarget.StoneColor);
    }

    [PunRPC]
    private void RpcSubmitPlacementRequest(
        int x,
        int y,
        int stoneColorInt,
        float releaseX,
        float releaseY,
        float releaseZ,
        bool lockToTargetCoordinate,
        bool allowBlockedCoordinateForBlocker,
        bool hasBlockerSnapshot,
        int blockerViewId,
        Vector3 blockerLocalPosition,
        Quaternion blockerLocalRotation,
        bool blockerConsumesTurnWhenBlocked,
        bool blockerCountsForStackWin,
        PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        EnsureCachedReferences();
        if (_matchManager == null)
        {
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] MatchManager missing on master - drop remote request.", this);
            return;
        }

        OmokStoneColor color = (OmokStoneColor)stoneColorInt;
        Vector2Int coordinate = new(x, y);
        Vector3 releasePosition = new(releaseX, releaseY, releaseZ);
        int senderActor = info.Sender != null ? info.Sender.ActorNumber : -1;

        Debug.Log($"[OmokPhotonAuthorityAdapter] 마스터가 리모트 요청 수신: ({x},{y}) {color} from actor {senderActor}");

        OmokStonePlacementRequest request = new(
            color,
            coordinate,
            releasePosition,
            lockToTargetCoordinate,
            allowBlockedCoordinateForBlocker,
            hasBlockerSnapshot,
            blockerViewId,
            blockerLocalPosition,
            blockerLocalRotation,
            blockerConsumesTurnWhenBlocked,
            blockerCountsForStackWin);

        // Preserve the remote-built request so blocker placement flags and release height
        // survive the authority hop.
        bool accepted = _matchManager.TryProcessPlacementRequest(request);
        if (!accepted)
        {
            Debug.LogWarning($"[OmokPhotonAuthorityAdapter] Master rejected remote placement ({x},{y}) {color}.", this);
        }
    }

    [PunRPC]
    private void RpcRequestRandomRemoval(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        EnsureCachedReferences();
        if (_matchManager == null)
        {
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] MatchManager missing on master - drop random removal request.", this);
            return;
        }

        if (!_matchManager.TryRemoveRandomStone(out OmokStoneRemovalResult removalResult))
        {
            int senderActor = info.Sender != null ? info.Sender.ActorNumber : -1;
            Debug.LogWarning($"[OmokPhotonAuthorityAdapter] Master rejected random removal request from actor {senderActor}.", this);
            return;
        }

        Debug.Log($"[OmokPhotonAuthorityAdapter] Master accepted random removal: ({removalResult.Coordinate.x},{removalResult.Coordinate.y}) {removalResult.StoneColor}");
    }

    [PunRPC]
    private void RpcConfirmRemovalRequest(int x, int y, int stoneColorInt, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        EnsureCachedReferences();
        if (_matchManager == null)
        {
            Debug.LogWarning("[OmokPhotonAuthorityAdapter] MatchManager missing on master - drop removal confirmation.", this);
            return;
        }

        OmokStoneColor color = (OmokStoneColor)stoneColorInt;
        OmokStoneRemovalResult removalTarget = new OmokStoneRemovalResult(new Vector2Int(x, y), color);
        if (!_matchManager.TryConfirmRemoveStone(removalTarget, out _))
        {
            int senderActor = info.Sender != null ? info.Sender.ActorNumber : -1;
            Debug.LogWarning($"[OmokPhotonAuthorityAdapter] Master rejected removal ({x},{y}) {color} from actor {senderActor}.", this);
        }
    }


    // ──────────────────────────────────────────────
    //  내부 유틸
    // ──────────────────────────────────────────────

    private bool EnsurePhotonView()
    {
        if (_photonView == null) _photonView = GetComponent<PhotonView>();
        if (_photonView == null)
        {
            Debug.LogError("[OmokPhotonAuthorityAdapter] PhotonView component missing on this GameObject.", this);
            return false;
        }
        return true;
    }

    private void EnsureCachedReferences()
    {
        if (TurnSystem == null) ResolveReferences();

        if (_stoneDropper == null && TurnSystem != null)
        {
            _stoneDropper = TurnSystem.GetComponent<OmokStoneDropper>();
            if (_stoneDropper == null) _stoneDropper = TurnSystem.GetComponentInChildren<OmokStoneDropper>(true);
        }
        if (_stoneDropper == null) _stoneDropper = FindFirstObjectByType<OmokStoneDropper>();

        if (_matchManager == null && TurnSystem != null)
        {
            _matchManager = TurnSystem.GetComponent<OmokMatchManager>();
            if (_matchManager == null) _matchManager = TurnSystem.GetComponentInChildren<OmokMatchManager>(true);
        }
        if (_matchManager == null) _matchManager = FindFirstObjectByType<OmokMatchManager>();

        if (_grid == null && TurnSystem != null)
        {
            _grid = TurnSystem.GetComponent<OmokGrid>();
            if (_grid == null) _grid = TurnSystem.GetComponentInChildren<OmokGrid>(true);
        }
        if (_grid == null) _grid = FindFirstObjectByType<OmokGrid>();
    }

    private void SubscribeToStoneDropper()
    {
        if (_subscribedStoneDropper == _stoneDropper) return;

        UnsubscribeFromStoneDropper();
        if (_stoneDropper == null) return;

        _stoneDropper.OnStonePlaced += HandleStonePlacedOnAuthority;
        _stoneDropper.OnStoneBlocked += HandleStoneBlockedOnAuthority;
        _subscribedStoneDropper = _stoneDropper;

        if (_matchManager != null)
        {
            _matchManager.OnStoneRemoved += HandleStoneRemovedOnAuthority;
            _matchManager.OnMatchEnded += HandleMatchEndedOnAuthority;
            _subscribedMatchManager = _matchManager;
        }
    }

    private void UnsubscribeFromStoneDropper()
    {
        if (_subscribedStoneDropper != null)
        {
            _subscribedStoneDropper.OnStonePlaced -= HandleStonePlacedOnAuthority;
            _subscribedStoneDropper.OnStoneBlocked -= HandleStoneBlockedOnAuthority;
            _subscribedStoneDropper = null;
        }

        if (_subscribedMatchManager != null)
        {
            _subscribedMatchManager.OnStoneRemoved -= HandleStoneRemovedOnAuthority;
            _subscribedMatchManager.OnMatchEnded -= HandleMatchEndedOnAuthority;
            _subscribedMatchManager = null;
        }
    }

    // ──────────────────────────────────────────────
    //  매치 종료 → NetworkGameManager 권한 결정 (마스터 전담)
    //  마스터=Gold, 리모트=Silver (TryConfigureMatchFromPhoton 의 색 배정과 일치)
    // ──────────────────────────────────────────────

    private void HandleMatchEndedOnAuthority(OmokStoneColor winnerColor)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (NetworkGameManager.Instance == null)
        {
            Debug.LogError("[OmokPhotonAuthorityAdapter] NetworkGameManager.Instance is null on match end.");
            return;
        }

        int masterActor = PhotonNetwork.MasterClient != null ? PhotonNetwork.MasterClient.ActorNumber : -1;
        int winnerActor;
        if (winnerColor == OmokStoneColor.None)
        {
            winnerActor = -1;
        }
        else if (winnerColor == OmokStoneColor.Gold)
        {
            winnerActor = masterActor;
        }
        else
        {
            winnerActor = ResolveOpponentActorNumber(masterActor);
        }

        Debug.Log($"[OmokPhotonAuthorityAdapter] 매치 종료 → NetworkGameManager.EndGame 호출 (winnerColor: {winnerColor}, winnerActor: {winnerActor})");
        NetworkGameManager.Instance.EndGame(winnerActor);
    }

    private int ResolveOpponentActorNumber(int masterActor)
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p == null) continue;
            if (p.ActorNumber != masterActor) return p.ActorNumber;
        }
        return -1;
    }

    private Vector3 ResolveRemoteReleasePosition(Vector2Int coordinate)
    {
        if (_grid != null && _grid.IsReady)
        {
            return _grid.GetWorldPosition(coordinate) + Vector3.up * 2f;
        }
        return Vector3.zero;
    }
}
