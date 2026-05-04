using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OmokTurnActorType
{
    Unassigned,
    LocalPlayer,
    RemotePlayer,
    Ai,
    PlayerOne,
    PlayerTwo
}

public enum OmokTurnAuthorityMode
{
    LocalOnly,
    HostOrServer,
    RemoteClient
}

public enum OmokTurnGameMode
{
    SingleLocalVsAi,
    MultiplayerHost,
    MultiplayerRemote
}

public enum OmokTurnTimeoutAction
{
    None,
    AutoDropRandomLegalMove
}

[Serializable]
public class OmokTurnSeatStatus
{
    [SerializeField] private OmokStoneColor stoneColor = OmokStoneColor.None;
    [SerializeField] private Color displayColor = Color.white;
    [SerializeField] private OmokTurnActorType actorType = OmokTurnActorType.Unassigned;
    [SerializeField] private string actorLabel = "Unassigned";
    [SerializeField] private bool isCurrentTurn;
    [SerializeField] private bool canAct;
    [SerializeField, Min(0)] private int placedStoneCount;

    public OmokStoneColor StoneColor => stoneColor;
    public Color DisplayColor => displayColor;
    public OmokTurnActorType ActorType => actorType;
    public string ActorLabel => actorLabel;
    public bool IsCurrentTurn => isCurrentTurn;
    public bool CanAct => canAct;
    public int PlacedStoneCount => placedStoneCount;

    public void SetStaticInfo(OmokStoneColor nextStoneColor, Color nextDisplayColor)
    {
        stoneColor = nextStoneColor;
        displayColor = nextDisplayColor;
    }

    public void SetActor(OmokTurnActorType nextActorType, string nextActorLabel)
    {
        actorType = nextActorType;
        actorLabel = string.IsNullOrWhiteSpace(nextActorLabel) ? nextActorType.ToString() : nextActorLabel;
    }

    public void SetRuntimeState(bool nextIsCurrentTurn, bool nextCanAct, int nextPlacedStoneCount)
    {
        isCurrentTurn = nextIsCurrentTurn;
        canAct = nextCanAct;
        placedStoneCount = Mathf.Max(0, nextPlacedStoneCount);
    }
}

[DisallowMultipleComponent]
[ExecuteAlways]
public class OmokTurnSystem : MonoBehaviour
{
    [SerializeField, HideInInspector] private OmokMatchManager matchManager;
    [SerializeField, HideInInspector] private OmokStoneDropper stoneDropper;
    [SerializeField, HideInInspector] private OmokLocalPlayerContext localPlayerContext;

    [Header("Turn Setup")]
    [SerializeField] private OmokTurnGameMode gameMode = OmokTurnGameMode.SingleLocalVsAi;
    [SerializeField] private bool useAi = true;
    [SerializeField] private OmokAiType selectedAiType = OmokAiType.Easy;
    [SerializeField] private OmokStoneColor localPlayerColor = OmokStoneColor.Silver;
    [SerializeField] private bool allowManualInput = true;
    [SerializeField] private bool restrictManualDragToCurrentTurn = true;
    [SerializeField, Min(0f)] private float aiTurnDelay = 0.35f;

    [Header("Turn Timer")]
    [SerializeField] private bool useTurnTimer = true;
    [SerializeField, Min(0.1f)] private float turnDurationSeconds = 10f;
    [SerializeField, Min(0.1f)] private float minimumTurnDurationSeconds = 3f;
    [SerializeField] private OmokTurnTimeoutAction timeoutAction = OmokTurnTimeoutAction.None;

    [Header("Seat Map")]
    [SerializeField] private OmokTurnSeatStatus goldSeat = new();
    [SerializeField] private OmokTurnSeatStatus silverSeat = new();

    [Header("Rules")]
    [SerializeField] private OmokStoneColor openingTurn = OmokStoneColor.Gold;
    [SerializeField] private bool allowOverline = true;
    [SerializeField] private bool blockedAttemptConsumesTurn = true;
    [SerializeField] private bool allowBlockerVerticalWin = true;
    [SerializeField, Min(1)] private int blockerVerticalWinLength = 5;

    [Header("Authority")]
    [SerializeField] private OmokTurnAuthorityMode authorityMode = OmokTurnAuthorityMode.LocalOnly;
    [SerializeField] private bool processPlacementRequestsLocally = true;
    [SerializeField] private bool applyStoneResultsLocally = true;

