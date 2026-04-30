using UnityEngine;
using System.Collections.Generic;

public class TestManager : MonoBehaviour
{
    [SerializeField] private Transform[] _groundSpawnPoints;   // 지상 스폰 포인트
    [SerializeField] private Transform[] _airSpawnPoints;      // 공중 스폰 포인트
    [SerializeField] private Transform[] _monsterSpawnPoints;   // 크라켄 스폰 포인트
    
    [Header("동물")]
    [SerializeField] private GameObject _parrotPrefab;
    [SerializeField] private GameObject _ratPrefab;
    [SerializeField] private GameObject _seaCrabPrefab;
    [SerializeField] private float _safeRadius = 2.5f;       // 바다 게 최소 안전 거리
    [SerializeField] private GameObject _turtlePrefab;

    [Header("아이템")]
    [SerializeField] private GameObject _rumPrefab;

    [Header("몬스터")]
    [SerializeField] private GameObject _krakenPrefab;
    [SerializeField] private GameObject _sirenPrefab;

    // 이벤트 구독 및 해제 (핵심!)
    private void OnEnable()
    {
        TrollEvents.RequestSafePosition += GetSafeRandomPosition;
        TrollEvents.OnPositionReleased += ReleasePosition;
    }

    private void OnDisable()
    {
        TrollEvents.RequestSafePosition -= GetSafeRandomPosition;
        TrollEvents.OnPositionReleased -= ReleasePosition;
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

    // 게가 죽거나 치워졌을 때 자리를 반납하는 함수
    private void ReleasePosition(Vector3 pos)
    {
        _occupiedPositions.RemoveAll(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pos.x, pos.z)) < 0.5f);
    }

    public void OnSpawnParrot()
    {
        if (_parrotPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(_parrotPrefab, _groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + _groundSpawnPoints[i].position);
    }

    public void OnSpawnRat()
    {
        if (_ratPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(_ratPrefab, _groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + _groundSpawnPoints[i].position);
    }

    public void OnSpawnSeaCrab()
    {
        if (_seaCrabPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(_seaCrabPrefab, _groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + _groundSpawnPoints[i].position);
    }

    public void OnSpawnTurtle()
    {
        if (_turtlePrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(_turtlePrefab, _groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + _groundSpawnPoints[i].position);
    }

    public void OnSpawnRum()
    {
        if (_rumPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 2);
        Instantiate(_rumPrefab, _airSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + _airSpawnPoints[i].position);
    }

    public void OnSpawnKraken()
    {
        if (_krakenPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 2);
        Instantiate(_krakenPrefab, _monsterSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + _monsterSpawnPoints[i].position);
    }

    public void OnSpawnSiren()
    {
        if (_sirenPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 2);
        Instantiate(_sirenPrefab, _monsterSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + _monsterSpawnPoints[i].position);
    }
    
}
