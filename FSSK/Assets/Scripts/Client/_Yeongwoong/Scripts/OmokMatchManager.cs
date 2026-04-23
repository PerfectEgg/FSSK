using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OmokMatchManager : MonoBehaviour
{
    private static readonly Vector2Int[] WinDirections =
    {
        new(1, 0),
        new(0, 1),
        new(1, 1),
        new(1, -1)
    };

    [Header("References")]
    [SerializeField] private OmokGrid grid;
    [SerializeField] private OmokStoneDropper stoneDropper;

    [Header("Result Overlay")]
    [SerializeField] private GameObject resultOverlayRoot;
    [SerializeField] private bool disableDropperOnMatchEnd = true;
    [SerializeField] private UnityEvent onMatchEnded;

    private OmokStoneColor[,] boardState;
    private readonly List<Vector2Int> winningCoordinates = new();
    private bool isMatchEnded;
    private OmokStoneColor winner = OmokStoneColor.None;
    private int placedStoneCount;

    public bool IsMatchEnded => isMatchEnded;
    public OmokStoneColor Winner => winner;
    public IReadOnlyList<Vector2Int> WinningCoordinates => winningCoordinates;

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

        ResetMatch();
    }

    private void OnEnable()
    {
        if (stoneDropper != null)
        {
            stoneDropper.StonePlaced += HandleStonePlaced;
        }
    }

    private void OnDisable()
    {
        if (stoneDropper != null)
        {
            stoneDropper.StonePlaced -= HandleStonePlaced;
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

        if (resultOverlayRoot != null)
        {
            resultOverlayRoot.SetActive(false);
        }

        if (disableDropperOnMatchEnd && stoneDropper != null)
        {
            stoneDropper.enabled = true;
        }
    }

    private void HandleStonePlaced(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        if (isMatchEnded || stoneColor == OmokStoneColor.None || boardState == null)
        {
            return;
        }

        if (!IsInsideBoard(coordinate) || boardState[coordinate.x, coordinate.y] != OmokStoneColor.None)
        {
            return;
        }

        boardState[coordinate.x, coordinate.y] = stoneColor;
        placedStoneCount++;

        if (TryFindWinningLine(coordinate, stoneColor, out List<Vector2Int> line))
        {
            winningCoordinates.Clear();
            winningCoordinates.AddRange(line);
            EndMatch(stoneColor);
            return;
        }

        if (placedStoneCount >= grid.BoardSize * grid.BoardSize)
        {
            winningCoordinates.Clear();
            EndMatch(OmokStoneColor.None);
        }
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

        onMatchEnded?.Invoke();
    }

    private bool TryFindWinningLine(Vector2Int coordinate, OmokStoneColor stoneColor, out List<Vector2Int> winningLine)
    {
        foreach (Vector2Int direction in WinDirections)
        {
            List<Vector2Int> line = new();
            CollectConnectedCoordinates(coordinate, new Vector2Int(-direction.x, -direction.y), stoneColor, line, true);
            line.Add(coordinate);
            CollectConnectedCoordinates(coordinate, direction, stoneColor, line, false);

            if (line.Count >= 5)
            {
                winningLine = line;
                return true;
            }
        }

        winningLine = null;
        return false;
    }

    private void CollectConnectedCoordinates(
        Vector2Int origin,
        Vector2Int direction,
        OmokStoneColor stoneColor,
        List<Vector2Int> coordinates,
        bool insertAtFront)
    {
        Vector2Int current = origin + direction;

        while (IsInsideBoard(current) && boardState[current.x, current.y] == stoneColor)
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

    private bool IsInsideBoard(Vector2Int coordinate)
    {
        if (grid == null)
        {
            return false;
        }

        return coordinate.x >= 0 &&
               coordinate.x < grid.BoardSize &&
               coordinate.y >= 0 &&
               coordinate.y < grid.BoardSize;
    }
}
