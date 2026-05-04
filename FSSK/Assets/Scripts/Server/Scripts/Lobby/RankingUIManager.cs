using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RankingUIManager : MonoBehaviour
{
    [SerializeField] private Button _backBtn;
    [SerializeField] private Button _refreshBtn;

    [Header("My Rank Panel")]
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _nicknameText;
    [SerializeField] private TextMeshProUGUI _scoreText;

    [Header("Rank List")]
    [SerializeField] private RankItemUI _rankItemPrefab; // RankItem 프리팹
    [SerializeField] private Transform _rankListContent; // ScrollView/Viewport/Content
    
    private const int RANK_LIST_LIMIT = 50;
    private const int RANK_LIST_OFFSET = 0;

    void Start()
    {
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

    // 1~50위 조회 후 ScrollView/Content 아래에 행을 복제해서 채움
    private void FetchRankList()
    {
        BackendRank.Instance.GetRankList(RANK_LIST_LIMIT, RANK_LIST_OFFSET,
            onSuccess: list =>
            {
                ClearRankList();

                foreach (var item in list)
                {
                    var row = Instantiate(_rankItemPrefab, _rankListContent);
                    row.Bind(item);
                }

                Debug.Log($"[RankingUIManager] 랭킹 리스트 표시 완료 ({list.Count}건)");
            },
            onFail: err =>
            {
                Debug.LogError($"[RankingUIManager] GetRankList failed: {err}");
            });
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
