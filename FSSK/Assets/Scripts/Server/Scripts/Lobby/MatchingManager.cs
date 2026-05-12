using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Photon 매칭 매니저. 마스터 서버 연결 → 로비 입장 → 매칭 버튼 → 방 입장/생성 → 인원 충족 시 게임 씬 로드.
/// </summary>
public class MatchingManager : MonoBehaviourPunCallbacks
{
    [Header("Room Settings")]
    [SerializeField] private byte _maxPlayers = 2;
    [SerializeField] private string _gameSceneName = "GameTest";
    [SerializeField] private string _soloSceneName = "Game";
    [SerializeField] private OmokAiType _soloAiType = OmokAiType.Easy;

    [Header("UI")]
    [SerializeField] private Button _mutiMatchButton;
    [SerializeField] private Button _soloPlayButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _timerText;

    private const string SOLO_BUTTON_NAME = "SoloPlayBtn";
    private const string CANCEL_LABEL = "취소";

    private float _matchingTime;
    private bool _isMatching;
    private bool _isStartingSolo;

    private TextMeshProUGUI _matchButtonLabel;
    private string _defaultMatchLabel;

    void Start()
    {
        // 매칭 버튼 텍스트 캐싱 + 기본 라벨 저장 (취소 후 복원용)
        if (_mutiMatchButton != null)
        {
            _matchButtonLabel = _mutiMatchButton.GetComponentInChildren<TextMeshProUGUI>();
            _defaultMatchLabel = _matchButtonLabel != null ? _matchButtonLabel.text : "매칭";
        }

        WireSoloButton();

        if (PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = false;
        }

        PhotonNetwork.AutomaticallySyncScene = true; // 마스터 씬 이동시 다른 인원도 같이 이동하는 기능

        // 이전 매칭 방에 남아있는 상태로 복귀한 경우 — 방 정리 후 OnLeftRoom 에서 로비 재입장
        if (PhotonNetwork.InRoom)
        {
            _mutiMatchButton.interactable = false;
            SetStatus("이전 방 정리 중...");
            PhotonNetwork.LeaveRoom();
            return;
        }

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

    public override void OnLeftRoom()
    {
        if (_isStartingSolo)
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
                return;
            }

            StartOfflineSoloRoom();
            return;
        }

