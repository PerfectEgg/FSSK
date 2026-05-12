using System;
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
    private const string DefaultTotalScoreFormat = "총 점수 : {0}점";
    private const string DefaultDeltaScoreFormat = "점수 변화 : {0:+#;-#;0}점";
    private const string DefaultTimeoutDeltaScoreFormat = "시간초과 패널티 : {0:+#;-#;0}점";

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _totalScoreText;
    [SerializeField] private TextMeshProUGUI _deltaScoreText;
    [SerializeField] private Button _returnLobbyBtn;

    [Header("Result Texts")]
    [SerializeField] private ResultTextSet _winResultText =
        new("게임 승리!", DefaultTotalScoreFormat, DefaultDeltaScoreFormat);
    [SerializeField] private ResultTextSet _loseResultText =
        new("게임 패배!", DefaultTotalScoreFormat, DefaultDeltaScoreFormat);
    [SerializeField] private ResultTextSet _drawResultText =
        new("게임 무승부!", DefaultTotalScoreFormat, DefaultDeltaScoreFormat);
    [SerializeField] private ResultTextSet _timeoutResultText =
        new("시간 초과!", DefaultTotalScoreFormat, DefaultTimeoutDeltaScoreFormat);

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
        ResultTextSet resultText = GetResultTextSet(resultType);

        if (_titleText != null)
        {
            _titleText.text = GetTitleMessage(resultType, resultText);
        }

        if (_totalScoreText != null)
        {
            _totalScoreText.text = FormatScoreText(resultText?.TotalScoreFormat, currentScore + scoreDelta, DefaultTotalScoreFormat);
        }

        if (_deltaScoreText != null)
        {
            _deltaScoreText.text = FormatScoreText(resultText?.DeltaScoreFormat, scoreDelta, GetDefaultDeltaScoreFormat(resultType));
        }

        gameObject.SetActive(true);
        Debug.Log($"[GameResultPanel] 결과 패널 표시 ({resultType}, total: {currentScore + scoreDelta}, delta: {scoreDelta:+#;-#;0})");
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private ResultTextSet GetResultTextSet(GameResultType resultType)
    {
        return resultType switch
        {
            GameResultType.Win     => _winResultText,
            GameResultType.Lose    => _loseResultText,
            GameResultType.Timeout => _timeoutResultText,
            _                      => _drawResultText
        };
    }

    private static string GetTitleMessage(GameResultType resultType, ResultTextSet resultText)
    {
        string message = resultText?.TitleMessage;
        return string.IsNullOrWhiteSpace(message) ? GetDefaultTitleMessage(resultType) : message;
    }

    private static string GetDefaultTitleMessage(GameResultType resultType)
    {
        return resultType switch
        {
            GameResultType.Win     => "게임 승리!",
            GameResultType.Lose    => "게임 패배!",
            GameResultType.Timeout => "시간 초과!",
            _                      => "게임 무승부!"
        };
    }

    private static string GetDefaultDeltaScoreFormat(GameResultType resultType)
    {
        return resultType == GameResultType.Timeout ? DefaultTimeoutDeltaScoreFormat : DefaultDeltaScoreFormat;
    }

    private static string FormatScoreText(string format, int value, string fallbackFormat)
    {
        string resolvedFormat = string.IsNullOrWhiteSpace(format) ? fallbackFormat : format;

        try
        {
            return string.Format(resolvedFormat, value);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[GameResultPanel] Invalid score text format: {resolvedFormat}");
            return string.Format(fallbackFormat, value);
        }
    }

    [Serializable]
    private sealed class ResultTextSet
    {
        [SerializeField] private string _titleMessage;
        [Tooltip("{0} = 최종 총점")]
        [SerializeField] private string _totalScoreFormat;
        [Tooltip("{0} = 점수 변화량")]
        [SerializeField] private string _deltaScoreFormat;

        public ResultTextSet(string titleMessage, string totalScoreFormat, string deltaScoreFormat)
        {
            _titleMessage = titleMessage;
            _totalScoreFormat = totalScoreFormat;
            _deltaScoreFormat = deltaScoreFormat;
        }

        public string TitleMessage => _titleMessage;
        public string TotalScoreFormat => _totalScoreFormat;
        public string DeltaScoreFormat => _deltaScoreFormat;
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
    Lose,
    Timeout
}
