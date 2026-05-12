using System;

[Serializable]
public class UserData
{
    public int score = 1000; // 랭킹 점수
    public int winCount = 0; // 이긴 횟수
    public int loseCount = 0; // 진 횟수

    public override string ToString()
    {
        return $"[UserData] 점수: {score} | 승: {winCount} | 패: {loseCount}";
    }
}
