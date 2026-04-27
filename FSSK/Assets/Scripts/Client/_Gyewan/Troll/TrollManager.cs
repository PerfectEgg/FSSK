using UnityEngine;
using System.Collections.Generic;

public class TrollManager : MonoBehaviour
{
    public Transform[] groundSpawnPoints;   // 지상 스폰 포인트
    public Transform[] airSpawnPoints;      // 공중 스폰 포인트
    
    public GameObject spawnPrefab;
    public GameObject parrotPrefab;
    public GameObject ratPrefab;
    public GameObject seaCrabPrefab;
    public GameObject turtlePrefab;

    // 이벤트 구독 및 해제 (핵심!)
    private void OnEnable()
    {
        GameEvents.RequestSafePosition += GetSafeRandomPosition;
        GameEvents.OnPositionReleased += ReleasePosition;
    }

    private void OnDisable()
    {
        GameEvents.RequestSafePosition -= GetSafeRandomPosition;
        GameEvents.OnPositionReleased -= ReleasePosition;
    }

    private List<Vector3> occupiedPositions = new List<Vector3>();

    [SerializeField] private float safeRadius = 2.5f;       // 바다 게 최소 안전 거리

    public void OnSpawnTroll()
    {
        Instantiate(spawnPrefab, new Vector3(0, 0.25f, 0), Quaternion.identity);
    }

    // 겹치지 않는 안전한 랜덤 위치를 찾아 반환하는 함수
    public Vector3 GetSafeRandomPosition()
    {
        int maxAttempts = 8; 

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 randomPos = new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
            if (IsPositionSafe(randomPos))
            {
                occupiedPositions.Add(randomPos); 
                return randomPos;
            }
        }
        return new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
    }

    // 해당 위치가 다른 게들과 겹치는지 검사하는 함수
    private bool IsPositionSafe(Vector3 pos)
    {
        foreach (var occupied in occupiedPositions)
        {
            if (Vector3.Distance(pos, occupied) < safeRadius) return false;
        }
        return true;
    }

    // 게가 죽거나 치워졌을 때 자리를 반납하는 함수
    private void ReleasePosition(Vector3 pos)
    {
        occupiedPositions.RemoveAll(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pos.x, pos.z)) < 0.5f);
    }

    public void OnSpawnParrot()
    {
        if (parrotPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(parrotPrefab, groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + groundSpawnPoints[i].position);
    }

    public void OnSpawnRat()
    {
        if (ratPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(ratPrefab, groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + groundSpawnPoints[i].position);
    }

    public void OnSpawnSeaCrab()
    {
        if (seaCrabPrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(seaCrabPrefab, groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + groundSpawnPoints[i].position);
    }

    public void OnSpawnTurtle()
    {
        if (turtlePrefab == null)
        {
            Debug.Log("프리팹 없음");
            return;
        }

        int i = Random.Range(0, 4);
        Instantiate(turtlePrefab, groundSpawnPoints[i].position, Quaternion.identity);
        Debug.Log("생성 완료: " + groundSpawnPoints[i].position);
    }
}
