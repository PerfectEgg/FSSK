using BackEnd.Leaderboard;
using TMPro;
using UnityEngine;

/// <summary>
/// 랭킹 리스트의 한 행 UI. RankingPanel(RankingUIManager)가 프리팹을 복제해 사용.
/// </summary>
public class RankItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private TextMeshProUGUI _scoreText;

    // 기본 정렬
    public void Bind(UserLeaderboardItem item)
    {
        if (_rankText != null) _rankText.text = item.rank + "위";
        if (_nicknameText != null) _nicknameText.text = item.nickname;
        if (_scoreText != null) _scoreText.text = item.score.ToString();
    }

    // 클라 후처리 정렬(score 동률 시 winCount 재정렬) 시 표시 순서대로 rank 부여하기 위한 오버로드
    public void Bind(int displayRank, UserLeaderboardItem item)
    {
        if (_rankText != null) _rankText.text = displayRank + "위";
        if (_nicknameText != null) _nicknameText.text = item.nickname;
        if (_scoreText != null) _scoreText.text = item.score.ToString();
    }
}
