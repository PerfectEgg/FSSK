using BackEnd.Leaderboard;
using TMPro;
using UnityEngine;

/// <summary>
/// Top3 현상수배지 슬롯. RankingPanel(RankingUIManager)가 인스펙터에 미리 박아둔 3개를 직접 바인딩.
/// </summary>
public class TopPlayerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private GameObject _emptyOverlay; // 등록자 < 3명일 때 보일 회색 placeholder (선택)
    
    //[SerializeField] private TextMeshProUGUI _bountyText; // 현상수배 금액 사용?

    public void Bind(UserLeaderboardItem item)
    {
        if (_nicknameText != null) _nicknameText.text = item.nickname;
        if (_scoreText != null) _scoreText.text = item.score.ToString();
        if (_emptyOverlay != null) _emptyOverlay.SetActive(false);
    }

    public void SetEmpty()
    {
        if (_nicknameText != null) _nicknameText.text = "-";
        if (_scoreText != null) _scoreText.text = "-";
        if (_emptyOverlay != null) _emptyOverlay.SetActive(true);
    }
}
