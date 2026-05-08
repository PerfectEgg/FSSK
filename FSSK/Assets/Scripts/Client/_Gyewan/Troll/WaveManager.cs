using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class WaveManager : MonoBehaviour
{
    // 싱글톤 패턴 적용
    public static WaveManager Instance { get; private set; }

    // 지정된 아이템 프로세스
    private List<int> _itemProgression = new List<int> { -1, 0, 1, -1, 0, 1, -1, 0, 1, 2 };

    [Header("웨이브 설정")]
    [SerializeField] private float _stageDuration = 30f;    // 각 웨이브 지속 시간 (30초)
    [SerializeField] private float _stageDelay = 5f;        // 웨이브 간의 지연 시간 (5초)
    private float _startTime;         // 웨이브 아이템 드랍 시작 시점
    private float _endTime;          // 웨이브 아이템 드랍 종료 시점

    [Header("아이템 설정")]
    [SerializeField] private GameObject _rumPrefab;         // 드랍할 아이템 프리팹
    [SerializeField] private GameObject _octopusPrefab;     // 드랍할 아이템 프리팹
    [SerializeField] private Transform[] _itemSpawnPoints;  // 아이템 스폰 포인트

    // 내부 상태 변수
    private int _currentStage = 0;
    private float _waveTimer = 0f;
    private bool _isWaveActive = false;

    // 아이템 스폰 제어용 변수
    private float _nextSpawnTime = 0f;                  // 다음 아이템 스폰 시점
    private bool _hasSpawnedItemInCurrentWave = false;  // 현재 웨이브에서 아이템이 이미 스폰되었는지 여부

    // 외부에서 현재 단계를 읽을 수 있도록 프로퍼티 개방
    public int currentStage => _currentStage;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 🟢 방장(마스터 클라이언트)일 때만 웨이브 시스템을 가동합니다.
        if (PhotonNetwork.IsMasterClient)
        {
            StartGameWaves();
        }
    }

    public void StartGameWaves()
    {
        // 🟢 안전장치: 방장이 아니면 실행 불가
        if (!PhotonNetwork.IsMasterClient) return;

        _currentStage = 0;
        _waveTimer = 0f;
        _startTime = _stageDelay;                 // 웨이브 시작 후 아이템 생성 초기 시간
        _endTime = _stageDuration -_stageDelay;   // 웨이브 시작 후 아이템 생성 종료 시간

        _isWaveActive = true;

        SetupNextItemSpawn();
        
        TrollEvents.OnWaveStageChanged?.Invoke(_currentStage); // 0단계 시작 방송
    }

    private void Update()
    {
        // 🟢 방장이 아니면 시간 계산을 아예 하지 않음 (각자 계산하면 싱크가 어긋납니다)
        if (!PhotonNetwork.IsMasterClient) return;

        if (!_isWaveActive) return;

        _waveTimer += Time.deltaTime;

        // 🟢 아이템 스폰 로직 (현재 웨이브에서 아직 스폰 안 했고, 랜덤 스폰 시간이 지났다면)
        if (!_hasSpawnedItemInCurrentWave && _waveTimer >= _nextSpawnTime)
        {
            SpawnItemForCurrentWave();
        }

        // 🟢 30초가 경과하면 다음 웨이브로 넘어감
        if (_waveTimer >= _stageDuration)
        {
            _waveTimer = 0f;
            _currentStage++;
            
            Debug.Log($"🌊 [WaveManager] {_currentStage}단계 진입!");

            SetupNextItemSpawn(); // 다음 웨이브의 랜덤 스폰 시간 다시 세팅
            
            // 🟢 방장이 다음 단계로 넘어갔음을 모두에게 전파
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.BroadcastWaveStage(_currentStage);
            }
        }
    }

    // 다음 아이템이 언제 스폰될지 시간을 정하고 상태를 초기화하는 함수
    private void SetupNextItemSpawn()
    {
        _nextSpawnTime = Random.Range(_startTime, _endTime);
        _hasSpawnedItemInCurrentWave = false;
    }

    // 아이템을 판별하고 스폰하는 핵심 함수
    private void SpawnItemForCurrentWave()
    {
        // 다시 한 번 방장 체크 (이중 안전장치)
        if (!PhotonNetwork.IsMasterClient) return;

        _hasSpawnedItemInCurrentWave = true; // 중복 생성 방지

        // 배열 범위를 초과하지 않도록 안전장치 (9단계를 넘어가면 마지막 값 유지)
        int targetIndex = Mathf.Min(_currentStage, _itemProgression.Count - 1);
        int itemType = _itemProgression[targetIndex];

        GameObject prefabToSpawn = null;

        // switch 문으로 아이템 종류 할당
        switch (itemType)
        {
            case -1:
                // -1은 아이템 드랍 없음, 그냥 함수를 종료합니다.
                return;
            case 0:
                // 0은 럼 드랍
                prefabToSpawn = _rumPrefab;
                break;
            case 1:
                // 1은 문어 드랍
                prefabToSpawn = _octopusPrefab;
                break;
            case 2:
                // 2는 50% 확률로 럼 또는 문어 드랍
                prefabToSpawn = (Random.value < 0.5f) ? _rumPrefab : _octopusPrefab;
                break;
        }

        // 스폰 포인트가 할당되어 있고 프리팹이 있다면 생성!
        if (prefabToSpawn != null && _itemSpawnPoints.Length > 0)
        {
            int randomSpawnIndex = Random.Range(0, _itemSpawnPoints.Length);
            Transform spawnPoint = _itemSpawnPoints[randomSpawnIndex];

           // 🟢 Instantiate 대신 서버 매니저의 네트워크 소환 로직 사용!
            string prefabName = prefabToSpawn.name;
            NetworkGameManager.Instance.SpawnNetworkObject(prefabName, spawnPoint.position, spawnPoint.rotation);
        }
    }
}