using System.Collections.Generic;
using UnityEngine;

internal static class OmokAiLogic
{
    private static readonly Vector2Int[] _directions =
    {
        new(1, 0),
        new(0, 1),
        new(1, 1),
        new(1, -1)
    };

    public static List<Vector2Int> CollectCandidateMoves(OmokStoneColor[,] boardState)
    {
        int size = boardState.GetLength(0);
        HashSet<Vector2Int> candidates = new();
        bool hasStone = false;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (boardState[x, y] == OmokStoneColor.None)
                {
                    continue;
                }

                hasStone = true;

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int candidateX = x + offsetX;
                        int candidateY = y + offsetY;

                        if (!IsInsideBoard(boardState, candidateX, candidateY) ||
                            boardState[candidateX, candidateY] != OmokStoneColor.None)
                        {
                            continue;
                        }

                        candidates.Add(new Vector2Int(candidateX, candidateY));
                    }
                }
            }
        }

        if (!hasStone)
        {
            int center = size / 2;
            candidates.Add(new Vector2Int(center, center));
        }

        return new List<Vector2Int>(candidates);
    }

    public static bool TryFindImmediateWinningMove(
        OmokStoneColor[,] boardState,
        OmokStoneColor stoneColor,
        List<Vector2Int> candidates,
        out Vector2Int move)
    {
        move = default;

        foreach (Vector2Int candidate in candidates)
        {
            if (!TryPlaceVirtualStone(boardState, candidate, stoneColor))
            {
                continue;
            }

            bool isWinningMove = IsWinningMove(boardState, candidate, stoneColor);
            boardState[candidate.x, candidate.y] = OmokStoneColor.None;

            if (!isWinningMove)
            {
                continue;
            }

            move = candidate;
            return true;
        }

        return false;
    }

    public static List<Vector2Int> CollectThreeBlockMoves(
        OmokStoneColor[,] boardState,
        OmokStoneColor stoneColor,
        List<Vector2Int> candidates)
    {
        HashSet<Vector2Int> candidateSet = new(candidates);
        HashSet<Vector2Int> blockMoveSet = new(FindOpenThreeBlockMoves(boardState, stoneColor, candidateSet));
        foreach (Vector2Int brokenThreeBlock in FindBrokenThreeBlockMoves(boardState, stoneColor, candidateSet))
        {
            blockMoveSet.Add(brokenThreeBlock);
        }

        return new List<Vector2Int>(blockMoveSet);
    }

    public static int EvaluateCandidate(
        OmokStoneColor[,] boardState,
        Vector2Int candidate,
        OmokStoneColor aiColor,
        OmokStoneColor opponentColor)
    {
        int size = boardState.GetLength(0);
        int center = size / 2;
        int score = (size - Mathf.Abs(candidate.x - center) - Mathf.Abs(candidate.y - center)) * 4;

        score += EvaluateColorPatterns(boardState, candidate, aiColor) * 2;
        score += EvaluateColorPatterns(boardState, candidate, opponentColor);
        score += CountNeighborStones(boardState, candidate, aiColor) * 15;
        score += CountNeighborStones(boardState, candidate, opponentColor) * 10;

        return score;
    }

    public static int CountNeighborStones(OmokStoneColor[,] boardState, Vector2Int candidate, OmokStoneColor targetColor)
    {
        int count = 0;

        for (int y = candidate.y - 1; y <= candidate.y + 1; y++)
        {
            for (int x = candidate.x - 1; x <= candidate.x + 1; x++)
            {
                if ((x == candidate.x && y == candidate.y) || !IsInsideBoard(boardState, x, y))
                {
                    continue;
                }

                if (boardState[x, y] == targetColor)
                {
                    count++;
                }
            }
        }

        return count;
    }

    public static int GetLongestLineLengthAfterMove(
        OmokStoneColor[,] boardState,
        Vector2Int candidate,
        OmokStoneColor stoneColor)
    {
        if (!TryPlaceVirtualStone(boardState, candidate, stoneColor))
        {
            return 0;
        }

        int bestLength = 0;
        foreach (Vector2Int direction in _directions)
        {
            int totalCount = 1 +
                             CountConnectedStones(boardState, candidate, direction, stoneColor) +
                             CountConnectedStones(boardState, candidate, -direction, stoneColor);
            bestLength = Mathf.Max(bestLength, totalCount);
        }

        boardState[candidate.x, candidate.y] = OmokStoneColor.None;
        return bestLength;
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

    private static List<Vector2Int> FindOpenThreeBlockMoves(
        OmokStoneColor[,] boardState,
        OmokStoneColor stoneColor,
        HashSet<Vector2Int> candidateSet)
    {
        HashSet<Vector2Int> blockMoves = new();
        int width = boardState.GetLength(0);
        int height = boardState.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (boardState[x, y] != stoneColor)
                {
                    continue;
                }

                Vector2Int start = new(x, y);
                foreach (Vector2Int direction in _directions)
                {
                    if (IsInsideBoard(boardState, start.x - direction.x, start.y - direction.y) &&
                        boardState[start.x - direction.x, start.y - direction.y] == stoneColor)
                    {
                        continue;
                    }

                    Vector2Int second = start + direction;
                    Vector2Int third = start + direction * 2;
                    Vector2Int after = start + direction * 3;

                    if (!IsInsideBoard(boardState, second.x, second.y) ||
                        !IsInsideBoard(boardState, third.x, third.y) ||
                        boardState[second.x, second.y] != stoneColor ||
                        boardState[third.x, third.y] != stoneColor)
                    {
                        continue;
                    }

                    if (IsInsideBoard(boardState, after.x, after.y) && boardState[after.x, after.y] == stoneColor)
                    {
                        continue;
                    }

                    Vector2Int frontEnd = start - direction;
                    Vector2Int backEnd = after;
                    if (!IsEmptyCell(boardState, frontEnd) || !IsEmptyCell(boardState, backEnd))
                    {
                        continue;
                    }

                    if (candidateSet.Contains(frontEnd))
                    {
                        blockMoves.Add(frontEnd);
                    }

                    if (candidateSet.Contains(backEnd))
                    {
                        blockMoves.Add(backEnd);
                    }
                }
            }
        }

        return new List<Vector2Int>(blockMoves);
    }

    private static List<Vector2Int> FindBrokenThreeBlockMoves(
        OmokStoneColor[,] boardState,
        OmokStoneColor stoneColor,
        HashSet<Vector2Int> candidateSet)
    {
        HashSet<Vector2Int> blockMoves = new();
        int width = boardState.GetLength(0);
        int height = boardState.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int start = new(x, y);
                foreach (Vector2Int direction in _directions)
                {
                    if (!TryGetCellState(boardState, start, direction, 0, out OmokStoneColor cell0) ||
                        !TryGetCellState(boardState, start, direction, 1, out OmokStoneColor cell1) ||
                        !TryGetCellState(boardState, start, direction, 2, out OmokStoneColor cell2) ||
                        !TryGetCellState(boardState, start, direction, 3, out OmokStoneColor cell3) ||
                        !TryGetCellState(boardState, start, direction, 4, out OmokStoneColor cell4) ||
                        !TryGetCellState(boardState, start, direction, 5, out OmokStoneColor cell5))
                    {
                        continue;
                    }

                    bool isLeftBrokenThree =
                        cell0 == OmokStoneColor.None &&
                        cell1 == stoneColor &&
                        cell2 == stoneColor &&
                        cell3 == OmokStoneColor.None &&
                        cell4 == stoneColor &&
                        cell5 == OmokStoneColor.None;

                    bool isRightBrokenThree =
                        cell0 == OmokStoneColor.None &&
                        cell1 == stoneColor &&
                        cell2 == OmokStoneColor.None &&
                        cell3 == stoneColor &&
                        cell4 == stoneColor &&
                        cell5 == OmokStoneColor.None;

                    if (!isLeftBrokenThree && !isRightBrokenThree)
                    {
                        continue;
                    }

                    AddBlockMoveIfCandidate(boardState, candidateSet, blockMoves, start, direction, 0);
                    AddBlockMoveIfCandidate(boardState, candidateSet, blockMoves, start, direction, 5);

                    if (isLeftBrokenThree)
                    {
                        AddBlockMoveIfCandidate(boardState, candidateSet, blockMoves, start, direction, 3);
                    }

                    if (isRightBrokenThree)
                    {
                        AddBlockMoveIfCandidate(boardState, candidateSet, blockMoves, start, direction, 2);
                    }
                }
            }
        }

        return new List<Vector2Int>(blockMoves);
    }

    private static int EvaluateColorPatterns(OmokStoneColor[,] boardState, Vector2Int candidate, OmokStoneColor stoneColor)
    {
        if (!TryPlaceVirtualStone(boardState, candidate, stoneColor))
        {
            return int.MinValue / 4;
        }

        int score = 0;
        foreach (Vector2Int direction in _directions)
        {
            int totalCount = 1 +
                             CountConnectedStones(boardState, candidate, direction, stoneColor) +
                             CountConnectedStones(boardState, candidate, -direction, stoneColor);
            int openEnds = CountOpenEnds(boardState, candidate, direction, stoneColor);

            score += ScoreLine(totalCount, openEnds);
        }

        boardState[candidate.x, candidate.y] = OmokStoneColor.None;
        return score;
    }

    private static int ScoreLine(int totalCount, int openEnds)
    {
        if (totalCount >= 5)
        {
            return 100000;
        }

        if (totalCount == 4)
        {
            return openEnds == 2 ? 16000 : 7000;
        }

        if (totalCount == 3)
        {
            return openEnds == 2 ? 3500 : 900;
        }

        if (totalCount == 2)
        {
            return openEnds == 2 ? 450 : 120;
        }

        return openEnds > 0 ? 35 : 0;
    }

    private static int CountConnectedStones(
        OmokStoneColor[,] boardState,
        Vector2Int origin,
        Vector2Int direction,
        OmokStoneColor stoneColor)
    {
        int count = 0;
        Vector2Int current = origin + direction;

        while (IsInsideBoard(boardState, current.x, current.y) && boardState[current.x, current.y] == stoneColor)
        {
            count++;
            current += direction;
        }

        return count;
    }

    private static int CountOpenEnds(
        OmokStoneColor[,] boardState,
        Vector2Int origin,
        Vector2Int direction,
        OmokStoneColor stoneColor)
    {
        int openEnds = 0;

        Vector2Int forward = origin + direction;
        while (IsInsideBoard(boardState, forward.x, forward.y) && boardState[forward.x, forward.y] == stoneColor)
        {
            forward += direction;
        }

        if (IsInsideBoard(boardState, forward.x, forward.y) && boardState[forward.x, forward.y] == OmokStoneColor.None)
        {
            openEnds++;
        }

        Vector2Int backward = origin - direction;
        while (IsInsideBoard(boardState, backward.x, backward.y) && boardState[backward.x, backward.y] == stoneColor)
        {
            backward -= direction;
        }

        if (IsInsideBoard(boardState, backward.x, backward.y) && boardState[backward.x, backward.y] == OmokStoneColor.None)
        {
            openEnds++;
        }

        return openEnds;
    }

    private static bool IsWinningMove(OmokStoneColor[,] boardState, Vector2Int move, OmokStoneColor stoneColor)
    {
        foreach (Vector2Int direction in _directions)
        {
            int totalCount = 1 +
                             CountConnectedStones(boardState, move, direction, stoneColor) +
                             CountConnectedStones(boardState, move, -direction, stoneColor);

            if (totalCount >= 5)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryPlaceVirtualStone(OmokStoneColor[,] boardState, Vector2Int candidate, OmokStoneColor stoneColor)
    {
        if (!IsInsideBoard(boardState, candidate.x, candidate.y) || boardState[candidate.x, candidate.y] != OmokStoneColor.None)
        {
            return false;
        }

        boardState[candidate.x, candidate.y] = stoneColor;
        return true;
    }

    private static bool IsInsideBoard(OmokStoneColor[,] boardState, int x, int y)
    {
        return x >= 0 &&
               x < boardState.GetLength(0) &&
               y >= 0 &&
               y < boardState.GetLength(1);
    }

    private static bool IsEmptyCell(OmokStoneColor[,] boardState, Vector2Int coordinate)
    {
        return IsInsideBoard(boardState, coordinate.x, coordinate.y) &&
               boardState[coordinate.x, coordinate.y] == OmokStoneColor.None;
    }

    private static bool TryGetCellState(
        OmokStoneColor[,] boardState,
        Vector2Int start,
        Vector2Int direction,
        int step,
        out OmokStoneColor cellState)
    {
        Vector2Int coordinate = start + direction * step;
        if (!IsInsideBoard(boardState, coordinate.x, coordinate.y))
        {
            cellState = OmokStoneColor.None;
            return false;
        }

        cellState = boardState[coordinate.x, coordinate.y];
        return true;
    }

    private static void AddBlockMoveIfCandidate(
        OmokStoneColor[,] boardState,
        HashSet<Vector2Int> candidateSet,
        HashSet<Vector2Int> blockMoves,
        Vector2Int start,
        Vector2Int direction,
        int step)
    {
        Vector2Int coordinate = start + direction * step;
        if (IsEmptyCell(boardState, coordinate) && candidateSet.Contains(coordinate))
        {
            blockMoves.Add(coordinate);
        }
    }
}
