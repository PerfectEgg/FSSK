using System.Collections.Generic;
using UnityEngine;

public enum OmokAiType
{
    Easy
}

public interface IOmokAi
{
    bool TryChooseMove(OmokStoneColor[,] boardState, OmokStoneColor aiColor, out Vector2Int move);
}

public sealed class OmokEasyAi : IOmokAi
{
    private static readonly Vector2Int[] Directions =
    {
        new(1, 0),
        new(0, 1),
        new(1, 1),
        new(1, -1)
    };

    public bool TryChooseMove(OmokStoneColor[,] boardState, OmokStoneColor aiColor, out Vector2Int move)
    {
        move = default;

        if (boardState == null || aiColor == OmokStoneColor.None)
        {
            return false;
        }

        // 연산량을 줄이기 위해 이미 놓인 돌 주변 칸만 후보로 본다.
        List<Vector2Int> candidates = CollectCandidateMoves(boardState);
        if (candidates.Count == 0)
        {
            return false;
        }

        // 지금 바로 이길 수 있는 수가 있으면 바로 둔다.
        if (TryFindImmediateWinningMove(boardState, aiColor, candidates, out move))
        {
            return true;
        }

        OmokStoneColor opponentColor = GetOppositeColor(aiColor);

        // 상대가 다음 턴에 바로 이길 수 있으면 우선 그 자리를 막는다.
        if (TryFindImmediateWinningMove(boardState, opponentColor, candidates, out move))
        {
            return true;
        }

        // 상대가 직선 열린 3을 만들었으면 끝자리 중 하나를 우선 막는다.
        if (TryFindOpenThreeBlockMove(boardState, aiColor, opponentColor, candidates, out move))
        {
            return true;
        }

        // 그 외에는 후보 점수를 계산한 뒤 상위권 중 하나를 고른다.
        return TryChooseScoredMove(boardState, aiColor, opponentColor, candidates, out move);
    }

    private static bool TryChooseScoredMove(
        OmokStoneColor[,] boardState,
        OmokStoneColor aiColor,
        OmokStoneColor opponentColor,
        List<Vector2Int> candidates,
        out Vector2Int move)
    {
        move = default;

        List<ScoredMove> scoredMoves = new(candidates.Count);
        foreach (Vector2Int candidate in candidates)
        {
            int score = EvaluateCandidate(boardState, candidate, aiColor, opponentColor);
            scoredMoves.Add(new ScoredMove(candidate, score));
        }

        if (scoredMoves.Count == 0)
        {
            return false;
        }

        scoredMoves.Sort((left, right) => right.Score.CompareTo(left.Score));

        // 항상 똑같이 두지 않도록 최고점 몇 개 중에서 랜덤 선택한다.
        int poolSize = Mathf.Min(3, scoredMoves.Count);
        int chosenIndex = Random.Range(0, poolSize);
        move = scoredMoves[chosenIndex].Coordinate;
        return true;
    }

    private static bool TryFindImmediateWinningMove(
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

    private static bool TryFindOpenThreeBlockMove(
        OmokStoneColor[,] boardState,
        OmokStoneColor aiColor,
        OmokStoneColor opponentColor,
        List<Vector2Int> candidates,
        out Vector2Int move)
    {
        move = default;
        HashSet<Vector2Int> candidateSet = new(candidates);
        HashSet<Vector2Int> blockMoveSet = new(FindOpenThreeBlockMoves(boardState, opponentColor, candidateSet));
        foreach (Vector2Int brokenThreeBlock in FindBrokenThreeBlockMoves(boardState, opponentColor, candidateSet))
        {
            blockMoveSet.Add(brokenThreeBlock);
        }

        if (blockMoveSet.Count == 0)
        {
            return false;
        }

        List<Vector2Int> blockMoves = new(blockMoveSet);
        bool foundBlockingMove = false;
        int bestScore = int.MinValue;
        foreach (Vector2Int candidate in blockMoves)
        {
            int score = EvaluateCandidate(boardState, candidate, aiColor, opponentColor);
            if (!foundBlockingMove || score > bestScore)
            {
                foundBlockingMove = true;
                bestScore = score;
                move = candidate;
            }
        }

        return foundBlockingMove;
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
                foreach (Vector2Int direction in Directions)
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
                foreach (Vector2Int direction in Directions)
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

    private static int EvaluateCandidate(
        OmokStoneColor[,] boardState,
        Vector2Int candidate,
        OmokStoneColor aiColor,
        OmokStoneColor opponentColor)
    {
        int size = boardState.GetLength(0);
        int center = size / 2;
        int score = (size - Mathf.Abs(candidate.x - center) - Mathf.Abs(candidate.y - center)) * 4;

        // 내 줄을 늘리는 수를 더 높게 보되, 상대 위협도 같이 반영한다.
        score += EvaluateColorPatterns(boardState, candidate, aiColor) * 2;
        score += EvaluateColorPatterns(boardState, candidate, opponentColor);
        score += CountNeighborStones(boardState, candidate, aiColor) * 15;
        score += CountNeighborStones(boardState, candidate, opponentColor) * 10;

        return score;
    }

    private static int EvaluateColorPatterns(OmokStoneColor[,] boardState, Vector2Int candidate, OmokStoneColor stoneColor)
    {
        if (!TryPlaceVirtualStone(boardState, candidate, stoneColor))
        {
            return int.MinValue / 4;
        }

        int score = 0;

        // 가로, 세로, 대각선 각 방향 점수를 따로 계산해서 합산한다.
        foreach (Vector2Int direction in Directions)
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

    private static int CountNeighborStones(OmokStoneColor[,] boardState, Vector2Int candidate, OmokStoneColor targetColor)
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

    private static int ScoreLine(int totalCount, int openEnds)
    {
        // 연결 수가 많고 양끝이 열려 있을수록 다음 수까지 이어질 가능성이 크다.
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
        foreach (Vector2Int direction in Directions)
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

    private static List<Vector2Int> CollectCandidateMoves(OmokStoneColor[,] boardState)
    {
        int size = boardState.GetLength(0);
        HashSet<Vector2Int> candidates = new();
        bool hasStone = false;

        // 기존 돌과 붙어 있는 빈칸만 후보로 사용한다.
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
            // 첫 수라면 중앙부터 두도록 한다.
            int center = size / 2;
            candidates.Add(new Vector2Int(center, center));
        }

        return new List<Vector2Int>(candidates);
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

    private static OmokStoneColor GetOppositeColor(OmokStoneColor stoneColor)
    {
        return stoneColor switch
        {
            OmokStoneColor.Black => OmokStoneColor.White,
            OmokStoneColor.White => OmokStoneColor.Black,
            _ => OmokStoneColor.None
        };
    }

    private readonly struct ScoredMove
    {
        public ScoredMove(Vector2Int coordinate, int score)
        {
            Coordinate = coordinate;
            Score = score;
        }

        public Vector2Int Coordinate { get; }
        public int Score { get; }
    }
}
