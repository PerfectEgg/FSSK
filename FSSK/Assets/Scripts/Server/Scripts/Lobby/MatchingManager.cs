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
    [SerializeField] private string _gameSceneName = "GameTest";

    [Header("UI")]
    [SerializeField] private Button _mutiMatchButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _timerText;

    private float _matchingTime;
    private bool _isMatching;

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true; // 마스터 씬 이동시 다른 인원도 같이 이동하는 기능

        // 이미 로비에 있으면 (랭킹 갔다 돌아온 경우) 즉시 활성화
        if (PhotonNetwork.InLobby)
        {
            _mutiMatchButton.interactable = true;
            SetStatus("매칭 버튼을 눌러 시작하세요");
            return;
        }

        // 마스터 연결이 되어잇는데 로비 미입장인 경우 로비 입장만
        if (PhotonNetwork.IsConnectedAndReady)
        {
            _mutiMatchButton.interactable = false;
            SetStatus("로비 입장 중...");
            PhotonNetwork.JoinLobby();
            return;
        }

        // 로그인 직후 -> 마스터 연결
        _mutiMatchButton.interactable = false;
        SetStatus("서버 연결 중...");

        Debug.Log("[MatchingManager] Photon 마스터 서버 연결 시도");
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
        Debug.Log("[MatchingManager] 랜덤 매칭 시작...");

        _mutiMatchButton.interactable = false;
        _isMatching = true;
        _matchingTime = 0f;
        _timerText.text = "00:00";
        SetStatus("매칭 중...");

        PhotonNetwork.JoinRandomRoom();
    }

    // 랜덤 매칭 실패 (입장 가능한 방 없음) → 새 방 생성 (이름 null = Photon 자동 생성)
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"[MatchingManager] 랜덤 매칭 실패 - 새 방 생성: {message}");

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
        Debug.Log($"[MatchingManager] 마스터 서버 연결 완료 - 로비 입장 Region: '{PhotonNetwork.CloudRegion}', AppVersion: '{PhotonNetwork.AppVersion}'");
        PhotonNetwork.JoinLobby();
    }

    // 매칭 버튼 활성화
    public override void OnJoinedLobby()
    {
        Debug.Log("[MatchingManager] 로비 입장 완료");
        _mutiMatchButton.interactable = true;
        SetStatus("매칭 버튼을 눌러 시작하세요");
    }

    // 로비 입장 및 인원 수 체크
    public override void OnJoinedRoom()
    {
        Debug.Log($"[MatchingManager] 플레이어 인원 수 : ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
        SetStatus($"대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
        CheckStartGame();
    }

    // 다른 플레이어 입장
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[MatchingManager] 플레이어 입장: '{newPlayer.NickName}' ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
        SetStatus($"대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
        CheckStartGame();
    }

    // 다른 플레이어 퇴장
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[MatchingManager] 플레이어 퇴장: '{otherPlayer.NickName}' ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
        SetStatus($"대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{_maxPlayers})");
    }

    // 방 생성 실패 (드물게 발생 — 네트워크 이슈 등) → 매칭 상태 해제
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[MatchingManager] CreateRoom failed (code: {returnCode}): {message}");
        _isMatching = false;
        _mutiMatchButton.interactable = true;
        SetStatus("방 생성 실패. 다시 시도해주세요.");
    }

    // 연결 끊김
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[MatchingManager] Disconnected (cause: {cause}) — reconnecting.");
        _isMatching = false;
        _mutiMatchButton.interactable = false;
        SetStatus("연결 끊김. 재연결 중...");
        PhotonNetwork.ConnectUsingSettings();
    }

    // 인원이 모이면 게임 씬으로 전환 (마스터 클라이언트만)
    private void CheckStartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < _maxPlayers) return;
        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[MatchingManager] _gameSceneName is empty.");
            return;
        }
        // Build Settings에 등록되어있지 않은 경우
        if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
        {
            Debug.LogError($"[MatchingManager] Scene '{_gameSceneName}' is not in Build Settings.");
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = false;
        Debug.Log($"[MatchingManager] 인원 충족 — 게임 씬 이동: '{_gameSceneName}'");
        PhotonNetwork.LoadLevel(_gameSceneName);
    }

    private void SetStatus(string msg)
    {
        if (_statusText != null)
            _statusText.text = msg;
    }
}
