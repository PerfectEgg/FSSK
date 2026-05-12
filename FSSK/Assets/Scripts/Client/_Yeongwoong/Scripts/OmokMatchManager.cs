using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class OmokMatchRules
{
    [SerializeField] private OmokStoneColor openingTurn = OmokStoneColor.Gold;
    [SerializeField] private bool allowOverline = true;
    [SerializeField] private bool blockedAttemptConsumesTurn = true;
    [SerializeField] private bool allowBlockerVerticalWin = true;
    [SerializeField, Min(1)] private int blockerVerticalWinLength = 5;

    public OmokStoneColor OpeningTurn => openingTurn == OmokStoneColor.None ? OmokStoneColor.Gold : openingTurn;
    public bool AllowOverline => allowOverline;
    public bool BlockedAttemptConsumesTurn => blockedAttemptConsumesTurn;
    public bool AllowBlockerVerticalWin => allowBlockerVerticalWin;
    public int BlockerVerticalWinLength => Mathf.Max(1, blockerVerticalWinLength);

    public bool IsWinningLineLength(int lineLength)
    {
        return allowOverline ? lineLength >= 5 : lineLength == 5;
    }

    public bool IsWinningBlockerStackLength(int stackLength)
    {
        return allowBlockerVerticalWin && stackLength >= BlockerVerticalWinLength;
    }

    public void Configure(
        OmokStoneColor nextOpeningTurn,
        bool nextAllowOverline,
        bool nextBlockedAttemptConsumesTurn,
        bool nextAllowBlockerVerticalWin,
        int nextBlockerVerticalWinLength)
    {
        openingTurn = nextOpeningTurn == OmokStoneColor.None ? OmokStoneColor.Gold : nextOpeningTurn;
        allowOverline = nextAllowOverline;
        blockedAttemptConsumesTurn = nextBlockedAttemptConsumesTurn;
        allowBlockerVerticalWin = nextAllowBlockerVerticalWin;
        blockerVerticalWinLength = Mathf.Max(1, nextBlockerVerticalWinLength);
    }
}

public readonly struct OmokManualPlacementState
{
    public OmokManualPlacementState(bool allowGold, bool allowSilver)
    {
        AllowGold = allowGold;
        AllowSilver = allowSilver;
    }

    public bool AllowGold { get; }
    public bool AllowSilver { get; }
}

public readonly struct OmokStoneRemovalResult
{
    public OmokStoneRemovalResult(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        Coordinate = coordinate;
        StoneColor = stoneColor;
    }

    public Vector2Int Coordinate { get; }
    public OmokStoneColor StoneColor { get; }
}

public static class OmokMatchFlow
{
    private static readonly Vector2Int[] _winDirections =
    {
        new(1, 0),
        new(0, 1),
        new(1, 1),
        new(1, -1)
    };

    public static bool CanAct(OmokStoneColor currentTurn, bool isMatchEnded, OmokStoneColor actorColor)
    {
        return !isMatchEnded &&
               actorColor != OmokStoneColor.None &&
               currentTurn == actorColor;
    }

    public static OmokManualPlacementState BuildManualPlacementState(
        OmokStoneColor currentTurn,
        bool isMatchEnded,
        OmokStoneColor localPlayerColor,
        bool allowManualInput)
    {
        if (!allowManualInput || localPlayerColor == OmokStoneColor.None)
        {
            return new OmokManualPlacementState(false, false);
        }

        bool canControlAssignedColor = CanAct(currentTurn, isMatchEnded, localPlayerColor);
        return new OmokManualPlacementState(
            canControlAssignedColor && localPlayerColor == OmokStoneColor.Gold,
            canControlAssignedColor && localPlayerColor == OmokStoneColor.Silver);
    }

    public static bool IsInsideBoard(int boardSize, Vector2Int coordinate)
    {
        return coordinate.x >= 0 &&
               coordinate.x < boardSize &&
               coordinate.y >= 0 &&
               coordinate.y < boardSize;
    }

    public static OmokStoneColor GetOppositeColor(OmokStoneColor stoneColor)
    {
        return stoneColor switch
        {
            OmokStoneColor.Gold => OmokStoneColor.Silver,
            OmokStoneColor.Silver => OmokStoneColor.Gold,
            _ => OmokStoneColor.None
        };
    }

