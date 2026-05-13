using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI; // UI 업데이트용 네임스페이스

public class UIManager : MonoBehaviour
{
    // 싱글톤
    public static UIManager Instance {get; private set;}
    
    [SerializeField] private GameObject _krakenWarningPanel;
    [SerializeField] private GameObject _sirenWarningPanel;
    [SerializeField] private Image _itemBarPanel;

    private void Awake() => Instance = this;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _krakenWarningPanel.SetActive(false);
        _sirenWarningPanel.SetActive(false);
        _itemBarPanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        TrollEvents.OnShowWarningMessage += HandleShowWarningMessage;
        TrollEvents.OnHideWarningMessage += HandleHideWarningMessage;
        TrollEvents.OnUpdateDebuffUI += HandleUpdateDebuffUI;
        TrollEvents.OnHideDebuffUI += HandleHideDebuffUI;
    }
    
    private void OnDisable()
    {
        TrollEvents.OnShowWarningMessage -= HandleShowWarningMessage;
        TrollEvents.OnHideWarningMessage -= HandleHideWarningMessage;
        TrollEvents.OnUpdateDebuffUI -= HandleUpdateDebuffUI;
        TrollEvents.OnHideDebuffUI -= HandleHideDebuffUI;
    }

    private void HandleShowWarningMessage(MonsterType type)
    {
        if (type == MonsterType.Kraken) _krakenWarningPanel.SetActive(true);
        else if (type == MonsterType.Siren) _sirenWarningPanel.SetActive(true);
    }

    private void HandleHideWarningMessage()
    {
        if (_krakenWarningPanel != null && _sirenWarningPanel != null)
        {
            _krakenWarningPanel.SetActive(false);
            _sirenWarningPanel.SetActive(false);
        }
    }

    private void HandleUpdateDebuffUI(float timeLeft)
    {
        if (_itemBarPanel != null && !_itemBarPanel.gameObject.activeSelf)
        {
            _itemBarPanel.gameObject.SetActive(true);
        }

        _itemBarPanel.fillAmount = timeLeft; // 예시: fillAmount를 남은 시간으로 설정 (0~1 사이 값)
    }

    private void HandleHideDebuffUI()
    {
        if (_itemBarPanel != null)
        {
            _itemBarPanel.gameObject.SetActive(false);
        }
    }
}
