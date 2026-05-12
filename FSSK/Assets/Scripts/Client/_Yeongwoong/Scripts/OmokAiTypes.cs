using UnityEngine;

public enum OmokAiType
{
    Easy,
    Normal
}

public interface IOmokAi
{
    bool TryChooseMove(OmokStoneColor[,] boardState, OmokStoneColor aiColor, out Vector2Int move);
}

internal readonly struct OmokScoredMove
{
    public OmokScoredMove(Vector2Int coordinate, int score)
    {
        Coordinate = coordinate;
        Score = score;
    }

    public Vector2Int Coordinate { get; }
    public int Score { get; }
}
