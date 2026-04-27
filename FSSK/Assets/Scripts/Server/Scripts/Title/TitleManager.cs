using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [SerializeField]
    GameObject GameSettingPanel; // 게임 설정 패널 
    bool isGameSetting; // 게임 설정 보임 여부

    void Awake()
    {
        isGameSetting = false;

        if(GameSettingPanel != null) GameSettingPanel.SetActive(false);
    } 

    // 시작 버튼 클릭 시
    public void OnStartClick()
    {
        SceneManager.LoadScene("Login");
    }

    //설정 버튼 클릭 시
    public void OnSettingClick()
    {
        if (!isGameSetting)
        {
            GameSettingPanel.SetActive(true);
            isGameSetting = true;
        }
        else
        {
            GameSettingPanel.SetActive(false);
            isGameSetting = false;
        }
    }

    // 종료 버튼 클릭 시
    public void OnExitClick()
    {
        // 유니티 에디터 환경일 경우: 플레이 모드를 강제로 끕니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        
        // 실제 스마트폰이나 PC로 빌드된 게임일 경우: 앱을 완전히 종료합니다.
#else
        Application.Quit();
#endif
    
    }
    
}