        // 매칭 취소 또는 이전 방 정리로 떠난 경우 — 로비 미입장 상태면 다시 입장 시도
        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
        {
            SetStatus("로비 입장 중...");
            PhotonNetwork.JoinLobby();
        }
    }

    void Update()
    {
        if (!_isMatching) return;

        _matchingTime += Time.deltaTime;
        int min = (int)(_matchingTime / 60);
        int sec = (int)(_matchingTime % 60);
        _timerText.text = $"{min:00}:{sec:00}";
    }

    // 매칭 버튼 클릭 → 매칭 중이면 취소, 아니면 랜덤 매칭 시도
    public void OnClickMatchButton()
    {
        if (_isMatching)
        {
            CancelMatching();
            return;
        }

        _isStartingSolo = false;

        Debug.Log("[MatchingManager] 랜덤 매칭 시작...");

        _isMatching = true;
        _matchingTime = 0f;
        _timerText.text = "00:00";
        SetMatchButtonLabel(CANCEL_LABEL);
        SetStatus("매칭 중...");

        PhotonNetwork.JoinRandomRoom();
    }

    // 매칭 취소 — InRoom 이면 LeaveRoom, 콜백 대기 중이면 _isMatching=false 가드로 후속 처리 차단
    private void CancelMatching()
    {
        Debug.Log("[MatchingManager] 매칭 취소");

        _isMatching = false;
        _matchingTime = 0f;
        if (_timerText != null) _timerText.text = "00:00";
        SetMatchButtonLabel(_defaultMatchLabel);
        SetStatus("매칭 취소됨");

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    private void SetMatchButtonLabel(string label)
    {
        if (_matchButtonLabel != null) _matchButtonLabel.text = label;
    }

    public void OnClickSoloPlayButton()
    {
        if (!CanLoadScene(_soloSceneName))
        {
            return;
        }

        SoloPlaySettings.SetAiType(_soloAiType);
        Debug.Log($"[MatchingManager] Starting solo play: '{_soloSceneName}'");

        _isStartingSolo = true;
        _isMatching = false;
        if (_timerText != null) _timerText.text = "00:00";
        SetButtonInteractable(_mutiMatchButton, false);
        SetButtonInteractable(_soloPlayButton, false);
        SetStatus("Starting solo play...");

        PhotonNetwork.AutomaticallySyncScene = false;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            return;
        }

        StartOfflineSoloRoom();
    }

    public void SetSoloAiEasy()
    {
        SetSoloAiType(OmokAiType.Easy);
    }

    public void SetSoloAiNormal()
    {
        SetSoloAiType(OmokAiType.Normal);
    }

    public void SetSoloAiType(OmokAiType aiType)
    {
        _soloAiType = aiType;
        SoloPlaySettings.SetAiType(aiType);
        SetStatus($"Solo AI: {aiType}");
    }

    // 랜덤 매칭 실패 (입장 가능한 방 없음) → 새 방 생성 (이름 null = Photon 자동 생성)
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (!_isMatching)
        {
            Debug.Log($"[MatchingManager] 취소된 매칭의 OnJoinRandomFailed 수신 — 무시 ({message})");
            return;
        }

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
        if (_isStartingSolo && PhotonNetwork.OfflineMode)
        {
            return;
        }

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
        if (_isStartingSolo && PhotonNetwork.OfflineMode)
        {
            LoadSoloScene();
            return;
        }

        if (!_isMatching)
        {
            Debug.Log("[MatchingManager] 매칭 취소 후 방 입장 콜백 도착 — 즉시 퇴장");
            PhotonNetwork.LeaveRoom();
            return;
        }

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
        SetMatchButtonLabel(_defaultMatchLabel);
        SetStatus("방 생성 실패. 다시 시도해주세요.");
    }

    // 연결 끊김
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (_isStartingSolo)
        {
            StartOfflineSoloRoom();
            return;
        }

        Debug.LogWarning($"[MatchingManager] Disconnected (cause: {cause}) — reconnecting.");
        _isMatching = false;
        _mutiMatchButton.interactable = false;
        SetMatchButtonLabel(_defaultMatchLabel);
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

    private void WireSoloButton()
    {
        if (_soloPlayButton == null)
        {
            GameObject soloButtonObject = GameObject.Find(SOLO_BUTTON_NAME);
            if (soloButtonObject != null)
            {
                _soloPlayButton = soloButtonObject.GetComponent<Button>();
            }
        }

        if (_soloPlayButton == null)
        {
            Debug.LogWarning("[MatchingManager] Solo play button is not assigned.");
            return;
        }

        _soloPlayButton.onClick.RemoveListener(OnClickSoloPlayButton);
        _soloPlayButton.onClick.AddListener(OnClickSoloPlayButton);
    }

    private void StartOfflineSoloRoom()
    {
        if (!CanLoadScene(_soloSceneName))
        {
            ResetSoloStartState();
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            return;
        }

        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.OfflineMode = true;

        RoomOptions options = new()
        {
            MaxPlayers = 1,
            IsOpen = false,
            IsVisible = false
        };

        if (!PhotonNetwork.CreateRoom("SoloPlay", options, TypedLobby.Default))
        {
            Debug.LogError("[MatchingManager] Failed to create offline solo room.");
            ResetSoloStartState();
        }
    }

    private void LoadSoloScene()
    {
        if (!CanLoadScene(_soloSceneName))
        {
            ResetSoloStartState();
            return;
        }

        Debug.Log($"[MatchingManager] Loading solo scene: '{_soloSceneName}'");
        SceneManager.LoadScene(_soloSceneName);
    }

    private bool CanLoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[MatchingManager] Solo scene name is empty.");
            SetStatus("Solo scene is not set.");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[MatchingManager] Scene '{sceneName}' is not in Build Settings.");
            SetStatus("Solo scene is not in Build Settings.");
            return false;
        }

        return true;
    }

    private void ResetSoloStartState()
    {
        _isStartingSolo = false;
        SetButtonInteractable(_mutiMatchButton, true);
        SetButtonInteractable(_soloPlayButton, true);
    }

    private static void SetButtonInteractable(Button button, bool value)
    {
        if (button != null)
        {
            button.interactable = value;
        }
    }
}

public static class SoloPlaySettings
{
    public static OmokAiType SelectedAiType { get; private set; } = OmokAiType.Easy;

    public static void SetAiType(OmokAiType aiType)
    {
        SelectedAiType = aiType;
    }
}
