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

    public OmokStoneColor OpeningTurn => openingTurn == OmokStoneColor.None ? OmokStoneColor.Black : openingTurn;
    public bool AllowOverline => allowOverline;
    public bool BlockedAttemptConsumesTurn => blockedAttemptConsumesTurn;

    public bool IsWinningLineLength(int lineLength)
    {
        return allowOverline ? lineLength >= 5 : lineLength == 5;
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

public static class OmokMatchFlow
{
    private static readonly Vector2Int[] WinDirections =
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
        foreach (Vector2Int direction in WinDirections)
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

    private OmokStoneColor[,] boardState;
    private readonly List<Vector2Int> winningCoordinates = new();
    private bool isMatchEnded;
    private OmokStoneColor winner = OmokStoneColor.None;
    private int placedStoneCount;
    private OmokStoneColor currentTurn = OmokStoneColor.Black;

    public bool IsMatchEnded => isMatchEnded;
    public OmokStoneColor Winner => winner;
    public IReadOnlyList<Vector2Int> WinningCoordinates => winningCoordinates;
    public OmokStoneColor CurrentTurn => currentTurn;
    public int BoardSize => grid != null ? grid.BoardSize : boardState != null ? boardState.GetLength(0) : 0;
    public OmokMatchRules Rules => rules;

    public event Action<OmokStoneColor> TurnChanged;
    public event Action<OmokStoneColor> MatchEnded;

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
            stoneDropper.PlacementRequested += HandlePlacementRequested;
            stoneDropper.StonePlaced += HandleStonePlaced;
            stoneDropper.StoneBlocked += HandleStoneBlocked;
        }
    }

    private void OnDisable()
    {
        if (stoneDropper != null)
        {
            stoneDropper.PlacementRequested -= HandlePlacementRequested;
            stoneDropper.StonePlaced -= HandleStonePlaced;
            stoneDropper.StoneBlocked -= HandleStoneBlocked;
        }
    }

    public void ResetMatch()
    {
        if (grid != null && grid.BoardSize > 0)
        {
            boardState = new OmokStoneColor[grid.BoardSize, grid.BoardSize];
        }
        else
        {
            boardState = null;
        }

        winningCoordinates.Clear();
        isMatchEnded = false;
        winner = OmokStoneColor.None;
        placedStoneCount = 0;
        currentTurn = rules != null ? rules.OpeningTurn : OmokStoneColor.Black;

        if (resultOverlayRoot != null)
        {
            resultOverlayRoot.SetActive(false);
        }

        if (disableDropperOnMatchEnd && stoneDropper != null)
        {
            stoneDropper.enabled = true;
        }

        TurnChanged?.Invoke(currentTurn);
    }

    public OmokStoneColor[,] GetBoardSnapshot()
    {
        if (boardState == null)
        {
            return null;
        }

        OmokStoneColor[,] snapshot = new OmokStoneColor[boardState.GetLength(0), boardState.GetLength(1)];
        Array.Copy(boardState, snapshot, boardState.Length);
        return snapshot;
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

    private void HandleStoneBlocked(OmokStoneColor stoneColor)
    {
        if (!applyStoneResultsLocally)
        {
            return;
        }

        TryRegisterBlockedAttempt(stoneColor);
    }

    public bool CanTakeTurn(OmokStoneColor stoneColor)
    {
        return OmokMatchFlow.CanAct(currentTurn, isMatchEnded, stoneColor);
    }

    public OmokManualPlacementState GetManualPlacementState(OmokStoneColor localPlayerColor, bool allowManualInput = true)
    {
        return OmokMatchFlow.BuildManualPlacementState(currentTurn, isMatchEnded, localPlayerColor, allowManualInput);
    }

    public bool CanAcceptPlacementRequest(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        return boardState != null &&
               stoneColor != OmokStoneColor.None &&
               CanTakeTurn(stoneColor) &&
               OmokMatchFlow.IsInsideBoard(BoardSize, coordinate) &&
               boardState[coordinate.x, coordinate.y] == OmokStoneColor.None;
    }

    public bool TryRegisterPlacedStone(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        if (!CanAcceptPlacementRequest(coordinate, stoneColor))
        {
            return false;
        }

        boardState[coordinate.x, coordinate.y] = stoneColor;
        placedStoneCount++;

        if (OmokMatchFlow.TryFindWinningLine(boardState, coordinate, stoneColor, rules, out List<Vector2Int> line))
        {
            winningCoordinates.Clear();
            winningCoordinates.AddRange(line);
            EndMatch(stoneColor);
            return true;
        }

        if (placedStoneCount >= BoardSize * BoardSize)
        {
            winningCoordinates.Clear();
            EndMatch(OmokStoneColor.None);
            return true;
        }

        currentTurn = OmokMatchFlow.GetOppositeColor(stoneColor);
        TurnChanged?.Invoke(currentTurn);
        return true;
    }

    public bool TryRegisterBlockedAttempt(OmokStoneColor stoneColor)
    {
        if (boardState == null ||
            rules == null ||
            !rules.BlockedAttemptConsumesTurn ||
            !CanTakeTurn(stoneColor))
        {
            return false;
        }

        currentTurn = OmokMatchFlow.GetOppositeColor(stoneColor);
        TurnChanged?.Invoke(currentTurn);
        return true;
    }

    private void EndMatch(OmokStoneColor resultWinner)
    {
        isMatchEnded = true;
        winner = resultWinner;

        if (disableDropperOnMatchEnd && stoneDropper != null)
        {
            stoneDropper.enabled = false;
        }

        if (resultOverlayRoot != null)
        {
            resultOverlayRoot.SetActive(true);
        }

        MatchEnded?.Invoke(resultWinner);
        onMatchEnded?.Invoke();
    }
}