    [Header("Live Turn State")]
    [SerializeField] private OmokStoneColor currentTurn = OmokStoneColor.None;
    [SerializeField] private string currentTurnActor = "Unassigned";
    [SerializeField] private bool isMatchEnded;
    [SerializeField] private OmokStoneColor winner = OmokStoneColor.None;
    [SerializeField] private OmokStoneColor nextRandomRemovalColor = OmokStoneColor.None;

    [Header("Live Turn Timer")]
    [SerializeField] private OmokStoneColor timerTurn = OmokStoneColor.None;
    [SerializeField, Min(0f)] private float turnElapsedSeconds;
    [SerializeField, Min(0f)] private float turnRemainingSeconds = 10f;
    [SerializeField, Min(0)] private int turnRemainingWholeSeconds = 10;
    [SerializeField, Range(0f, 1f)] private float turnTimerProgress01;
    [SerializeField] private bool turnTimerExpired;

    [Header("Board Snapshot")]
    [SerializeField, Min(0)] private int boardSize;
    [SerializeField, Min(0)] private int totalPlacedStones;
    [SerializeField, Min(0)] private int emptyCells;

    [Header("Display Colors")]
    [SerializeField] private Color goldDisplayColor = new(1f, 0.72f, 0.18f, 1f);
    [SerializeField] private Color silverDisplayColor = new(0.82f, 0.86f, 0.9f, 1f);

    private OmokMatchManager _subscribedMatchManager;
    private OmokLocalPlayerContext _subscribedLocalPlayerContext;
    private OmokStoneDropper _subscribedStoneDropper;
    private Coroutine _pendingAiTurn;
    private IOmokAi _selectedAi;
    private bool _isApplyingSetup;

    public OmokTurnGameMode GameMode => gameMode;
    public OmokTurnAuthorityMode AuthorityMode => authorityMode;
    public bool IsAuthoritativePeer => authorityMode != OmokTurnAuthorityMode.RemoteClient;
    public bool ShouldSubmitRequestsToAuthority => authorityMode == OmokTurnAuthorityMode.RemoteClient;
    public bool CanMutateMatchStateLocally => IsAuthoritativePeer;
    public bool CanRunAiLocally => useAi && IsAuthoritativePeer;
    public bool UseAi => useAi;
    public OmokAiType SelectedAiType => selectedAiType;
    public OmokStoneColor LocalPlayerColor => GetLocalPlayerColor();
    public OmokStoneColor AiStoneColor => GetAiStoneColor();
    public OmokStoneColor CurrentTurn => currentTurn;
    public string CurrentTurnActor => currentTurnActor;
    public OmokTurnSeatStatus GoldSeat => goldSeat;
    public OmokTurnSeatStatus SilverSeat => silverSeat;
    public bool IsMatchEnded => isMatchEnded;
    public OmokStoneColor Winner => winner;
    public bool UseTurnTimer => useTurnTimer;
    public float TurnDurationSeconds => turnDurationSeconds;
    public float MinimumTurnDurationSeconds => minimumTurnDurationSeconds;
    public OmokTurnTimeoutAction TimeoutAction => timeoutAction;
    public OmokStoneColor TimerTurn => timerTurn;
    public float TurnElapsedSeconds => turnElapsedSeconds;
    public float TurnRemainingSeconds => turnRemainingSeconds;
    public int TurnRemainingWholeSeconds => turnRemainingWholeSeconds;
    public float TurnTimerProgress01 => turnTimerProgress01;
    public bool TurnTimerExpired => turnTimerExpired;

    public event Action<OmokStonePlacementRequest> OnPlacementRequestSubmittedToAuthority;
    public event Action OnRandomRemovalRequestedFromAuthority;
    public event Action<OmokStoneRemovalResult> OnRemovalConfirmationSubmittedToAuthority;
    public event Action<OmokStoneColor> OnTurnTimerExpired;

    private void Reset()
    {
        ResolveReferences();
        ApplyGameModePreset();
        _selectedAi = CreateAi(selectedAiType);
        ApplySetupToRuntime();
        RefreshInspectorState();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyGameModePreset();
        _selectedAi = CreateAi(selectedAiType);
        ApplySetupToRuntime();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        ApplyGameModePreset();
        ApplySetupToRuntime();
        RefreshInspectorState();
        TryQueueAiTurn();
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelPendingAiTurn();
    }

