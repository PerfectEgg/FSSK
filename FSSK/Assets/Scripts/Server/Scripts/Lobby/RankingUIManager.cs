using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
public class RankingUIManager : MonoBehaviour
{
    [SerializeField] private Button _backBtn;
    [SerializeField] private Button _refreshBtn;

    [Header("My Rank Panel")]
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private TextMeshProUGUI _scoreText;

    [Header("Top 3 Player Posters")]
    [SerializeField] private TopPlayerUI _firstPoster;   // 1위
    [SerializeField] private TopPlayerUI _secondPoster;  // 2위
    [SerializeField] private TopPlayerUI _thirdPoster;   // 3위

    [Header("Rank List (4위부터)")]
    [SerializeField] private RankItemUI _rankItemPrefab; // RankItem 프리팹
    [SerializeField] private Transform _rankListContent; // ScrollView/Viewport/Content
    
    private const int RANK_LIST_LIMIT = 50;
    private const int RANK_LIST_OFFSET = 0;

    void Start()
    {

        if (_firstPoster == null || _secondPoster == null || _thirdPoster == null)
        {
            Debug.LogError("[RankingUIManager] posters not assigned.");
            return;
        }
        if (_rankText == null || _nicknameText == null || _scoreText == null)
        {
            Debug.LogError("[RankingUIManager] MyRank text fields not assigned.");
            return;
        }
        if (_rankItemPrefab == null || _rankListContent == null)
        {
            Debug.LogError("[RankingUIManager] Rank list prefab/content not assigned.");
            return;
        }
        if (_backBtn == null || _refreshBtn == null)
        {
            Debug.LogError("[RankingUIManager] Buttons not assigned.");
            return;
        }
        if (BackendRank.Instance == null)
        {
            Debug.LogError("[RankingUIManager] BackendRank.Instance is null.");
            return;
        }

        _backBtn.onClick.AddListener(OnBackBtn);
        _refreshBtn.onClick.AddListener(OnRefreshBtn);

        FetchMyRank();
        FetchRankList();
    }

    // BackendManager 캐시로 우선 채우고, 리더보드 조회 결과로 덮어씀 (미등록/실패 시에도 기본값 표시)
    private void FetchMyRank()
    {
        var backend = BackendManager.Instance;
        _nicknameText.text = backend != null && !string.IsNullOrEmpty(backend.Nickname) ? backend.Nickname : "이름없음";
        _scoreText.text = backend != null && backend.MyUserData != null ? backend.MyUserData.score.ToString() : "0";
        _rankText.text = "-";

        BackendRank.Instance.GetMyRank(
            onSuccess: list =>
            {
                if (list.Count == 0)
                {
                    Debug.Log("[RankingUIManager] 내 랭킹 미등록 상태");
                    return;
                }

                var me = list[0];
                _rankText.text = me.rank + "위";
                _nicknameText.text = me.nickname;
                _scoreText.text = me.score.ToString();
                Debug.Log($"[RankingUIManager] 내 랭킹 표시 완료 ({me.rank}위 / {me.score}점)");
            },
            onFail: err =>
            {
                Debug.LogError($"[RankingUIManager] GetMyRank failed: {err}");
            });
    }

    // 1~50위 조회 후 Top3 이후는 ScrollView/Content 아래에 행을 복제해서 채움
    private void FetchRankList()
    {
        BackendRank.Instance.GetRankList(RANK_LIST_LIMIT, RANK_LIST_OFFSET,
            onSuccess: list =>
            {
                ClearRankList();

                BindTop3(list);
                Debug.Log($"[RankingUIManager] Top 3 데이터 삽입 완료");
                // Top3 이후부터는 스크롤 리스트에 삽입
                for (int i = 3; i < list.Count; i++)
                {
                    var row = Instantiate(_rankItemPrefab, _rankListContent);
                    row.Bind(list[i]);
                }

                Debug.Log($"[RankingUIManager] 랭킹 리스트 표시 완료 ({list.Count}건)");
            },
            onFail: err =>
            {
                Debug.LogError($"[RankingUIManager] GetRankList failed: {err}");
            });
    }

    // Top3 할당
    private void BindTop3(List<BackEnd.Leaderboard.UserLeaderboardItem> list)
    {
        TopPlayerUI[] posters = { _firstPoster, _secondPoster, _thirdPoster };
        for (int i = 0; i < posters.Length; i++)
        {
            if (i < list.Count) posters[i].Bind(list[i]);
            else posters[i].SetEmpty();
        }
    }

    private void ClearRankList()
    {
        for (int i = _rankListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(_rankListContent.GetChild(i).gameObject);
        }
    }

    // 뒤로가기 버튼
    public void OnBackBtn()
    {
        BackendManager.Instance.LoadLobbyScene();
    }

    // 새로고침 버튼 — 내 랭킹 + 전체 리스트 둘 다 다시 조회
    public void OnRefreshBtn()
    {
        Debug.Log("[RankingUIManager] 랭킹 새로고침");
        FetchMyRank();
        FetchRankList();
    }
}
