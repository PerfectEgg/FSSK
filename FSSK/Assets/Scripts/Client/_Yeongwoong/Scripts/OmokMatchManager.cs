using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class OmokMatchRules
{
    [SerializeField] private OmokStoneColor openingTurn = OmokStoneColor.Black;
    [SerializeField] private bool allowOverline = true;
    [SerializeField] private bool blockedAttemptConsumesTurn = true;
    [SerializeField] private bool allowBlockerVerticalWin = true;
    [SerializeField, Min(1)] private int blockerVerticalWinLength = 5;

    public OmokStoneColor OpeningTurn => openingTurn == OmokStoneColor.None ? OmokStoneColor.Black : openingTurn;
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
}

public readonly struct OmokManualPlacementState
{
    public OmokManualPlacementState(bool allowBlack, bool allowWhite)
    {
        AllowBlack = allowBlack;
        AllowWhite = allowWhite;
    }

    public bool AllowBlack { get; }
    public bool AllowWhite { get; }
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
            canControlAssignedColor && localPlayerColor == OmokStoneColor.Black,
            canControlAssignedColor && localPlayerColor == OmokStoneColor.White);
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
            OmokStoneColor.Black => OmokStoneColor.White,
            OmokStoneColor.White => OmokStoneColor.Black,
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
    [SerializeField] private OmokStoneColor randomRemovalOpeningColor = OmokStoneColor.Black;

    private OmokStoneColor[,] _boardState;
    private readonly List<Vector2Int> _winningCoordinates = new();
    private bool _isMatchEnded;
    private OmokStoneColor _winner = OmokStoneColor.None;
    private int _placedStoneCount;
    private OmokStoneColor _currentTurn = OmokStoneColor.Black;
    private OmokStoneColor _nextRandomRemovalColor = OmokStoneColor.Black;

    public bool IsMatchEnded => _isMatchEnded;
    public OmokStoneColor Winner => _winner;
    public IReadOnlyList<Vector2Int> WinningCoordinates => _winningCoordinates;
    public OmokStoneColor CurrentTurn => _currentTurn;
    public int BoardSize => grid != null ? grid.BoardSize : _boardState != null ? _boardState.GetLength(0) : 0;
    public OmokMatchRules Rules => rules;
    public OmokStoneColor NextRandomRemovalColor => _nextRandomRemovalColor;

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
        _currentTurn = rules != null ? rules.OpeningTurn : OmokStoneColor.Black;
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

    public OmokStoneColor[,] GetBoardSnapshot()
    {
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

        OmokStoneColor targetColor = NormalizeRemovalColor(_nextRandomRemovalColor);
        if (TryRemoveRandomStoneByColor(targetColor, out removalResult))
        {
            AdvanceRandomRemovalColor(removalResult.StoneColor);
            return true;
        }

        OmokStoneColor fallbackColor = OmokMatchFlow.GetOppositeColor(targetColor);
        if (fallbackColor != OmokStoneColor.None && TryRemoveRandomStoneByColor(fallbackColor, out removalResult))
        {
            AdvanceRandomRemovalColor(removalResult.StoneColor);
            return true;
        }

        return false;
    }

    private bool TryRemoveRandomStoneByColor(OmokStoneColor targetColor, out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;

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
        return TryRemoveStone(targetCoordinate, targetColor, out removalResult);
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
        if (!processPlacementRequestsLocally || stoneDropper == null)
        {
            return;
        }

        if (!CanAcceptPlacementRequest(request.TargetCoordinate, request.StoneColor))
        {
            return;
        }

        stoneDropper.TryExecutePlacement(request);
    }

    private void HandleStonePlaced(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        if (!applyStoneResultsLocally)
        {
            return;
        }

        TryRegisterPlacedStone(coordinate, stoneColor);
    }

    private void HandleStoneBlocked(OmokBlockedStoneResult blockedResult)
    {
        if (!applyStoneResultsLocally)
        {
            return;
        }

        TryRegisterBlockedAttempt(blockedResult);
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
        return _boardState != null &&
               stoneColor != OmokStoneColor.None &&
               CanTakeTurn(stoneColor) &&
               OmokMatchFlow.IsInsideBoard(BoardSize, coordinate) &&
               _boardState[coordinate.x, coordinate.y] == OmokStoneColor.None;
    }

    public bool TryRegisterPlacedStone(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        if (!CanAcceptPlacementRequest(coordinate, stoneColor))
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
        return stoneColor == OmokStoneColor.White ? OmokStoneColor.White : OmokStoneColor.Black;
    }

    private void AdvanceRandomRemovalColor(OmokStoneColor removedColor)
    {
        OmokStoneColor nextColor = OmokMatchFlow.GetOppositeColor(removedColor);
        _nextRandomRemovalColor = nextColor == OmokStoneColor.None ? OmokStoneColor.Black : nextColor;
    }
}
