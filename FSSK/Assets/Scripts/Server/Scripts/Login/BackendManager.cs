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
    // 싱글톤
    public static BackendManager Instance { get; private set; }

    // 유저 데이터 프로 퍼티 (씬 이동 후 MatchingManager 등에서 참조) - 
    public string Nickname { get; private set; } = "";
    public bool IsLoggedIn { get; private set; } = false;
    public UserData MyUserData {get; private set; } 

    // 뒤끝 GameData 테이블 이름 (콘솔에서 만든 테이블명과 동일해야 함)
    private const string USER_DATA_TABLE = "UserData";


    // 씬 이름 (Inspector 에서 변경 가능)
    [Header("Scene Names")]
    [SerializeField] private string _matchingSceneName = "Lobby";

    // 초기화
    void Awake()
    {
        // 중복 방지
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BackendManager] Duplicate instance detected, destroying.");
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
            Debug.Log("[BackendManager] 뒤끝 초기화 성공 : " + bro);
        else
            Debug.LogError($"[BackendManager] Backend.Initialize failed: {bro}");
    }

    // 로그인 (성공/실패만 반환, 닉네임 조회는 LoginUIManager 에서 직접)
    public void Login(string id, string pw,
                      Action onSuccess,
                      Action<string> onFail)
    {
        bool ok = BackendLogin.Instance.CustomLogin(id, pw, out string errorMsg);

        if (ok)
        {
            IsLoggedIn = true;
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
            onSuccess?.Invoke();
        }
        else
        {
            onFail?.Invoke(errorMsg);
        }
    }

    // 닉네임 설정 (로그인 후 최초 설정 or 변경)
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
    //  유저 게임 데이터 생성  
    //  UserData 기본값(score=1000, winCount=0, loseCount=0)으로 row 생성
    //  닉네임은 뒤끝이 자동 연동하므로 컬럼에 넣지 않음
    //  구조 흐름 : 유저 정보 확인 -> 게임 데이터 없으면 추가 후 씬 넘어감/있으면 씬 넘어감  
    // ──────────────────────────────────────────────────────────────
    public void CreateOrLoadUserData(Action onSuccess,
                               Action<string> onFail)
    {

        // 기존 유저 게임 데이터 확인
        var getDataBro = Backend.GameData.GetMyData(USER_DATA_TABLE, new Where(), 1);
        
        // API 호출 실패인 경우
        if (!getDataBro.IsSuccess())
        {
            Debug.LogError($"[BackendManager] GetMyData failed: {getDataBro}");
            onFail?.Invoke(getDataBro.ToString());
        }

        var rows = getDataBro.FlattenRows();
        if(rows != null && rows.Count > 0)
        {
            // 데이터 있음 -> 로드
            var row = rows[0];
            MyUserData = new UserData
            {
                score = int.Parse(row["score"].ToString()),
                winCount  = int.Parse(row["winCount"].ToString()),
                loseCount = int.Parse(row["loseCount"].ToString())
            };
            Debug.Log($"[BackendManager] 유저 데이터 로드 (score: {MyUserData.score}, win: {MyUserData.winCount}, lose: {MyUserData.loseCount})");
            onSuccess?.Invoke();
            return;
        }
        
        // 데이터 없는 경우 -> 유저 데이터 생성
        MyUserData = new UserData();

        Param param = new()
        {
            { "score", MyUserData.score },
            { "winCount", MyUserData.winCount },
            { "loseCount", MyUserData.loseCount }
        };

        var InsertBro = Backend.GameData.Insert(USER_DATA_TABLE, param);

        if (InsertBro.IsSuccess())
        {
            Debug.Log("[BackendManager] 유저 데이터 생성 성공 : " + InsertBro);
            onSuccess?.Invoke();
        }
        else
        {
            Debug.LogError($"[BackendManager] Insert failed: {InsertBro}");
            onFail?.Invoke(InsertBro.ToString());
        }
    }

    // 로비 씬 이동 헬퍼
    public void LoadLobbyScene()
    {
        Debug.Log($"[BackendManager] 로비 씬 이동: '{_matchingSceneName}'");
        SceneManager.LoadScene(_matchingSceneName);
    }
}