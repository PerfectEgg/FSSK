using BackEnd;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 회원가입 씬 UI 연결 매니저.
/// 아이디/비밀번호 입력 → 뒤끝 회원가입 → 닉네임 적용 → 매칭 씬 이동.
/// </summary>
public class RegisterUIManager : MonoBehaviour
{
    // ── 회원가입 패널 ────────────────────────────────────────────
    [Header("Register Panel")]
    [SerializeField] private GameObject _registerPanel;
    [SerializeField] private TMP_InputField _idInput;
    [SerializeField] private TMP_InputField _pwInput;
    [SerializeField] private TMP_InputField _pwConfirmInput;
    [SerializeField] private Button _registerBtn;
    [SerializeField] private TextMeshProUGUI _registerStatusText;

    [Header("Scene Names")]
    [SerializeField] private string _loginSceneName = "Login";

    // ─────────────────────────────────────────────────────────────

    void Start()
    {
        ShowRegisterPanel();

        _registerBtn.onClick.AddListener(OnRegisterClick);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            FocusNextInput();
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
            _pwConfirmInput.Select();
            _pwConfirmInput.ActivateInputField();
        }
        else if (current == _pwConfirmInput.gameObject)
        {
            _idInput.Select();
            _idInput.ActivateInputField();
        }
    }

    // 회원가입 버튼
    public void OnRegisterClick()
    {
        string id = _idInput.text.Trim();
        string pw = _pwInput.text;
        string pwConfirm = _pwConfirmInput.text;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw) || string.IsNullOrEmpty(pwConfirm))
        {
            SetRegisterStatus("빈칸에 내용을 입력해주세요.");
            return;
        }

        if (pw != pwConfirm)
        {
            SetRegisterStatus("비밀번호가 일치하지 않습니다.");
            return;
        }

        SetRegisterStatus("회원가입 중...");
        SetRegisterInteractable(false);

        BackendManager.Instance.SignUp(id, pw,
            onSuccess: () =>
            {
                Debug.Log($"[RegisterUIManager] 회원가입 성공");
                SetRegisterStatus("회원가입 성공! 로그인 해주세요.");
                SetRegisterInteractable(true);

                // id => 닉네임 적용
                bool ok = BackendLogin.Instance.UpdateNickname(id, out string errorMsg);
                if (!ok)
                {
                    Debug.LogWarning($"[RegisterUIManager] UpdateNickname failed: {errorMsg}");
                    return;
                }

                // BackendManager 에 로컬 닉네임 동기화 (서버 재호출 없음)
                BackendManager.Instance.ApplyNickname(id);
                
                //유저 데이터 row 최초 생성 → 성공 시 매칭 씬 이동
                BackendManager.Instance.CreateOrLoadUserData(
                    onSuccess: () => BackendManager.Instance.LoadLobbyScene(),
                    onFail: (e) =>
                    {
                        SetRegisterStatus("유저 데이터 생성 실패: " + e);
                        SetRegisterInteractable(true);
                    }
                );
            },
            onFail: (err) =>
            {
                Debug.LogWarning($"[RegisterUIManager] SignUp failed : {err}");
                SetRegisterStatus("회원가입 실패: " + err);
                SetRegisterInteractable(true);
            }
        );
    }

    // 뒤로 가기 → 로그인 씬으로 이동
    public void OnBackBtnClick()
    {
        if (string.IsNullOrEmpty(_loginSceneName))
        {
            Debug.LogError("[RegisterUIManager] _loginSceneName is empty.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(_loginSceneName))
        {
            Debug.LogError($"[RegisterUIManager] Scene '{_loginSceneName}' is not in Build Settings.");
            return;
        }
        Debug.Log($"[RegisterUIManager] 로그인 씬 이동: '{_loginSceneName}'");

        SceneManager.LoadScene(_loginSceneName);
    }

    // 패널 전환 헬퍼
    private void ShowRegisterPanel()
    {
        if (_registerPanel != null) _registerPanel.SetActive(true);
    }

    // 회원가입 상태
    private void SetRegisterStatus(string msg)
    {
        if (_registerStatusText != null) _registerStatusText.text = msg;
    }

    // 버튼 활성화 여부
    private void SetRegisterInteractable(bool value)
    {
        _registerBtn.interactable = value;
    }

    
    
}
