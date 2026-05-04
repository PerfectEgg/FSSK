using BackEnd.Leaderboard;
using TMPro;
using UnityEngine;

/// <summary>
/// 랭킹 리스트의 한 행 UI. RankingUIManager가 프리팹을 복제해 사용.
/// </summary>
public class RankItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private TextMeshProUGUI _scoreText;

    public void Bind(UserLeaderboardItem item)
    {
        if (_rankText != null) _rankText.text = item.rank + "위";
        if (_nicknameText != null) _nicknameText.text = item.nickname;
        if (_scoreText != null) _scoreText.text = item.score.ToString();
    }
}
