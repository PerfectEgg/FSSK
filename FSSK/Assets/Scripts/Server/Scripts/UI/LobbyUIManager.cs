using TMPro;
using UnityEngine;

public class LobbyUIManager : MonoBehaviour
{
    [Header("Character Info")]
    [SerializeField] private TextMeshProUGUI _scoreText; // 유저 점수
    [SerializeField] private TextMeshProUGUI _nicknameText; // 유저 닉네임

    void Start()
    {
        RefreshUserInfo();
    }

    // BackendManager에 캐시된 닉네임/유저 데이터를 UI에 반영
    private void RefreshUserInfo()
    {
        var backend = BackendManager.Instance;
        if (backend == null)
        {
            Debug.LogError("[LobbyUIManager] BackendManager.Instance가 없습니다. 로그인 씬을 거치지 않았는지 확인하세요.");
            return;
        }

        if (_nicknameText != null)
            _nicknameText.text = string.IsNullOrEmpty(backend.Nickname) ? "이름없음" : backend.Nickname;

        if (_scoreText != null)
            _scoreText.text = backend.MyUserData != null ? backend.MyUserData.score.ToString() : "0";
    }
}
