using BackEnd;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인 씬 UI 연결 매니저.
///
/// 하이어라키 구성:
///   LoginPanel
///     ├ IDtxt          (TextMeshProUGUI - 레이블, 필요 없으면 없어도 됨)
///     ├ PWtxt          (TextMeshProUGUI - 레이블)
///     ├ ID             (TMP_InputField  - 아이디 입력)
///     ├ PASS           (TMP_InputField  - 비밀번호 입력)
///     ├ LoginBtn       (Button)
///     └ RegisterBtn    (Button)
///   NicknamePanel      (로그인 후 닉네임 미설정 시 표시)
///     ├ NicknameInput  (TMP_InputField)
///     ├ ConfirmBtn     (Button)
///     └ StatusText     (TextMeshProUGUI - 오류/안내 메시지)
///   StatusText         (TextMeshProUGUI - 로그인 상태 메시지)
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    // ── 로그인 패널 ──────────────────────────────────────────────
    [Header("Login Panel")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField idInput;
    [SerializeField] private TMP_InputField passInput;
    [SerializeField] private Button loginBtn;
    [SerializeField] private Button registerBtn;
    [SerializeField] private TextMeshProUGUI loginStatusText;

    // ── 닉네임 설정 패널 (로그인 성공 후 닉네임 없을 때 표시) ───
    [Header("Nickname Panel")]
    [SerializeField] private GameObject nicknamePanel;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button nicknameConfirmBtn;
    [SerializeField] private TextMeshProUGUI nicknameStatusText;

    // ─────────────────────────────────────────────────────────────

    void Start()
    {
        ShowLoginPanel();

        loginBtn.onClick.AddListener(OnLoginClick);
        registerBtn.onClick.AddListener(OnRegisterClick);
        nicknameConfirmBtn.onClick.AddListener(OnNicknameConfirmClick);
    }

    // ─────────────────────────────────────────────────────────────
    //  로그인 버튼
    // ─────────────────────────────────────────────────────────────
    public void OnLoginClick()
    {
        string id = idInput.text.Trim();
        string pw = passInput.text;

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
                    Debug.Log("유저 정보 조회 성공 : " + userInfoBro);
                    string nickname = "";
                    try
                    {
                        var row = userInfoBro.GetReturnValuetoJSON()["row"];
                        if (row != null)
                        {
                            // LitJSON 에서 null 값은 ToString() 시 "null" 문자열로 반환되므로 명시적으로 체크
                            string nick = row["nickname"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(nick) && nick != "null")
                                nickname = nick;
                        }
                    }
                    catch { /* 닉네임 없음 */ }

                    if (!string.IsNullOrEmpty(nickname))
                    {
                        // 닉네임 있음 → BackendManager 에 저장 후 매칭 씬으로
                        BackendManager.Instance.ApplyNickname(nickname);
                        SetLoginStatus("로그인 성공!");
                        BackendManager.Instance.LoadMatchingScene();
                    }
                    else
                    {
                        // 닉네임 없음 → 닉네임 설정 패널 표시
                        ShowNicknamePanel();
                    }
                }
                else
                {
                    SetLoginStatus("유저 정보 조회 실패: " + userInfoBro);
                    SetLoginInteractable(true);
                }
            },
            onFail: (err) =>
            {
                SetLoginStatus("로그인 실패: " + err);
                SetLoginInteractable(true);
            }
        );
    }

    // ─────────────────────────────────────────────────────────────
    //  회원가입 버튼
    // ─────────────────────────────────────────────────────────────
    public void OnRegisterClick()
    {
        string id = idInput.text.Trim();
        string pw = passInput.text;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            SetLoginStatus("아이디와 비밀번호를 입력해주세요.");
            return;
        }

        SetLoginStatus("회원가입 중...");
        SetLoginInteractable(false);

        BackendManager.Instance.SignUp(id, pw,
            onSuccess: () =>
            {
                SetLoginStatus("회원가입 성공! 로그인 해주세요.");
                SetLoginInteractable(true);
            },
            onFail: (err) =>
            {
                SetLoginStatus("회원가입 실패: " + err);
                SetLoginInteractable(true);
            }
        );
    }

    // ─────────────────────────────────────────────────────────────
    //  닉네임 확인 버튼
    //  BackendLogin 으로 서버에 저장 → BackendManager 에 로컬 반영
    // ─────────────────────────────────────────────────────────────
    public void OnNicknameConfirmClick()
    {
        string nickname = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(nickname))
        {
            SetNicknameStatus("닉네임을 입력해주세요.");
            return;
        }

        SetNicknameStatus("닉네임 설정 중...");
        nicknameConfirmBtn.interactable = false;

        // 1. BackendLogin 으로 뒤끝 서버에 직접 저장
        bool ok = BackendLogin.Instance.UpdateNickname(nickname, out string errorMsg);

        if (ok)
        {
            // 2. BackendManager 에 로컬 닉네임 동기화 (서버 재호출 없음)
            BackendManager.Instance.ApplyNickname(nickname);

            SetNicknameStatus("닉네임 설정 완료!");
            BackendManager.Instance.LoadMatchingScene();
        }
        else
        {
            SetNicknameStatus("닉네임 설정 실패: " + errorMsg);
            nicknameConfirmBtn.interactable = true;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  패널 전환 헬퍼
    // ─────────────────────────────────────────────────────────────
    private void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (nicknamePanel != null) nicknamePanel.SetActive(false);
    }

    private void ShowNicknamePanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (nicknamePanel != null) nicknamePanel.SetActive(true);
        SetNicknameStatus("사용할 닉네임을 입력해주세요.");
    }

    private void SetLoginStatus(string msg)
    {
        if (loginStatusText != null) loginStatusText.text = msg;
    }

    private void SetNicknameStatus(string msg)
    {
        if (nicknameStatusText != null) nicknameStatusText.text = msg;
    }

    private void SetLoginInteractable(bool value)
    {
        loginBtn.interactable = value;
        registerBtn.interactable = value;
    }
}
