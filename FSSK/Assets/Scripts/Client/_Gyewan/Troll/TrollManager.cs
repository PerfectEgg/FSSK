using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class TrollManager : MonoBehaviour
{
    public static TrollManager Instance { get; private set; }

    public enum TrollType { None, Kraken, Siren, Parrot, Rat, Turtle, SeaCrab }

    [System.Serializable]
    public struct TrollData
    {
        public TrollType trollType;
        public string trollPrefabPath;  // Resources 폴더 내의 프리팹 경로
        [Tooltip("등장 확률 가중치 (예: 10, 20)")]
        public int spawnWeight;
    }

    [Header("트롤링 이벤트 데이터")]
    [SerializeField] private List<TrollData> _trollDatabase;
    [SerializeField] private Transform[] _groundSpawnPoints;    // 지상 스폰 포인트
    [SerializeField] private Transform[] _monsterSpawnPoints;   // 몬스터 스폰 포인트
    
    [Header("바다 게 설정")]
    [SerializeField] private float _safeRadius = 2.5f;       // 바다 게 최소 안전 거리

    // 내부 상태 관리
    private int _executionCount = 0;       // 실행 횟수
    private float _currentWaitTimer = 0f;  // 현재 돌아가는 타이머
    private bool _isWaiting = false;       // 타이머 작동 여부
    private TrollType _lastTroll = TrollType.None; // 연속 발생 금지용

    // 이벤트 구독 및 해제 (핵심!)
    private void OnEnable()
    {
        TrollEvents.RequestSafePosition += GetSafeRandomPosition;   // 안전한 랜덤 위치 요청 이벤트 구독
        TrollEvents.OnPositionReleased += ReleasePosition;          // 위치 반납 이벤트 구독
        TrollEvents.OnTrollFinished += HandleTrollFinished;         // 트롤 종료 이벤트 구독
    }

    private void OnDisable()
    {
        TrollEvents.RequestSafePosition -= GetSafeRandomPosition;
        TrollEvents.OnPositionReleased -= ReleasePosition;
        TrollEvents.OnTrollFinished -= HandleTrollFinished;
    }

    private List<Vector3> _occupiedPositions = new List<Vector3>();

    // 겹치지 않는 안전한 랜덤 위치를 찾아 반환하는 함수
    public Vector3 GetSafeRandomPosition()
    {
        int maxAttempts = 8; 

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 randomPos = new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
            if (IsPositionSafe(randomPos))
            {
                _occupiedPositions.Add(randomPos); 
                return randomPos;
            }
        }
        return new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
    }

    // 해당 위치가 다른 게들과 겹치는지 검사하는 함수
    private bool IsPositionSafe(Vector3 pos)
    {
        foreach (var occupied in _occupiedPositions)
        {
            if (Vector3.Distance(pos, occupied) < _safeRadius) return false;
        }
        return true;
    }

    // 트롤 종료 이벤트 수신 -> 다음 타이머 시작
    private void HandleTrollFinished()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        StartWaitTimer();
    }

    // 게가 죽거나 치워졌을 때 자리를 반납하는 함수
    private void ReleasePosition(Vector3 pos)
    {
        _occupiedPositions.RemoveAll(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pos.x, pos.z)) < 0.5f);
    }

    // 싱클톤 설정
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartTrollingSystem();
    }

    void Update()
    {
        // 방장이 아니면 타이머 로직을 아예 실행하지 않음
        if (!PhotonNetwork.IsMasterClient) return;

        if (!_isWaiting) return;

        _currentWaitTimer -= Time.deltaTime;

        if (_currentWaitTimer <= 0f)
        {
            _isWaiting = false;
            ExecuteRandomTroll();
        }
    }

    // 트롤 실행
    private void ExecuteRandomTroll()
    {
        // 다시 한 번 방장 권한 체크 (안전장치)
        if (!PhotonNetwork.IsMasterClient) return;

        TrollData selectedTroll = GetRandomTroll();
        _lastTroll = selectedTroll.trollType;
        _executionCount++;

        Debug.Log($"🚨 [트롤링 매니저] {_executionCount}번째 이벤트: {selectedTroll.trollType} 발생!");

        // 트롤 생성
        if (selectedTroll.trollPrefabPath != null)
        {
            string prefabName = selectedTroll.trollPrefabPath; // 예: "Trolls/Kraken"
            Vector3 spawnPos;

            if (selectedTroll.trollType == TrollType.Kraken || selectedTroll.trollType == TrollType.Siren)
            {
                // 몬스터 트롤은 몬스터 스폰 포인트에서 랜덤으로 생성
                int spawnIndex = Random.Range(0, _monsterSpawnPoints.Length);
                spawnPos = _monsterSpawnPoints[spawnIndex].position;
            }
            else
            {
                // 일반 트롤은 지상 스폰 포인트에서 랜덤으로 생성
                int spawnIndex = Random.Range(0, _groundSpawnPoints.Length);
                spawnPos = _groundSpawnPoints[spawnIndex].position;
            }

            // 🟢 NetworkGameManager를 통한 동기화 소환
            NetworkGameManager.Instance.SpawnNetworkObject(prefabName, spawnPos, Quaternion.identity);
            Debug.Log($"🚨 [Master] {selectedTroll.trollType} 네트워크 소환 요청");
        }
    }

    // 가중치 기반 랜덤 선택 (연속 발생 금지 적용)
    private TrollData GetRandomTroll()
    {
        int totalWeight = 0;
        List<TrollData> validTrolls = new List<TrollData>();

        // 이전 트롤을 제외한 나머지 트롤들의 총 가중치 계산
        foreach (var data in _trollDatabase)
        {
            if (data.trollType != _lastTroll)
            {
                validTrolls.Add(data);
                totalWeight += data.spawnWeight;
            }
        }

        // 랜덤 값 뽑기
        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var data in validTrolls)
        {
            currentWeight += data.spawnWeight;
            if (randomValue < currentWeight)
            {
                return data;
            }
        }

        return validTrolls[0]; // 혹시 모를 예외 방지
    }

    // 🟢 게임 시작 시(선공 플레이어 턴 시작 시) 호출
    public void StartTrollingSystem()
    {
        // 🟢 시스템 시작 자체를 방장만 통제
        if (!PhotonNetwork.IsMasterClient) return;

        _executionCount = 0;
        _lastTroll = TrollType.None;
        
        StartWaitTimer();
    }

    // 게임 종료 시 호출
    public void StopTrollingSystem()
    {
        _isWaiting = false;
        // 필요하다면 현재 씬에 있는 트롤들을 일괄 파괴하는 로직 추가
    }

    // 🟢 실행 횟수에 따른 타이머 설정
    private void StartWaitTimer()
    {
        float waitTime = CalculateWaitTime(_executionCount + 1); // 다음 실행될 회차 기준
        _currentWaitTimer = waitTime;
        _isWaiting = true;

        Debug.Log($"⏳ [트롤링 매니저] 대기 타이머 시작: {waitTime}초 뒤 다음 이벤트 발생");
    }

    // 실행 횟수에 따른 대기 시간 계산 함수
    private float CalculateWaitTime(int nextCount)
    {
        if (nextCount <= 0) return 5f; // 초기값
        if (nextCount <= 5) return 10f;
        if (nextCount <= 10) return 8f;
        if (nextCount <= 14) return 6f;
        if (nextCount <= 18) return 4f;
        return 2f; // 19회 이상 최소 대기 시간
    }
}
