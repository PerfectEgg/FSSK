using System;
using BackEnd;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로그인 씬 UI 연결 매니저.
/// 로그인/회원가입
///
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    // ── 로그인 패널 ──────────────────────────────────────────────
    [Header("Login Panel")]
    [SerializeField] private GameObject _loginPanel;
    [SerializeField] private TMP_InputField _idInput;
    [SerializeField] private TMP_InputField _pwInput;
    [SerializeField] private Button _loginBtn;
    [SerializeField] private Button _registerBtn;
    [SerializeField] private TextMeshProUGUI _loginStatusText;

    [Header("Scene Names")]
    [SerializeField] private string _registerSceneName = "Register";

    // ─────────────────────────────────────────────────────────────

    void Start()
    {
        ShowloginPanel();

        _loginBtn.onClick.AddListener(OnLoginClick);
        _registerBtn.onClick.AddListener(OnRegisterClick);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            FocusNextInput();

        if (Input.GetKeyDown(KeyCode.Return)) // Enter 키
            OnLoginClick();
    }

    // Tab 키로 다음 입력창으로 포커스 이동
    private void FocusNextInput()
    {
        if (EventSystem.current == null) return;

        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current == null) return;

        if (current == _idInput.gameObject)
        {
            _pwInput.Select();
            _pwInput.ActivateInputField();
        }
        else if (current == _pwInput.gameObject)
        {
            _idInput.Select();
            _idInput.ActivateInputField();
        }
    }

    //  로그인 버튼
    public void OnLoginClick()
    {
        string id = _idInput.text.Trim();
        string pw = _pwInput.text;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            SetLoginStatus("아이디와 비밀번호를 입력해주세요.");
            return;
        }

        SetLoginStatus("로그인 중...");
        SetLoginInteractable(false);

        BackendManager.Instance.Login(id, pw,
            onSuccess: () =>
            {
                SetLoginStatus("닉네임 확인 중...");

                // 로그인 성공 후 GetUserInfo 로 닉네임을 별도 조회
                var userInfoBro = Backend.BMember.GetUserInfo();
                if (userInfoBro.IsSuccess())
                {
                    Debug.Log("[LoginUIManager] 유저 정보 조회 성공 : " + userInfoBro);
                    string nickname = "";
                    try
                    {
                        var row = userInfoBro.GetReturnValuetoJSON()["row"];
                        if (row != null)
                        {
                            // LitJSON 에서 null 값은 ToString() 시 "null" 문자열로 반환되므로 명시적으로 체크
                            string nick = row["nickname"]?.ToString() ?? "";
                            
                            if (!string.IsNullOrEmpty(nick) && nick != "null")
                            {
                                nickname = nick;
                                BackendManager.Instance.ApplyNickname(nickname);
                            }
                        }

                        Debug.Log($"[LoginUIManager] 로그인 성공 (nickname: '{nickname}')");
                        SetLoginStatus("로그인 성공!");

                        // 유저 데이터 삽입 여부
                        BackendManager.Instance.CreateOrLoadUserData(
                            onSuccess: () => BackendManager.Instance.LoadLobbyScene(),
                            onFail: (e) =>
                            {
                                SetLoginStatus("유저 데이터 생성 실패: " + e);
                                SetLoginInteractable(true);
                            }
                        );
                    }
                    catch (Exception e)
                    { 
                        Debug.LogError($"[LoginUIManager] Failed to parse user info JSON: {e.Message}");
                        SetLoginInteractable(true); 
                    }
                }
                else
                {
                    Debug.LogError($"[LoginUIManager] GetUserInfo failed: {userInfoBro}");
                    SetLoginStatus("유저 정보 조회 실패: " + userInfoBro);
                    SetLoginInteractable(true);
                }
            },
            onFail: (err) =>
            {
                Debug.LogWarning($"[LoginUIManager] Login failed : {err}");
                SetLoginStatus("로그인 실패: " + err);
                SetLoginInteractable(true);
            }
        );
    }

    //  회원가입 버튼 → Register 씬으로 이동
    public void OnRegisterClick()
    {
        if (string.IsNullOrEmpty(_registerSceneName))
        {
            Debug.LogError("[LoginUIManager] _registerSceneName is empty.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(_registerSceneName))
        {
            Debug.LogError($"[LoginUIManager] Scene '{_registerSceneName}' is not in Build Settings.");
            return;
        }
        Debug.Log($"[LoginUIManager] 회원가입 씬 이동: '{_registerSceneName}'");
        
        SceneManager.LoadScene(_registerSceneName);
    }

    //  패널 전환 헬퍼
    private void ShowloginPanel()
    {
        if (_loginPanel != null) _loginPanel.SetActive(true);
    }

    // 로그인 상태
    private void SetLoginStatus(string msg)
    {
        if (_loginStatusText != null) _loginStatusText.text = msg;
    }


    // 버튼 활성화 여부
    private void SetLoginInteractable(bool value)
    {
        _loginBtn.interactable = value;
        _registerBtn.interactable = value;
    }
}