    public static bool TryFindWinningLine(
        OmokStoneColor[,] boardState,
        Vector2Int coordinate,
        OmokStoneColor stoneColor,
        OmokMatchRules rules,
        out List<Vector2Int> winningLine)
    {
        winningLine = null;

        if (boardState == null || stoneColor == OmokStoneColor.None || rules == null)
        {
            return false;
        }

        int boardSize = boardState.GetLength(0);
        foreach (Vector2Int direction in _winDirections)
        {
            List<Vector2Int> line = new();
            CollectConnectedCoordinates(boardState, boardSize, coordinate, new Vector2Int(-direction.x, -direction.y), stoneColor, line, true);
            line.Add(coordinate);
            CollectConnectedCoordinates(boardState, boardSize, coordinate, direction, stoneColor, line, false);

            if (rules.IsWinningLineLength(line.Count))
            {
                winningLine = line;
                return true;
            }
        }

        return false;
    }

    private static void CollectConnectedCoordinates(
        OmokStoneColor[,] boardState,
        int boardSize,
        Vector2Int origin,
        Vector2Int direction,
        OmokStoneColor stoneColor,
        List<Vector2Int> coordinates,
        bool insertAtFront)
    {
        Vector2Int current = origin + direction;

        while (IsInsideBoard(boardSize, current) && boardState[current.x, current.y] == stoneColor)
        {
            if (insertAtFront)
            {
                coordinates.Insert(0, current);
            }
            else
            {
                coordinates.Add(current);
            }

            current += direction;
        }
    }
}

