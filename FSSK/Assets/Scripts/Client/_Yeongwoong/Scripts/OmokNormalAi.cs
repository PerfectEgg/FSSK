using System.Collections.Generic;
using UnityEngine;

public sealed class OmokNormalAi : IOmokAi
{
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

        if (TryFindBestThreeBlockMove(boardState, aiColor, opponentColor, candidates, out move))
        {
            return true;
        }

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

        List<OmokScoredMove> scoredMoves = new(candidates.Count);
        foreach (Vector2Int candidate in candidates)
        {
            int score = OmokAiLogic.EvaluateCandidate(boardState, candidate, aiColor, opponentColor);
            scoredMoves.Add(new OmokScoredMove(candidate, score));
        }

        if (scoredMoves.Count == 0)
        {
            return false;
        }

        scoredMoves.Sort((left, right) => right.Score.CompareTo(left.Score));

        int poolSize = Mathf.Min(3, scoredMoves.Count);
        int chosenIndex = Random.Range(0, poolSize);
        move = scoredMoves[chosenIndex].Coordinate;
        return true;
    }

    private static bool TryFindBestThreeBlockMove(
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

        bool foundBlockingMove = false;
        int bestScore = int.MinValue;
        foreach (Vector2Int candidate in blockMoves)
        {
            int score = OmokAiLogic.EvaluateCandidate(boardState, candidate, aiColor, opponentColor);
            if (!foundBlockingMove || score > bestScore)
            {
                foundBlockingMove = true;
                bestScore = score;
                move = candidate;
            }
        }

        return foundBlockingMove;
    }
}
