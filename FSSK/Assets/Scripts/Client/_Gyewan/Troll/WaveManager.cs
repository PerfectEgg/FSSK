using UnityEngine;

public class WaveManager : MonoBehaviour
{
    // 싱글톤 패턴 적용
    public static WaveManager Instance { get; private set; }

    [Header("Wave Settings")]
    [SerializeField] private float _stageDuration = 5f;    // 각 웨이브 지속 시간 (30초)
    [SerializeField] private float _startTime = 5f;         // 웨이브 아이템 드랍 시작 시점
    [SerializeField] private float _endTime = 25f;          // 웨이브 아이템 드랍 종료 시점

    // 내부 상태 변수
    private int _currentStage = 0;
    private float _waveTimer = 0f;
    private bool _isWaveActive = false;

    // 외부에서 현재 단계를 읽을 수 있도록 프로퍼티 개방
    public int currentStage => _currentStage;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartGameWaves();
    }

    public void StartGameWaves()
    {
        _currentStage = 0;
        _waveTimer = 0f;
        _isWaveActive = true;
        
        GameEvents.OnWaveStageChanged?.Invoke(_currentStage); // 0단계 시작 방송
    }

    private void Update()
    {
        if (!_isWaveActive) return;

        _waveTimer += Time.deltaTime;

        // 🟢 30초가 경과하면 다음 웨이브로 넘어감
        if (_waveTimer >= _stageDuration)
        {
            _waveTimer = 0f;
            _currentStage++;
            
            Debug.Log($"🌊 [WaveManager] {_currentStage}단계 진입!");
            
            // 🟢 지휘자는 그저 현재 몇 단계인지만 동네방네 소문냅니다.
            GameEvents.OnWaveStageChanged?.Invoke(_currentStage);
        }
    }
}