    private void OnValidate()
    {
        ApplyGameModePreset();

        if (localPlayerColor != OmokStoneColor.Gold && localPlayerColor != OmokStoneColor.Silver)
        {
            localPlayerColor = useAi ? OmokStoneColor.Silver : OmokStoneColor.None;
        }

        blockerVerticalWinLength = Mathf.Max(1, blockerVerticalWinLength);
        aiTurnDelay = Mathf.Max(0f, aiTurnDelay);
        minimumTurnDurationSeconds = Mathf.Max(0.1f, minimumTurnDurationSeconds);
        turnDurationSeconds = Mathf.Max(minimumTurnDurationSeconds, turnDurationSeconds);
        _selectedAi = CreateAi(selectedAiType);

        ResolveReferences();
        ApplySetupToRuntime();
        RefreshInspectorState();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            UpdateTurnTimer(Time.deltaTime);
        }
        else
        {
            RefreshInspectorState();
        }
    }

    public void SetGameMode(OmokTurnGameMode nextGameMode)
    {
        if (gameMode == nextGameMode)
        {
            return;
        }

        gameMode = nextGameMode;
        ApplyGameModePreset();
        ApplySetupToRuntime();
        RefreshInspectorState();
        TryQueueAiTurn();
    }

    public void ApplyGameModePreset()
    {
        switch (gameMode)
        {
            case OmokTurnGameMode.SingleLocalVsAi:
                useAi = true;
                allowManualInput = true;
                restrictManualDragToCurrentTurn = true;
                authorityMode = OmokTurnAuthorityMode.LocalOnly;
                break;

            case OmokTurnGameMode.MultiplayerHost:
                useAi = false;
                allowManualInput = true;
                restrictManualDragToCurrentTurn = true;
                authorityMode = OmokTurnAuthorityMode.HostOrServer;
                break;

            case OmokTurnGameMode.MultiplayerRemote:
                useAi = false;
                allowManualInput = true;
                restrictManualDragToCurrentTurn = true;
                authorityMode = OmokTurnAuthorityMode.RemoteClient;
                break;
        }
    }

    public void SetAiEnabled(bool isEnabled)
    {
        if (useAi == isEnabled)
        {
            return;
        }

        useAi = isEnabled;
        ApplySetupToRuntime();
        RefreshInspectorState();
        TryQueueAiTurn();
    }

    public void SetSelectedAiType(OmokAiType aiType)
    {
        selectedAiType = aiType;
        _selectedAi = CreateAi(selectedAiType);
        RefreshInspectorState();
        TryQueueAiTurn();
    }

    public void SetLocalPlayerColor(OmokStoneColor stoneColor)
    {
        localPlayerColor = stoneColor;
        ApplySetupToRuntime();
        RefreshInspectorState();
        TryQueueAiTurn();
    }

    public void SetTurnTimerEnabled(bool isEnabled)
    {
        if (useTurnTimer == isEnabled)
        {
            return;
        }

        useTurnTimer = isEnabled;
        ResetTurnTimer();
    }

    public void SetTurnDurationSeconds(float seconds)
    {
        float normalizedSeconds = Mathf.Max(0.1f, seconds);
        if (Mathf.Approximately(turnDurationSeconds, normalizedSeconds))
        {
            return;
        }

        turnDurationSeconds = normalizedSeconds;
        RefreshTurnTimerSnapshot();
    }

    public void SetMinimumTurnDurationSeconds(float seconds)
    {
        minimumTurnDurationSeconds = Mathf.Max(0.1f, seconds);
        if (turnDurationSeconds < minimumTurnDurationSeconds)
        {
            SetTurnDurationSeconds(minimumTurnDurationSeconds);
            return;
        }

        RefreshTurnTimerSnapshot();
    }

    public void AddTurnDurationSeconds(float secondsDelta)
    {
        SetTurnDurationSeconds(Mathf.Max(minimumTurnDurationSeconds, turnDurationSeconds + secondsDelta));
    }

    public void ReduceTurnDurationSeconds(float seconds)
    {
        AddTurnDurationSeconds(-Mathf.Max(0f, seconds));
    }

    public void ScaleTurnDuration(float multiplier)
    {
        SetTurnDurationSeconds(Mathf.Max(minimumTurnDurationSeconds, turnDurationSeconds * Mathf.Max(0f, multiplier)));
    }

    public void SetTurnDurationForWaveStage(int waveStage, float baseSeconds, float reductionPerStage, float minimumSeconds)
    {
        SetMinimumTurnDurationSeconds(minimumSeconds);
        float nextDuration = Mathf.Max(minimumTurnDurationSeconds, baseSeconds - (Mathf.Max(0, waveStage) * Mathf.Max(0f, reductionPerStage)));
        SetTurnDurationSeconds(nextDuration);
    }

    public void SetTimeoutAction(OmokTurnTimeoutAction nextTimeoutAction)
    {
        timeoutAction = nextTimeoutAction;
    }

    public void SetTimeoutAutoDropEnabled(bool isEnabled)
    {
        timeoutAction = isEnabled ? OmokTurnTimeoutAction.AutoDropRandomLegalMove : OmokTurnTimeoutAction.None;
    }

    [ContextMenu("Reset Turn Timer")]
    public void ResetTurnTimer()
    {
        RefreshMatchSnapshot();
        ResetTurnTimerState(currentTurn);
    }

    [ContextMenu("Apply Turn Setup")]
    public void ApplySetupToRuntime()
    {
        if (_isApplyingSetup)
        {
            return;
        }

        _isApplyingSetup = true;
        ResolveReferences();

        try
        {
            if (matchManager != null)
            {
                processPlacementRequestsLocally = IsAuthoritativePeer;
                applyStoneResultsLocally = IsAuthoritativePeer;
                matchManager.ConfigureRules(
                    openingTurn,
                    allowOverline,
                    blockedAttemptConsumesTurn,
                    allowBlockerVerticalWin,
                    blockerVerticalWinLength);
                matchManager.SetAuthorityMode(processPlacementRequestsLocally, applyStoneResultsLocally);
            }

            OmokStoneColor configuredLocalColor = GetConfiguredLocalPlayerColor();

            if (localPlayerContext != null)
            {
                localPlayerContext.ConfigureLocalPlayer(configuredLocalColor, allowManualInput);
            }

            if (stoneDropper != null)
            {
                stoneDropper.SetRestrictManualDragToCurrentTurn(restrictManualDragToCurrentTurn);

                if (configuredLocalColor == OmokStoneColor.None)
                {
                    stoneDropper.ClearLocalManualStoneColor();
                }
                else
                {
                    stoneDropper.SetLocalManualStoneColor(configuredLocalColor);
                }
            }

            RefreshManualPlacementState();
        }
        finally
        {
            _isApplyingSetup = false;
        }
    }

    [ContextMenu("Refresh Turn Inspector State")]
    public void RefreshInspectorState()
    {
        goldSeat.SetStaticInfo(OmokStoneColor.Gold, goldDisplayColor);
        silverSeat.SetStaticInfo(OmokStoneColor.Silver, silverDisplayColor);

        ResolveSeatActor(OmokStoneColor.Gold, out OmokTurnActorType goldActorType, out string goldActorLabel);
        ResolveSeatActor(OmokStoneColor.Silver, out OmokTurnActorType silverActorType, out string silverActorLabel);
        goldSeat.SetActor(goldActorType, goldActorLabel);
        silverSeat.SetActor(silverActorType, silverActorLabel);

        RefreshMatchSnapshot();
        SyncTurnTimerWithCurrentTurn(false);
    }

    public string GetActorLabel(OmokStoneColor stoneColor)
    {
        return stoneColor switch
        {
            OmokStoneColor.Gold => goldSeat.ActorLabel,
            OmokStoneColor.Silver => silverSeat.ActorLabel,
            _ => "Unassigned"
        };
    }

    public bool CanTakeTurn(OmokStoneColor stoneColor)
    {
        return matchManager != null && matchManager.CanTakeTurn(stoneColor);
    }

    public bool TryRemoveRandomStone(out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;
        if (!CanMutateMatchStateLocally || matchManager == null)
        {
            OnRandomRemovalRequestedFromAuthority?.Invoke();
            return false;
        }

        return matchManager.TryRemoveRandomStone(out removalResult);
    }

    public bool TrySelectNextRemovalTarget(out OmokStoneRemovalResult removalTarget)
    {
        removalTarget = default;
        if (!CanMutateMatchStateLocally || matchManager == null)
        {
            OnRandomRemovalRequestedFromAuthority?.Invoke();
            return false;
        }

        return matchManager.TrySelectNextRemovalTarget(out removalTarget);
    }

    public bool TryConfirmRemoveStone(OmokStoneRemovalResult removalTarget)
    {
        return TryConfirmRemoveStone(removalTarget, out _);
    }

    public bool TryConfirmRemoveStone(OmokStoneRemovalResult removalTarget, out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;
        if (!CanMutateMatchStateLocally || matchManager == null)
        {
            OnRemovalConfirmationSubmittedToAuthority?.Invoke(removalTarget);
            return false;
        }

        return matchManager.TryConfirmRemoveStone(removalTarget, out removalResult);
    }

    public bool TryApplyAuthoritativePlacementResult(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        return matchManager != null && matchManager.TryApplyPlacementResult(coordinate, stoneColor);
    }

    public bool TryApplyAuthoritativeBlockedResult(OmokBlockedStoneResult blockedResult)
    {
        return matchManager != null && matchManager.TryApplyBlockedResult(blockedResult);
    }

    public bool TryExecuteAuthoritativePlacementVisual(OmokStonePlacementRequest request)
    {
        return stoneDropper != null && stoneDropper.TryExecutePlacement(request);
    }

    public bool TryApplyAuthoritativeRemoval(OmokStoneRemovalResult removalTarget, out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;
        return matchManager != null && matchManager.TryConfirmRemoveStone(removalTarget, out removalResult);
    }

    private void RefreshManualPlacementState()
    {
        if (stoneDropper == null)
        {
            return;
        }

        if (matchManager == null)
        {
            stoneDropper.SetManualPlacementState(true, true);
            return;
        }

        if (!useAi && GetLocalPlayerColor() == OmokStoneColor.None && localPlayerContext == null)
        {
            stoneDropper.SetManualPlacementState(true, true);
            return;
        }

        OmokManualPlacementState placementState = matchManager.GetManualPlacementState(GetLocalPlayerColor(), allowManualInput);
        stoneDropper.SetManualPlacementState(placementState.AllowGold, placementState.AllowSilver);
    }

    private void RefreshMatchSnapshot()
    {
        currentTurn = matchManager != null ? matchManager.CurrentTurn : OmokStoneColor.None;
        currentTurnActor = GetActorLabel(currentTurn);
        isMatchEnded = matchManager != null && matchManager.IsMatchEnded;
        winner = matchManager != null ? matchManager.Winner : OmokStoneColor.None;
        nextRandomRemovalColor = matchManager != null ? matchManager.NextRandomRemovalColor : OmokStoneColor.None;

        CountBoardStones(out int goldCount, out int silverCount);
        boardSize = matchManager != null ? matchManager.BoardSize : 0;
        totalPlacedStones = goldCount + silverCount;
        int cellCount = boardSize * boardSize;
        emptyCells = Mathf.Max(0, cellCount - totalPlacedStones);

        goldSeat.SetRuntimeState(currentTurn == OmokStoneColor.Gold, CanTakeTurn(OmokStoneColor.Gold), goldCount);
        silverSeat.SetRuntimeState(currentTurn == OmokStoneColor.Silver, CanTakeTurn(OmokStoneColor.Silver), silverCount);
    }

    private void UpdateTurnTimer(float deltaTime)
    {
        SyncTurnTimerWithCurrentTurn(false);

        if (!useTurnTimer ||
            matchManager == null ||
            isMatchEnded ||
            currentTurn == OmokStoneColor.None)
        {
            return;
        }

        if (turnTimerExpired)
        {
            return;
        }

        turnElapsedSeconds = Mathf.Min(turnDurationSeconds, turnElapsedSeconds + Mathf.Max(0f, deltaTime));
        RefreshTurnTimerSnapshot();

        if (turnRemainingSeconds > 0f)
        {
            return;
        }

        turnTimerExpired = true;
        OmokStoneColor timedOutTurn = timerTurn;
        OnTurnTimerExpired?.Invoke(timedOutTurn);
        HandleTurnTimerExpired(timedOutTurn);
    }

    private void SyncTurnTimerWithCurrentTurn(bool forceReset)
    {
        if (forceReset || timerTurn != currentTurn)
        {
            ResetTurnTimerState(currentTurn);
        }
        else
        {
            RefreshTurnTimerSnapshot();
        }
    }

    private void ResetTurnTimerState(OmokStoneColor nextTimerTurn)
    {
        timerTurn = nextTimerTurn;
        turnElapsedSeconds = 0f;
        turnTimerExpired = false;
        RefreshTurnTimerSnapshot();
    }

    private void RefreshTurnTimerSnapshot()
    {
        minimumTurnDurationSeconds = Mathf.Max(0.1f, minimumTurnDurationSeconds);
        turnDurationSeconds = Mathf.Max(minimumTurnDurationSeconds, turnDurationSeconds);

        if (!useTurnTimer ||
            matchManager == null ||
            isMatchEnded ||
            timerTurn == OmokStoneColor.None)
        {
            turnElapsedSeconds = 0f;
            turnRemainingSeconds = useTurnTimer ? turnDurationSeconds : 0f;
            turnRemainingWholeSeconds = Mathf.CeilToInt(turnRemainingSeconds);
            turnTimerProgress01 = 0f;
            turnTimerExpired = false;
            return;
        }

        turnElapsedSeconds = Mathf.Clamp(turnElapsedSeconds, 0f, turnDurationSeconds);
        turnRemainingSeconds = Mathf.Max(0f, turnDurationSeconds - turnElapsedSeconds);
        turnRemainingWholeSeconds = Mathf.CeilToInt(turnRemainingSeconds);
        turnTimerProgress01 = Mathf.Clamp01(turnElapsedSeconds / turnDurationSeconds);
    }

    private void HandleTurnTimerExpired(OmokStoneColor timedOutTurn)
    {
        if (timeoutAction != OmokTurnTimeoutAction.AutoDropRandomLegalMove)
        {
            return;
        }

        TryAutoDropTimedOutTurn(timedOutTurn);
    }

    private bool TryAutoDropTimedOutTurn(OmokStoneColor timedOutTurn)
    {
        if (!CanMutateMatchStateLocally ||
            timedOutTurn == OmokStoneColor.None ||
            matchManager == null ||
            stoneDropper == null ||
            !matchManager.CanTakeTurn(timedOutTurn) ||
            !TryChooseTimeoutMove(timedOutTurn, out Vector2Int move))
        {
            return false;
        }

        return stoneDropper.TryRequestPlacement(timedOutTurn, move);
    }

    private bool TryChooseTimeoutMove(OmokStoneColor stoneColor, out Vector2Int move)
    {
        move = default;

        OmokStoneColor[,] snapshot = matchManager != null ? matchManager.GetBoardSnapshot() : null;
        if (snapshot == null)
        {
            return false;
        }

        List<Vector2Int> candidates = OmokAiLogic.CollectCandidateMoves(snapshot);
        return TryChooseRandomAcceptedMove(candidates, stoneColor, out move) ||
               TryChooseRandomAcceptedMove(CollectAllEmptyMoves(snapshot), stoneColor, out move);
    }

    private bool TryChooseRandomAcceptedMove(List<Vector2Int> candidates, OmokStoneColor stoneColor, out Vector2Int move)
    {
        move = default;
        if (candidates == null)
        {
            return false;
        }

        while (candidates.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            Vector2Int candidate = candidates[index];
            candidates.RemoveAt(index);

            if (matchManager != null && matchManager.CanAcceptPlacementRequest(candidate, stoneColor))
            {
                move = candidate;
                return true;
            }
        }

        return false;
    }

    private static List<Vector2Int> CollectAllEmptyMoves(OmokStoneColor[,] snapshot)
    {
        List<Vector2Int> moves = new();
        if (snapshot == null)
        {
            return moves;
        }

        for (int y = 0; y < snapshot.GetLength(1); y++)
        {
            for (int x = 0; x < snapshot.GetLength(0); x++)
            {
                if (snapshot[x, y] == OmokStoneColor.None)
                {
                    moves.Add(new Vector2Int(x, y));
                }
            }
        }

        return moves;
    }

    private void CountBoardStones(out int goldCount, out int silverCount)
    {
        goldCount = 0;
        silverCount = 0;

        OmokStoneColor[,] snapshot = matchManager != null ? matchManager.GetBoardSnapshot() : null;
        if (snapshot == null)
        {
            return;
        }

        for (int y = 0; y < snapshot.GetLength(1); y++)
        {
            for (int x = 0; x < snapshot.GetLength(0); x++)
            {
                switch (snapshot[x, y])
                {
                    case OmokStoneColor.Gold:
                        goldCount++;
                        break;
                    case OmokStoneColor.Silver:
                        silverCount++;
                        break;
                }
            }
        }
    }

    private void ResolveSeatActor(OmokStoneColor stoneColor, out OmokTurnActorType actorType, out string actorLabel)
    {
        if (stoneColor == OmokStoneColor.None)
        {
            actorType = OmokTurnActorType.Unassigned;
            actorLabel = "Unassigned";
            return;
        }

        if (useAi && GetAiStoneColor() == stoneColor)
        {
            actorType = OmokTurnActorType.Ai;
            actorLabel = $"AI ({selectedAiType})";
            return;
        }

        if (GetLocalPlayerColor() == stoneColor)
        {
            actorType = OmokTurnActorType.LocalPlayer;
            actorLabel = useAi ? "Player" : "Local Player";
            return;
        }

        if (localPlayerContext != null && localPlayerContext.HasLocalSeat)
        {
            actorType = OmokTurnActorType.RemotePlayer;
            actorLabel = "Remote Player";
            return;
        }

        actorType = stoneColor == OmokStoneColor.Gold ? OmokTurnActorType.PlayerOne : OmokTurnActorType.PlayerTwo;
        actorLabel = stoneColor == OmokStoneColor.Gold ? "Player 1" : "Player 2";
    }

    private OmokStoneColor GetLocalPlayerColor()
    {
        if (localPlayerContext != null && localPlayerContext.HasLocalSeat)
        {
            return localPlayerContext.LocalStoneColor;
        }

        return GetConfiguredLocalPlayerColor();
    }

    private OmokStoneColor GetConfiguredLocalPlayerColor()
    {
        return localPlayerColor;
    }

    private OmokStoneColor GetAiStoneColor()
    {
        if (!useAi)
        {
            return OmokStoneColor.None;
        }

        OmokStoneColor playerColor = GetLocalPlayerColor();
        if (playerColor == OmokStoneColor.None)
        {
            playerColor = OmokStoneColor.Gold;
        }

        return OmokMatchFlow.GetOppositeColor(playerColor);
    }

    private void TryQueueAiTurn()
    {
        CancelPendingAiTurn();

        if (!Application.isPlaying || !isActiveAndEnabled || !CanAiMove())
        {
            return;
        }

        _pendingAiTurn = StartCoroutine(RunAiTurn());
    }

    private IEnumerator RunAiTurn()
    {
        yield return null;

        if (aiTurnDelay > 0f)
        {
            yield return new WaitForSeconds(aiTurnDelay);
        }

        _pendingAiTurn = null;

        if (!CanAiMove() || _selectedAi == null)
        {
            yield break;
        }

        OmokStoneColor aiStoneColor = GetAiStoneColor();
        OmokStoneColor[,] snapshot = matchManager.GetBoardSnapshot();
        if (!_selectedAi.TryChooseMove(snapshot, aiStoneColor, out Vector2Int move))
        {
            yield break;
        }

        if (!stoneDropper.TryRequestPlacement(aiStoneColor, move))
        {
            TryQueueAiTurn();
        }
    }

    private bool CanAiMove()
    {
        return CanRunAiLocally &&
               _selectedAi != null &&
               matchManager != null &&
               stoneDropper != null &&
               matchManager.CanTakeTurn(GetAiStoneColor());
    }

    private void CancelPendingAiTurn()
    {
        if (_pendingAiTurn == null)
        {
            return;
        }

        StopCoroutine(_pendingAiTurn);
        _pendingAiTurn = null;
    }

    private void ResolveReferences()
    {
        if (matchManager == null)
        {
            matchManager = GetComponent<OmokMatchManager>();
        }

        if (matchManager == null)
        {
            matchManager = GetComponentInChildren<OmokMatchManager>(true);
        }

        if (matchManager == null)
        {
            matchManager = GetComponentInParent<OmokMatchManager>();
        }

        if (matchManager == null)
        {
            matchManager = FindFirstObjectByType<OmokMatchManager>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponent<OmokStoneDropper>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponentInChildren<OmokStoneDropper>(true);
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponentInParent<OmokStoneDropper>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = FindFirstObjectByType<OmokStoneDropper>();
        }

        if (localPlayerContext == null)
        {
            localPlayerContext = GetComponent<OmokLocalPlayerContext>();
        }

        if (localPlayerContext == null)
        {
            localPlayerContext = GetComponentInChildren<OmokLocalPlayerContext>(true);
        }

        if (localPlayerContext == null)
        {
            localPlayerContext = GetComponentInParent<OmokLocalPlayerContext>();
        }

        if (localPlayerContext == null)
        {
            localPlayerContext = FindFirstObjectByType<OmokLocalPlayerContext>();
        }
    }

    private void Subscribe()
    {
        if (_subscribedMatchManager != matchManager)
        {
            if (_subscribedMatchManager != null)
            {
                _subscribedMatchManager.OnTurnChanged -= HandleTurnChanged;
                _subscribedMatchManager.OnMatchEnded -= HandleMatchEnded;
                _subscribedMatchManager.OnStoneRemoved -= HandleStoneRemoved;
            }

            _subscribedMatchManager = matchManager;
            if (_subscribedMatchManager != null)
            {
                _subscribedMatchManager.OnTurnChanged += HandleTurnChanged;
                _subscribedMatchManager.OnMatchEnded += HandleMatchEnded;
                _subscribedMatchManager.OnStoneRemoved += HandleStoneRemoved;
            }
        }

        if (_subscribedStoneDropper != stoneDropper)
        {
            if (_subscribedStoneDropper != null)
            {
                _subscribedStoneDropper.OnPlacementRequested -= HandlePlacementRequested;
            }

            _subscribedStoneDropper = stoneDropper;
            if (_subscribedStoneDropper != null)
            {
                _subscribedStoneDropper.OnPlacementRequested += HandlePlacementRequested;
            }
        }

        if (_subscribedLocalPlayerContext != localPlayerContext)
        {
            if (_subscribedLocalPlayerContext != null)
            {
                _subscribedLocalPlayerContext.OnLocalPlayerStateChanged -= HandleLocalPlayerStateChanged;
            }

            _subscribedLocalPlayerContext = localPlayerContext;
            if (_subscribedLocalPlayerContext != null)
            {
                _subscribedLocalPlayerContext.OnLocalPlayerStateChanged += HandleLocalPlayerStateChanged;
            }
        }
    }

    private void Unsubscribe()
    {
        if (_subscribedMatchManager != null)
        {
            _subscribedMatchManager.OnTurnChanged -= HandleTurnChanged;
            _subscribedMatchManager.OnMatchEnded -= HandleMatchEnded;
            _subscribedMatchManager.OnStoneRemoved -= HandleStoneRemoved;
            _subscribedMatchManager = null;
        }

        if (_subscribedStoneDropper != null)
        {
            _subscribedStoneDropper.OnPlacementRequested -= HandlePlacementRequested;
            _subscribedStoneDropper = null;
        }

        if (_subscribedLocalPlayerContext != null)
        {
            _subscribedLocalPlayerContext.OnLocalPlayerStateChanged -= HandleLocalPlayerStateChanged;
            _subscribedLocalPlayerContext = null;
        }
    }

    private void HandlePlacementRequested(OmokStonePlacementRequest request)
    {
        if (!ShouldSubmitRequestsToAuthority)
        {
            return;
        }

        OnPlacementRequestSubmittedToAuthority?.Invoke(request);
    }

    private void HandleTurnChanged(OmokStoneColor nextTurn)
    {
        RefreshManualPlacementState();
        RefreshInspectorState();
        SyncTurnTimerWithCurrentTurn(true);
        TryQueueAiTurn();
    }

    private void HandleMatchEnded(OmokStoneColor resultWinner)
    {
        CancelPendingAiTurn();
        RefreshManualPlacementState();
        RefreshInspectorState();
        RefreshTurnTimerSnapshot();
    }

    private void HandleStoneRemoved(OmokStoneRemovalResult removalResult)
    {
        RefreshInspectorState();
    }

    private void HandleLocalPlayerStateChanged(OmokLocalPlayerContext context)
    {
        if (_isApplyingSetup)
        {
            RefreshInspectorState();
            return;
        }

        ApplySetupToRuntime();
        RefreshInspectorState();
        TryQueueAiTurn();
    }

    private static IOmokAi CreateAi(OmokAiType aiType)
    {
        return aiType switch
        {
            OmokAiType.Easy => new OmokEasyAi(),
            OmokAiType.Normal => new OmokNormalAi(),
            _ => new OmokNormalAi()
        };
    }
}
