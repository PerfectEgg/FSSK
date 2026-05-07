using UnityEngine;
using UnityEngine.SceneManagement;

public class CommonUIManager : MonoBehaviour
{
    [SerializeField]
    private GameObject _gameSettingPanel; // 게임 설정 패널 
    private bool _isGameSetting; // 게임 설정 보임 여부

    void Awake()
    {   
        // 게임 설정 패널이 있는 씬일 경우
        if(_gameSettingPanel != null)
        {
            _isGameSetting = false;
            _gameSettingPanel.SetActive(false);
            Debug.Log("[CommonUIManager] _gameSettingPanel 설정");
            
        }
        else
        {
            Debug.Log("[CommonUIManager] _gameSettingPanel 미설정");
        }
        
    } 

    // 시작 버튼 클릭 시
    public void OnStartClick()
    {
        Debug.Log("[CommonUIManager] [게임시작] 로그인 화면 진입");
        SceneManager.LoadScene("Login");
    }

    //설정 버튼 클릭 시
    public void OnSettingClick()
    {
        if(_gameSettingPanel == null)
        {
            Debug.LogError("[CommonUIManager] _gameSettingPanel not assigned");
            return;
        }

        _isGameSetting = !_isGameSetting;
        _gameSettingPanel.SetActive(_isGameSetting);
        
    }

    // 종료 버튼 클릭 시
    public void OnExitClick()
    {

        Debug.Log("[CommonUIManager] [게임 종료] 버튼 클릭");

        // 유니티 에디터 환경일 경우: 플레이 모드를 강제로 끕니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        
        // 실제 스마트폰이나 PC로 빌드된 게임일 경우: 앱을 완전히 종료합니다.
#else
        Application.Quit();
#endif
    
    }
    
}
