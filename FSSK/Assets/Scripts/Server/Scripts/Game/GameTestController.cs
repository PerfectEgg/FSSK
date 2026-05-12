using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game 씬용 임시 테스트 컨트롤러.
/// 승리/패배 버튼 → 본인 점수 ±10 + RPC로 상대에게 반대 결과 전파 → 점수 반영 끝나면 Lobby 이동.
/// 실제 랭킹 계산식이 정해지면 이 스크립트는 제거하고 매치 결과 처리 로직으로 대체.
///
/// ⚠️ 같은 GameObject에 PhotonView 컴포넌트가 반드시 붙어있어야 RPC가 동작함.
/// </summary>
public class GameTestController : MonoBehaviourPun
{
    [Header("Test Buttons")]
    [SerializeField] private Button _winBtn;
    [SerializeField] private Button _loseBtn;

    // 랭킹 계산 로직은 회의에서 결정(임시 ±10)
    private const int WIN_SCORE_DELTA = 10;
    private const int LOSE_SCORE_DELTA = -10;

    void Start()
    {
        if (_winBtn == null || _loseBtn == null)
        {
            Debug.LogError("[GameTestController] Win/Lose buttons not assigned.");
            return;
        }

        _winBtn.onClick.AddListener(OnWinClick);
        _loseBtn.onClick.AddListener(OnLoseClick);
    }

    public void OnWinClick()
    {
        SendOpponentResultRpc(LOSE_SCORE_DELTA);   // 상대는 패배
        ApplyMatchResult(WIN_SCORE_DELTA);
    }

    public void OnLoseClick()
    {
        SendOpponentResultRpc(WIN_SCORE_DELTA);    // 상대는 승리
        ApplyMatchResult(LOSE_SCORE_DELTA);
    }

    // ──────────────────────────────────────────────────────────────
    //  같은 방의 다른 플레이어에게 결과 전파 (방 미참여 시 스킵 = 단독 테스트 가능)
    // ──────────────────────────────────────────────────────────────
    private void SendOpponentResultRpc(int opponentDelta)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.Log("[GameTestController] 방 미참여 상태 — RPC 전송 스킵 (단독 테스트)");
            return;
        }

        photonView.RPC(nameof(ApplyOpponentResultRpc), RpcTarget.Others, opponentDelta);
    }

    [PunRPC]
    private void ApplyOpponentResultRpc(int delta)
    {
        Debug.Log($"[GameTestController] 상대 결과 통보 수신 (delta {delta:+#;-#;0})");
        ApplyMatchResult(delta);
    }

    // ──────────────────────────────────────────────────────────────
    //  본인 점수 갱신 → 성공 시 캐시 동기화 + 방 나가기 + Lobby 이동
    //  AutomaticallySyncScene을 끄면 LeaveRoom 직후 씬 전환해도 SetProperties race 안 남
    // ──────────────────────────────────────────────────────────────
    private void ApplyMatchResult(int delta)
    {
        if (BackendRank.Instance == null)
        {
            Debug.LogError("[GameTestController] BackendRank.Instance is null.");
            return;
        }
        if (BackendManager.Instance == null || BackendManager.Instance.MyUserData == null)
        {
            Debug.LogError("[GameTestController] BackendManager.MyUserData is null.");
            return;
        }

        SetButtonsInteractable(false);

        int currentScore = BackendManager.Instance.MyUserData.score;
        int nextScore = currentScore + delta;

        Debug.Log($"[GameTestController] 점수 갱신 요청 ({currentScore} → {nextScore}, delta {delta:+#;-#;0})");

        BackendRank.Instance.UpdateMyScore(nextScore,
            onSuccess: () =>
            {
                BackendManager.Instance.MyUserData.score = nextScore; // 로컬 캐시 동기화
                Debug.Log($"[GameTestController] 점수 갱신 완료 ({nextScore}점)");

                if (PhotonNetwork.InRoom)
                {
                    PhotonNetwork.AutomaticallySyncScene = false; // 떠나는 클라가 룸 프로퍼티 갱신 못 하게 차단
                    PhotonNetwork.LeaveRoom();
                }

                BackendManager.Instance.LoadLobbyScene();
            },
            onFail: err =>
            {
                Debug.LogError($"[GameTestController] UpdateMyScore failed: {err}");
                SetButtonsInteractable(true);
            });
    }

    private void SetButtonsInteractable(bool value)
    {
        _winBtn.interactable = value;
        _loseBtn.interactable = value;
    }
}
