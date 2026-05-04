using System;
using System.Collections.Generic;
using BackEnd;
using BackEnd.Leaderboard;
using UnityEngine;

/// <summary>
/// 뒤끝 리더보드(UserRank) 전용 매니저.
/// 점수 업데이트 / 랭킹 리스트 조회만 담당하고, UI 표시는 호출자(RankingManager 등)가 처리.
/// </summary>
public class BackendRank : MonoBehaviour
{
    public static BackendRank Instance { get; private set; }

    [Header("UserRank Leaderboard")]
    [SerializeField] private string _userRankUuid; // 뒤끝 콘솔 > 리더보드 에서 발급받은 UUID

    // 점수 업데이트 시 함께 갱신할 GameData 테이블
    private const string USER_DATA_TABLE = "UserData";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BackendRank] Duplicate instance detected, destroying.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ──────────────────────────────────────────────────────────────
    //  내 점수를 UserData.score 컬럼에 반영 → 리더보드 자동 동기화
    //  GameData 조회/생성은 동기, 리더보드 갱신은 비동기 콜백
    // ──────────────────────────────────────────────────────────────
    public void UpdateMyScore(int score,
                              Action onSuccess,
                              Action<string> onFail)
    {
        if (string.IsNullOrEmpty(_userRankUuid))
        {
            onFail?.Invoke("UserRank UUID가 비어있습니다. Inspector에서 설정하세요.");
            return;
        }

        // 1) UserData row의 inDate 조회 (없으면 새 row 생성)
        string rowInDate;
        var getBro = Backend.GameData.GetMyData(USER_DATA_TABLE, new Where());
        if (!getBro.IsSuccess())
        {
            Debug.LogError($"[BackendRank] GetMyData failed: {getBro}");
            onFail?.Invoke(getBro.ToString());
            return;
        }

        var rows = getBro.FlattenRows();  
        if (rows.Count > 0)
        {
            rowInDate = rows[0]["inDate"].ToString();
        }
        else
        {
            var insertBro = Backend.GameData.Insert(USER_DATA_TABLE);
            if (!insertBro.IsSuccess())
            {
                Debug.LogError($"[BackendRank] Insert failed: {insertBro}");
                onFail?.Invoke(insertBro.ToString());
                return;
            }
            rowInDate = insertBro.GetInDate();
        }

        // 2) score 컬럼 갱신 + 리더보드 동기화 (비동기)
        Param param = new Param();
        param.Add("score", score);

        Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(
            _userRankUuid, USER_DATA_TABLE, rowInDate, param,
            callback =>
            {
                if (!callback.IsSuccess())
                {
                    Debug.LogError($"[BackendRank] UpdateMyDataAndRefreshLeaderboard failed: {callback}");
                    onFail?.Invoke(callback.ToString());
                    return;
                }

                Debug.Log($"[BackendRank] 랭킹 업데이트 성공 (score: {score})");
                onSuccess?.Invoke();
            });
    }

    // ──────────────────────────────────────────────────────────────
    //  랭킹 리스트 조회 (offset+1위부터 limit개)
    //  ex) limit=50, offset=0  → 1~50위
    //      limit=50, offset=50 → 51~100위
    //  raw UserLeaderboardItem 리스트를 그대로 콜백에 전달 (호출자에서 표시)
    // ──────────────────────────────────────────────────────────────
    public void GetRankList(int limit, int offset,
                            Action<List<UserLeaderboardItem>> onSuccess,
                            Action<string> onFail)
    {
        if (string.IsNullOrEmpty(_userRankUuid))
        {
            onFail?.Invoke("UserRank UUID가 비어있습니다.");
            return;
        }

        Backend.Leaderboard.User.GetLeaderboard(
            _userRankUuid, limit, offset,
            callback =>
            {
                if (!callback.IsSuccess())
                {
                    Debug.LogError($"[BackendRank] GetLeaderboard failed: {callback}");
                    onFail?.Invoke(callback.ToString());
                    return;
                }

                var list = callback.GetUserLeaderboardList();
                Debug.Log($"[BackendRank] 랭킹 조회 성공 (총 등록자 {callback.GetTotalCount()}명, 받은 {list.Count}건)");
                onSuccess?.Invoke(list);
            });
    }

    // ──────────────────────────────────────────────────────────────
    //  내 랭킹만 조회 (UserLeaderboardItem 리스트 — 랭크 미등록 시 빈 리스트)
    // ──────────────────────────────────────────────────────────────
    public void GetMyRank(Action<List<UserLeaderboardItem>> onSuccess,
                          Action<string> onFail)
    {
        if (string.IsNullOrEmpty(_userRankUuid))
        {
            onFail?.Invoke("UserRank UUID가 비어있습니다.");
            return;
        }

        Backend.Leaderboard.User.GetMyLeaderboard(_userRankUuid, callback =>
        {
            if (!callback.IsSuccess())
            {
                // 404 = 아직 점수 갱신 전(리더보드 entry 없음). 에러가 아니라 미등록 상태로 처리
                if (callback.GetStatusCode() == "404")
                {
                    Debug.Log("[BackendRank] 내 랭킹 미등록 상태 (점수 갱신 이력 없음)");
                    onSuccess?.Invoke(new List<UserLeaderboardItem>());
                    return;
                }

                Debug.LogError($"[BackendRank] GetMyLeaderboard failed: {callback}");
                onFail?.Invoke(callback.ToString());
                return;
            }

            var list = callback.GetUserLeaderboardList();
            Debug.Log($"[BackendRank] 내 랭킹 조회 성공 (받은 {list.Count}건)");
            onSuccess?.Invoke(list);
        });
    }
}
