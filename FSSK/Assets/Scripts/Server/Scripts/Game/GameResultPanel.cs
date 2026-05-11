using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 종료 결과 패널.
/// NetworkGameManager 가 매치 결과를 결정한 뒤 Show() 로 호출하여 텍스트를 채운다.
/// 로비 복귀 버튼은 NetworkGameManager.ReturnToLobby() 를 호출.
/// </summary>
public class GameResultPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI _titleText;       // "게임 승리!" / "게임 패배!" / "게임 무승부!"
    [SerializeField] private TextMeshProUGUI _totalScoreText;  // "총 점수 : 1000점"
    [SerializeField] private TextMeshProUGUI _deltaScoreText;   // "플러스 점수 : -+10점"
    [SerializeField] private Button _returnLobbyBtn;

    private void Awake()
    {
        if (_returnLobbyBtn == null)
        {
            Debug.LogError("[GameResultPanel] _returnLobbyBtn NullReference.");
            return;
        }

        _returnLobbyBtn.onClick.RemoveAllListeners();
        _returnLobbyBtn.onClick.AddListener(OnClickReturnLobby);
    }

    /// <summary>
    /// 매치 결과를 패널에 그리고 활성화한다.
    /// </summary>
    /// <param name="resultType">승/패/무 결과</param>
    /// <param name="currentScore">뒤끝 점수 (반영 전 값)</param>
    /// <param name="scoreDelta">이번 매치 점수 변동 (양수=승, 음수=패, 0=무승부)</param>
    public void Show(GameResultType resultType, int currentScore, int scoreDelta)
    {
        if (_titleText != null)
        {
            _titleText.text = resultType switch
            {
                GameResultType.Win  => "게임 승리!",
                GameResultType.Lose => "게임 패배!",
                _                   => "게임 무승부!"
            };
        }

        if (_totalScoreText != null)
        {
            _totalScoreText.text = $"총 점수 : {currentScore + scoreDelta}점";
        }

        if (_deltaScoreText != null)
        {
            _deltaScoreText.text = $"플러스 점수 : {scoreDelta:+#;-#;0}점";
        }

        gameObject.SetActive(true);
        Debug.Log($"[GameResultPanel] 결과 패널 표시 ({resultType}, total: {currentScore + scoreDelta}, delta: {scoreDelta:+#;-#;0})");
    }

    private void OnClickReturnLobby()
    {
        Debug.Log("[GameResultPanel] 로비로 돌아가기 버튼 클릭");

        if (NetworkGameManager.Instance == null)
        {
            Debug.LogError("[GameResultPanel] NetworkGameManager.Instance is null on lobby return.");
            return;
        }

        _returnLobbyBtn.interactable = false;
        NetworkGameManager.Instance.ReturnToLobby();
    }
}

public enum GameResultType
{
    Draw,
    Win,
    Lose
}
