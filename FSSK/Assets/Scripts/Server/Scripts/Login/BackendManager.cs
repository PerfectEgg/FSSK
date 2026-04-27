using UnityEngine;
using UnityEngine.SceneManagement;
using BackEnd;
using System;

/// <summary>
/// 뒤끝 SDK 초기화 및 유저 데이터를 씬 전반에 걸쳐 유지하는 싱글톤 매니저.
/// DontDestroyOnLoad 로 로그인 → 매칭 씬까지 살아남습니다.
/// </summary>
public class BackendManager : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────
    //  싱글톤
    // ──────────────────────────────────────────────────────────────
    public static BackendManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    //  유저 데이터 (씬 이동 후 MatchingManager 등에서 참조)
    // ──────────────────────────────────────────────────────────────
    public string Nickname { get; private set; } = "";
    public bool IsLoggedIn { get; private set; } = false;

    // ──────────────────────────────────────────────────────────────
    //  씬 이름 (Inspector 에서 변경 가능)
    // ──────────────────────────────────────────────────────────────
    [Header("Scene Names")]
    [SerializeField] private string matchingSceneName = "Matching";

    // ──────────────────────────────────────────────────────────────
    //  초기화
    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        // 중복 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeBackend();
    }

    private void InitializeBackend()
    {
        var bro = Backend.Initialize();
        if (bro.IsSuccess())
            Debug.Log("뒤끝 초기화 성공 : " + bro);
        else
            Debug.LogError("뒤끝 초기화 실패 : " + bro);
    }

    // ──────────────────────────────────────────────────────────────
    //  로그인 (성공/실패만 반환, 닉네임 조회는 LoginUIManager 에서 직접)
    // ──────────────────────────────────────────────────────────────
    public void Login(string id, string pw,
                      Action onSuccess,
                      Action<string> onFail)
    {
        bool ok = BackendLogin.Instance.CustomLogin(id, pw, out string errorMsg);

        if (ok)
        {
            IsLoggedIn = true;
            Debug.Log("[BackendManager] 로그인 성공");
            onSuccess?.Invoke();
        }
        else
        {
            onFail?.Invoke(errorMsg);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  회원가입
    //  성공 시 onSuccess(), 실패 시 onFail(errorMsg)
    // ──────────────────────────────────────────────────────────────
    public void SignUp(string id, string pw,
                       Action onSuccess,
                       Action<string> onFail)
    {
        bool ok = BackendLogin.Instance.CustomSignUp(id, pw, out string errorMsg);

        if (ok)
        {
            Debug.Log("[BackendManager] 회원가입 성공");
            onSuccess?.Invoke();
        }
        else
        {
            onFail?.Invoke(errorMsg);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  닉네임 설정 (로그인 후 최초 설정 or 변경)
    // ──────────────────────────────────────────────────────────────
    public void SetNickname(string nickname,
                            Action onSuccess,
                            Action<string> onFail)
    {
        bool ok = BackendLogin.Instance.UpdateNickname(nickname, out string errorMsg);

        if (ok)
        {
            Nickname = nickname;
            Debug.Log($"[BackendManager] 닉네임 설정 완료: '{nickname}'");
            onSuccess?.Invoke();
        }
        else
        {
            onFail?.Invoke(errorMsg);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  서버 호출 없이 로컬 Nickname 값만 저장 (LoginUIManager 에서
    //  BackendLogin 으로 직접 저장한 뒤 여기서 동기화할 때 사용)
    // ──────────────────────────────────────────────────────────────
    public void ApplyNickname(string nickname)
    {
        Nickname = nickname;
        Debug.Log($"[BackendManager] 닉네임 로컬 적용: '{nickname}'");
    }

    // ──────────────────────────────────────────────────────────────
    //  씬 이동 헬퍼
    // ──────────────────────────────────────────────────────────────
    public void LoadMatchingScene()
    {
        SceneManager.LoadScene(matchingSceneName);
    }
}