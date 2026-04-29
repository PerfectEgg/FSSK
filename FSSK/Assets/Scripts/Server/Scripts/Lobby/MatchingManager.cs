using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Photon 매칭 매니저. 마스터 서버 연결 → 로비 입장 → 매칭 버튼 → 방 입장/생성 → 인원 충족 시 게임 씬 로드.
/// </summary>
public class MatchingManager : MonoBehaviourPunCallbacks
{
    [Header("Room Settings")]
    [SerializeField] private byte _maxPlayers = 2;
    [SerializeField] private string _gameSceneName = "Game";

    [Header("UI")]
    [SerializeField] private Button _matchButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _timerText;

    private float _matchingTime;
    private bool _isMatching;

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        _matchButton.interactable = false;
        SetStatus("서버 연결 중...");

        PhotonNetwork.ConnectUsingSettings();
    }

    void Update()
    {
        if (!_isMatching) return;

        _matchingTime += Time.deltaTime;
        int min = (int)(_matchingTime / 60);
        int sec = (int)(_matchingTime % 60);
        _timerText.text = $"{min:00}:{sec:00}";
    }

    // 매칭 버튼 클릭 → 랜덤 매칭 시도 (실패 시 OnJoinRandomFailed 에서 새 방 생성)
    public void OnClickMatchButton()
    {
        _matchButton.interactable = false;
        _isMatching = true;
        _matchingTime = 0f;
        _timerText.text = "00:00";
        SetStatus("매칭 중...");

        PhotonNetwork.JoinRandomRoom();
    }

    // 랜덤 매칭 실패 (입장 가능한 방 없음) → 새 방 생성 (이름 null = Photon 자동 생성)
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"랜덤 매칭 실패 - 새 방 생성: {message}");

        RoomOptions options = new()
        {
            MaxPlayers = _maxPlayers,
            IsOpen = true,
            IsVisible = true
        };
        PhotonNetwork.CreateRoom(null, options, TypedLobby.Default);
    }

    // 대기실 입장
    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    // 매칭 버튼 활성화
    public override void OnJoinedLobby()
    {
        _matchButton.interactable = true;
        SetStatus("매칭 버튼을 눌러 시작하세요");
    }

    // 로비 입장 및 인원 수 체크
    public override void OnJoinedRoom()
    {
        SetStatus($"대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
        CheckStartGame();
    }

    // 다른 플레이어 입장
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SetStatus($"대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
        CheckStartGame();
    }

    // 다른 플레이어 퇴장
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SetStatus($"대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
    }

    // 방 생성 실패 (드물게 발생 — 네트워크 이슈 등) → 매칭 상태 해제
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"방 생성 실패: {message}");
        _isMatching = false;
        _matchButton.interactable = true;
        SetStatus("방 생성 실패. 다시 시도해주세요.");
    }

    // 연결 끊김
    public override void OnDisconnected(DisconnectCause cause)
    {
        _isMatching = false;
        _matchButton.interactable = false;
        SetStatus("연결 끊김. 재연결 중...");
        PhotonNetwork.ConnectUsingSettings();
    }

    // 인원이 모이면 게임 씬으로 전환 (마스터 클라이언트만)
    private void CheckStartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < _maxPlayers) return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.LoadLevel(_gameSceneName);
    }

    private void SetStatus(string msg)
    {
        if (_statusText != null)
            _statusText.text = msg;
    }
}
