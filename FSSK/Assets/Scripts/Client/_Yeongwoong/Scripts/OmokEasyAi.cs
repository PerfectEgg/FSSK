using System.Collections.Generic;
using UnityEngine;

public sealed class OmokEasyAi : IOmokAi
{
    private const float LOOSE_THREE_BLOCK_CHANCE = 0.7f;
    private const float RANDOM_QUIET_MOVE_CHANCE = 0.18f;
    private const int QUIET_MOVE_POOL_SIZE = 8;

    public bool TryChooseMove(OmokStoneColor[,] boardState, OmokStoneColor aiColor, out Vector2Int move)
    {
        move = default;

        if (boardState == null || aiColor == OmokStoneColor.None)
        {
            return false;
        }

        List<Vector2Int> candidates = OmokAiLogic.CollectCandidateMoves(boardState);
        if (candidates.Count == 0)
        {
            return false;
        }

        if (OmokAiLogic.TryFindImmediateWinningMove(boardState, aiColor, candidates, out move))
        {
            return true;
        }

        OmokStoneColor opponentColor = OmokAiLogic.GetOppositeColor(aiColor);
        if (OmokAiLogic.TryFindImmediateWinningMove(boardState, opponentColor, candidates, out move))
        {
            return true;
        }

        if (TryChooseLooseThreeBlockMove(boardState, aiColor, opponentColor, candidates, out move))
        {
            return true;
        }

        return TryChooseEasyScoredMove(boardState, aiColor, opponentColor, candidates, out move);
    }

    private static bool TryChooseLooseThreeBlockMove(
        OmokStoneColor[,] boardState,
        OmokStoneColor aiColor,
        OmokStoneColor opponentColor,
        List<Vector2Int> candidates,
        out Vector2Int move)
    {
        move = default;

        List<Vector2Int> blockMoves = OmokAiLogic.CollectThreeBlockMoves(boardState, opponentColor, candidates);
        if (blockMoves.Count == 0)
        {
            return false;
        }

        List<OmokScoredMove> scoredMoves = new(blockMoves.Count);
        foreach (Vector2Int candidate in blockMoves)
        {
            int score = OmokAiLogic.EvaluateCandidate(boardState, candidate, aiColor, opponentColor);
            scoredMoves.Add(new OmokScoredMove(candidate, score));
        }

        scoredMoves.Sort((left, right) => right.Score.CompareTo(left.Score));

        int chosenIndex;
        if (scoredMoves.Count == 1 || Random.value >= LOOSE_THREE_BLOCK_CHANCE)
        {
            int strongPoolSize = Mathf.Min(2, scoredMoves.Count);
            chosenIndex = Random.Range(0, strongPoolSize);
        }
        else
        {
            int looseStartIndex = Mathf.Min(scoredMoves.Count - 1, Mathf.Max(1, scoredMoves.Count / 3));
            chosenIndex = Random.Range(looseStartIndex, scoredMoves.Count);
        }

        move = scoredMoves[chosenIndex].Coordinate;
        return true;
    }

    private static bool TryChooseEasyScoredMove(
        OmokStoneColor[,] boardState,
        OmokStoneColor aiColor,
        OmokStoneColor opponentColor,
        List<Vector2Int> candidates,
        out Vector2Int move)
    {
        move = default;

        if (candidates.Count == 0)
        {
            return false;
        }

        if (Random.value < RANDOM_QUIET_MOVE_CHANCE)
        {
            move = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        List<OmokScoredMove> scoredMoves = new(candidates.Count);
        foreach (Vector2Int candidate in candidates)
        {
            int score = EvaluateEasyCandidate(boardState, candidate, aiColor, opponentColor);
            scoredMoves.Add(new OmokScoredMove(candidate, score));
        }

        scoredMoves.Sort((left, right) => right.Score.CompareTo(left.Score));

        int poolSize = Mathf.Min(QUIET_MOVE_POOL_SIZE, scoredMoves.Count);
        int chosenIndex = Random.Range(0, poolSize);
        move = scoredMoves[chosenIndex].Coordinate;
        return true;
    }

    private static int EvaluateEasyCandidate(
        OmokStoneColor[,] boardState,
        Vector2Int candidate,
        OmokStoneColor aiColor,
        OmokStoneColor opponentColor)
    {
        int size = boardState.GetLength(0);
        int center = size / 2;
        int score = (size - Mathf.Abs(candidate.x - center) - Mathf.Abs(candidate.y - center)) * 3;

        score += OmokAiLogic.CountNeighborStones(boardState, candidate, aiColor) * 35;
        score += OmokAiLogic.CountNeighborStones(boardState, candidate, opponentColor) * 28;
        score += OmokAiLogic.GetLongestLineLengthAfterMove(boardState, candidate, aiColor) * 80;
        score += OmokAiLogic.GetLongestLineLengthAfterMove(boardState, candidate, opponentColor) * 55;
        score += Random.Range(0, 18);

        return score;
    }
}