public class OmokMatchManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OmokGrid grid;
    [SerializeField] private OmokStoneDropper stoneDropper;

    [Header("Authority")]
    [SerializeField] private bool processPlacementRequestsLocally = true;
    [SerializeField] private bool applyStoneResultsLocally = true;

    [Header("Rules")]
    [SerializeField] private OmokMatchRules rules = new();

    [Header("Result Overlay")]
    [SerializeField] private GameObject resultOverlayRoot;
    [SerializeField] private bool disableDropperOnMatchEnd = true;
    [SerializeField] private UnityEvent onMatchEnded;

    [Header("Random Removal")]
    [SerializeField] private OmokStoneColor randomRemovalOpeningColor = OmokStoneColor.Gold;

    [Header("Debug")]
    [SerializeField] private bool logPlacementFailures = true;

    private OmokStoneColor[,] _boardState;
    private readonly List<Vector2Int> _winningCoordinates = new();
    private readonly List<OmokBoardStoneState> _liveBoardStoneBuffer = new();
    private bool _isMatchEnded;
    private OmokStoneColor _winner = OmokStoneColor.None;
    private int _placedStoneCount;
    private OmokStoneColor _currentTurn = OmokStoneColor.Gold;
    private OmokStoneColor _nextRandomRemovalColor = OmokStoneColor.Gold;

    public bool IsMatchEnded => _isMatchEnded;
    public OmokStoneColor Winner => _winner;
    public IReadOnlyList<Vector2Int> WinningCoordinates => _winningCoordinates;
    public OmokStoneColor CurrentTurn => _currentTurn;
    public int BoardSize => grid != null ? grid.BoardSize : _boardState != null ? _boardState.GetLength(0) : 0;
    public OmokMatchRules Rules => rules;
    public bool ProcessPlacementRequestsLocally => processPlacementRequestsLocally;
    public bool ApplyStoneResultsLocally => applyStoneResultsLocally;
    public OmokStoneColor NextRandomRemovalColor => _nextRandomRemovalColor;
    public OmokStoneColor NextRemovalColor => _nextRandomRemovalColor;

    public event Action<OmokStoneColor> OnTurnChanged;
    public event Action<OmokStoneColor> OnMatchEnded;
    public event Action<OmokStoneRemovalResult> OnStoneRemoved;

    private void Reset()
    {
        grid = GetComponent<OmokGrid>();
        stoneDropper = GetComponent<OmokStoneDropper>();
    }

    private void Awake()
    {
        if (grid == null)
        {
            grid = GetComponent<OmokGrid>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponent<OmokStoneDropper>();
        }

        rules ??= new OmokMatchRules();
        ResetMatch();
    }

    private void OnEnable()
    {
        if (stoneDropper != null)
        {
            stoneDropper.OnPlacementRequested += HandlePlacementRequested;
            stoneDropper.OnStonePlaced += HandleStonePlaced;
            stoneDropper.OnStoneBlocked += HandleStoneBlocked;
        }
    }

    private void OnDisable()
    {
        if (stoneDropper != null)
        {
            stoneDropper.OnPlacementRequested -= HandlePlacementRequested;
            stoneDropper.OnStonePlaced -= HandleStonePlaced;
            stoneDropper.OnStoneBlocked -= HandleStoneBlocked;
        }
    }

    public void ResetMatch()
    {
        if (grid != null && grid.BoardSize > 0)
        {
            _boardState = new OmokStoneColor[grid.BoardSize, grid.BoardSize];
        }
        else
        {
            _boardState = null;
        }

        _winningCoordinates.Clear();
        _isMatchEnded = false;
        _winner = OmokStoneColor.None;
        _placedStoneCount = 0;
        _currentTurn = rules != null ? rules.OpeningTurn : OmokStoneColor.Gold;
        _nextRandomRemovalColor = NormalizeRemovalColor(randomRemovalOpeningColor);

        if (resultOverlayRoot != null)
        {
            resultOverlayRoot.SetActive(false);
        }

        if (disableDropperOnMatchEnd && stoneDropper != null)
        {
            stoneDropper.enabled = true;
        }

        OnTurnChanged?.Invoke(_currentTurn);
    }

    public void SetAuthorityMode(bool shouldProcessPlacementRequestsLocally, bool shouldApplyStoneResultsLocally)
    {
        processPlacementRequestsLocally = shouldProcessPlacementRequestsLocally;
        applyStoneResultsLocally = shouldApplyStoneResultsLocally;
    }

    public void ConfigureRules(
        OmokStoneColor openingTurn,
        bool allowOverline,
        bool blockedAttemptConsumesTurn,
        bool allowBlockerVerticalWin,
        int blockerVerticalWinLength)
    {
        rules ??= new OmokMatchRules();
        rules.Configure(
            openingTurn,
            allowOverline,
            blockedAttemptConsumesTurn,
            allowBlockerVerticalWin,
            blockerVerticalWinLength);

        if (!Application.isPlaying || (!_isMatchEnded && _placedStoneCount == 0))
        {
            _currentTurn = rules.OpeningTurn;
            if (Application.isPlaying)
            {
                OnTurnChanged?.Invoke(_currentTurn);
            }
        }
    }

    public OmokStoneColor[,] GetBoardSnapshot()
    {
        SyncBoardStateWithLiveStones();
        TryEndMatchFromExistingBoard();

        if (_boardState == null)
        {
            return null;
        }

        OmokStoneColor[,] snapshot = new OmokStoneColor[_boardState.GetLength(0), _boardState.GetLength(1)];
        Array.Copy(_boardState, snapshot, _boardState.Length);
        return snapshot;
    }

    public bool TryRemoveRandomStone()
    {
        return TryRemoveRandomStone(out _);
    }

    public bool TryRemoveRandomStone(out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;

        if (!TrySelectNextRemovalTarget(out OmokStoneRemovalResult removalTarget))
        {
            return false;
        }

        return TryConfirmRemoveStone(removalTarget, out removalResult);
    }

    public bool TrySelectNextRemovalTarget(out OmokStoneRemovalResult removalTarget)
    {
        removalTarget = default;

        OmokStoneColor targetColor = NormalizeRemovalColor(_nextRandomRemovalColor);
        return TrySelectRemovalTargetByColor(targetColor, out removalTarget);
    }

    public bool TryConfirmRemoveStone(OmokStoneRemovalResult removalTarget)
    {
        return TryConfirmRemoveStone(removalTarget, out _);
    }

    public bool TryConfirmRemoveStone(OmokStoneRemovalResult removalTarget, out OmokStoneRemovalResult removalResult)
    {
        if (TryRemoveStone(removalTarget.Coordinate, removalTarget.StoneColor, out removalResult))
        {
            AdvanceRandomRemovalColor(removalResult.StoneColor);
            return true;
        }

        return false;
    }

    private bool TrySelectRemovalTargetByColor(OmokStoneColor targetColor, out OmokStoneRemovalResult removalTarget)
    {
        removalTarget = default;
        SyncBoardStateWithLiveStones();

        if (_boardState == null || _isMatchEnded || targetColor == OmokStoneColor.None)
        {
            return false;
        }

        List<Vector2Int> candidates = new();
        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_boardState[x, y] == targetColor)
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        Vector2Int targetCoordinate = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        removalTarget = new OmokStoneRemovalResult(targetCoordinate, targetColor);
        return true;
    }

    private bool TryRemoveStone(Vector2Int coordinate, OmokStoneColor expectedColor, out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;

        if (_boardState == null ||
            _isMatchEnded ||
            !OmokMatchFlow.IsInsideBoard(BoardSize, coordinate))
        {
            return false;
        }

        OmokStoneColor removedColor = _boardState[coordinate.x, coordinate.y];
        if (removedColor == OmokStoneColor.None ||
            (expectedColor != OmokStoneColor.None && removedColor != expectedColor))
        {
            return false;
        }

        if (stoneDropper != null && !stoneDropper.TryRemoveStoneAt(coordinate, removedColor))
        {
            return false;
        }

        _boardState[coordinate.x, coordinate.y] = OmokStoneColor.None;
        _placedStoneCount = Mathf.Max(0, _placedStoneCount - 1);
        _winningCoordinates.Clear();
        removalResult = new OmokStoneRemovalResult(coordinate, removedColor);
        OnStoneRemoved?.Invoke(removalResult);
        return true;
    }

    private void HandlePlacementRequested(OmokStonePlacementRequest request)
    {
        if (!processPlacementRequestsLocally)
        {
            return;
        }

        TryProcessPlacementRequest(request);
    }

    private void HandleStonePlaced(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        if (!applyStoneResultsLocally)
        {
            return;
        }

        TryApplyPlacementResult(coordinate, stoneColor);
    }

    private void HandleStoneBlocked(OmokBlockedStoneResult blockedResult)
    {
        if (!applyStoneResultsLocally)
        {
            return;
        }

        TryApplyBlockedResult(blockedResult);
    }

    public bool TryProcessPlacementRequest(OmokStonePlacementRequest request)
    {
        if (stoneDropper == null)
        {
            LogPlacementFailure($"Rejected {request.StoneColor} at {request.TargetCoordinate}: stoneDropper is missing.");
            return false;
        }

        if (!CanAcceptPlacementRequest(request))
        {
            LogPlacementFailure($"Rejected {request.StoneColor} at {request.TargetCoordinate}: {GetPlacementRejectReason(request)}");
            return false;
        }

        if (!stoneDropper.TryExecutePlacement(request))
        {
            LogPlacementFailure($"Rejected {request.StoneColor} at {request.TargetCoordinate}: stoneDropper.TryExecutePlacement failed.");
            return false;
        }

        return true;
    }

    public bool TryApplyPlacementResult(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        return TryRegisterPlacedStone(coordinate, stoneColor);
    }

    public bool TryApplyBlockedResult(OmokBlockedStoneResult blockedResult)
    {
        return TryRegisterBlockedAttempt(blockedResult);
    }

    public bool TryPassTurn(OmokStoneColor expectedTurn)
    {
        return TryPassTurn(expectedTurn, out _);
    }

    public bool TryPassTurn(OmokStoneColor expectedTurn, out OmokStoneColor nextTurn)
    {
        nextTurn = OmokStoneColor.None;

        if (_isMatchEnded ||
            expectedTurn == OmokStoneColor.None ||
            _currentTurn != expectedTurn)
        {
            return false;
        }

        nextTurn = OmokMatchFlow.GetOppositeColor(expectedTurn);
        _currentTurn = nextTurn;
        OnTurnChanged?.Invoke(_currentTurn);
        return true;
    }

    public bool TryApplyAuthoritativeTurnPass(OmokStoneColor expectedPreviousTurn, OmokStoneColor authoritativeNextTurn)
    {
        if (_isMatchEnded ||
            authoritativeNextTurn == OmokStoneColor.None)
        {
            return false;
        }

        if (_currentTurn == authoritativeNextTurn)
        {
            return true;
        }

        if (expectedPreviousTurn != OmokStoneColor.None &&
            _currentTurn != expectedPreviousTurn)
        {
            Debug.LogWarning(
                $"[OmokMatchManager] Applying authoritative turn pass while local turn is {_currentTurn}; expected {expectedPreviousTurn}, next {authoritativeNextTurn}.",
                this);
        }

        _currentTurn = authoritativeNextTurn;
        OnTurnChanged?.Invoke(_currentTurn);
        return true;
    }

    public bool CanTakeTurn(OmokStoneColor stoneColor)
    {
        return OmokMatchFlow.CanAct(_currentTurn, _isMatchEnded, stoneColor);
    }

    public OmokManualPlacementState GetManualPlacementState(OmokStoneColor localPlayerColor, bool allowManualInput = true)
    {
        return OmokMatchFlow.BuildManualPlacementState(_currentTurn, _isMatchEnded, localPlayerColor, allowManualInput);
    }

    public bool CanAcceptPlacementRequest(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        SyncBoardStateWithLiveStones();
        TryEndMatchFromExistingBoard();
        return CanAcceptPlacementRequestCore(coordinate, stoneColor);
    }

    public bool CanAcceptPlacementRequest(OmokStonePlacementRequest request)
    {
        SyncBoardStateWithLiveStones();
        TryEndMatchFromExistingBoard();

        if (_boardState == null ||
            request.StoneColor == OmokStoneColor.None ||
            !CanTakeTurn(request.StoneColor) ||
            !OmokMatchFlow.IsInsideBoard(BoardSize, request.TargetCoordinate))
        {
            return false;
        }

        return request.AllowBlockedCoordinateForBlocker ||
               _boardState[request.TargetCoordinate.x, request.TargetCoordinate.y] == OmokStoneColor.None;
    }

    public bool TryRegisterPlacedStone(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        SyncBoardStateWithLiveStones(coordinate);

        if (!CanAcceptPlacementRequestCore(coordinate, stoneColor))
        {
            return false;
        }

        _boardState[coordinate.x, coordinate.y] = stoneColor;
        _placedStoneCount++;

        if (OmokMatchFlow.TryFindWinningLine(_boardState, coordinate, stoneColor, rules, out List<Vector2Int> line))
        {
            _winningCoordinates.Clear();
            _winningCoordinates.AddRange(line);
            EndMatch(stoneColor);
            return true;
        }

        if (_placedStoneCount >= BoardSize * BoardSize)
        {
            _winningCoordinates.Clear();
            EndMatch(OmokStoneColor.None);
            return true;
        }

        _currentTurn = OmokMatchFlow.GetOppositeColor(stoneColor);
        OnTurnChanged?.Invoke(_currentTurn);
        return true;
    }

    private bool CanAcceptPlacementRequestCore(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        return _boardState != null &&
               stoneColor != OmokStoneColor.None &&
               CanTakeTurn(stoneColor) &&
               OmokMatchFlow.IsInsideBoard(BoardSize, coordinate) &&
               _boardState[coordinate.x, coordinate.y] == OmokStoneColor.None;
    }

    private void SyncBoardStateWithLiveStones()
    {
        SyncBoardStateWithLiveStones(default, false);
    }

    private void SyncBoardStateWithLiveStones(Vector2Int ignoredCoordinate)
    {
        SyncBoardStateWithLiveStones(ignoredCoordinate, true);
    }

    private void SyncBoardStateWithLiveStones(Vector2Int ignoredCoordinate, bool hasIgnoredCoordinate)
    {
        if (_boardState == null || stoneDropper == null)
        {
            return;
        }

        int boardSize = BoardSize;
        if (boardSize <= 0)
        {
            return;
        }

        _liveBoardStoneBuffer.Clear();
        stoneDropper.CollectLiveBoardStones(_liveBoardStoneBuffer);

        OmokStoneColor[,] syncedBoardState = new OmokStoneColor[boardSize, boardSize];
        int liveStoneCount = 0;
        foreach (OmokBoardStoneState boardStone in _liveBoardStoneBuffer)
        {
            Vector2Int coordinate = boardStone.Coordinate;
            if ((hasIgnoredCoordinate && coordinate == ignoredCoordinate) ||
                boardStone.StoneColor == OmokStoneColor.None ||
                !OmokMatchFlow.IsInsideBoard(boardSize, coordinate) ||
                syncedBoardState[coordinate.x, coordinate.y] != OmokStoneColor.None)
            {
                continue;
            }

            syncedBoardState[coordinate.x, coordinate.y] = boardStone.StoneColor;
            liveStoneCount++;
        }

        if (!HasBoardStateChanged(syncedBoardState, liveStoneCount))
        {
            return;
        }

        _boardState = syncedBoardState;
        _placedStoneCount = liveStoneCount;
        if (!_isMatchEnded)
        {
            _winningCoordinates.Clear();
        }
    }

    private bool HasBoardStateChanged(OmokStoneColor[,] nextBoardState, int nextPlacedStoneCount)
    {
        if (_boardState == null ||
            nextBoardState == null ||
            _boardState.GetLength(0) != nextBoardState.GetLength(0) ||
            _boardState.GetLength(1) != nextBoardState.GetLength(1) ||
            _placedStoneCount != nextPlacedStoneCount)
        {
            return true;
        }

        for (int y = 0; y < _boardState.GetLength(1); y++)
        {
            for (int x = 0; x < _boardState.GetLength(0); x++)
            {
                if (_boardState[x, y] != nextBoardState[x, y])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void TryEndMatchFromExistingBoard()
    {
        if (_isMatchEnded ||
            _boardState == null ||
            rules == null ||
            !TryFindExistingWinningLine(out OmokStoneColor resultWinner, out List<Vector2Int> winningLine))
        {
            return;
        }

        _winningCoordinates.Clear();
        _winningCoordinates.AddRange(winningLine);
        EndMatch(resultWinner);
    }

    private bool TryFindExistingWinningLine(out OmokStoneColor resultWinner, out List<Vector2Int> winningLine)
    {
        resultWinner = OmokStoneColor.None;
        winningLine = null;

        if (_boardState == null)
        {
            return false;
        }

        int boardSize = BoardSize;
        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                OmokStoneColor stoneColor = _boardState[x, y];
                if (stoneColor == OmokStoneColor.None)
                {
                    continue;
                }

                Vector2Int coordinate = new(x, y);
                if (!OmokMatchFlow.TryFindWinningLine(_boardState, coordinate, stoneColor, rules, out List<Vector2Int> line))
                {
                    continue;
                }

                resultWinner = stoneColor;
                winningLine = line;
                return true;
            }
        }

        return false;
    }

    public bool TryRegisterBlockedAttempt(OmokBlockedStoneResult blockedResult)
    {
        if (_boardState == null ||
            rules == null ||
            !CanTakeTurn(blockedResult.StoneColor))
        {
            return false;
        }

        if (rules.IsWinningBlockerStackLength(blockedResult.ConsecutiveSameColorStackCount))
        {
            _winningCoordinates.Clear();
            EndMatch(blockedResult.StoneColor);
            return true;
        }

        if (!rules.BlockedAttemptConsumesTurn)
        {
            return false;
        }

        _currentTurn = OmokMatchFlow.GetOppositeColor(blockedResult.StoneColor);
        OnTurnChanged?.Invoke(_currentTurn);
        return true;
    }

    public void ForceEndMatchAsDraw()
    {
        if (_isMatchEnded) return;
        EndMatch(OmokStoneColor.None);
    }

    private void EndMatch(OmokStoneColor resultWinner)
    {
        _isMatchEnded = true;
        _winner = resultWinner;

        if (disableDropperOnMatchEnd && stoneDropper != null)
        {
            stoneDropper.enabled = false;
        }

        if (resultOverlayRoot != null)
        {
            resultOverlayRoot.SetActive(true);
        }

        OnMatchEnded?.Invoke(resultWinner);
        onMatchEnded?.Invoke();
    }

    private static OmokStoneColor NormalizeRemovalColor(OmokStoneColor stoneColor)
    {
        return stoneColor == OmokStoneColor.Silver ? OmokStoneColor.Silver : OmokStoneColor.Gold;
    }

    private string GetPlacementRejectReason(OmokStonePlacementRequest request)
    {
        if (_boardState == null)
        {
            return "board state is not initialized";
        }

        if (request.StoneColor == OmokStoneColor.None)
        {
            return "stone color is None";
        }

        if (!CanTakeTurn(request.StoneColor))
        {
            return $"not {request.StoneColor}'s turn (currentTurn={_currentTurn}, isEnded={_isMatchEnded}, winner={_winner})";
        }

        if (!OmokMatchFlow.IsInsideBoard(BoardSize, request.TargetCoordinate))
        {
            return $"target is outside board (boardSize={BoardSize})";
        }

        OmokStoneColor cellState = _boardState[request.TargetCoordinate.x, request.TargetCoordinate.y];
        if (!request.AllowBlockedCoordinateForBlocker && cellState != OmokStoneColor.None)
        {
            return $"target cell is occupied by {cellState}";
        }

        return "unknown match gate";
    }

    private void LogPlacementFailure(string message)
    {
        if (logPlacementFailures)
        {
            Debug.LogWarning($"[OmokMatchManager] {message}", this);
        }
    }

    private void AdvanceRandomRemovalColor(OmokStoneColor removedColor)
    {
        OmokStoneColor nextColor = OmokMatchFlow.GetOppositeColor(removedColor);
        _nextRandomRemovalColor = nextColor == OmokStoneColor.None ? OmokStoneColor.Gold : nextColor;
    }
}
