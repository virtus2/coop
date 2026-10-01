using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class EnemySpawner : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameObject dummyMonsterPrefab;
    
    [Header("Spawning Config")]
    [SerializeField] private float spawnInterval = 3f;
    
    public int ActiveEnemyCount { get; private set; }
    
    private bool isSpawning = false;
    private float spawnTimer = 0f;
    private int totalEnemiesToSpawn = 0;
    private int spawnedEnemies = 0;

    // 맵 생성 시 각 방에서 등록해줄 스폰 포인트들
    private List<Transform> roomSpawnPoints = new List<Transform>();

    public void RegisterSpawnPoint(Transform sp)
    {
        if (!roomSpawnPoints.Contains(sp)) roomSpawnPoints.Add(sp);
    }

    public void StartSpawning(int waveLevel)
    {
        if (!IsServer) return;
        isSpawning = true;
        spawnTimer = 0f;
        spawnedEnemies = 0;
        
        // 웨이브 레벨에 따른 스폰 수량 조정 (추후 기획에 맞춰 공식 변경 가능)
        totalEnemiesToSpawn = 10 + (waveLevel * 5); 
        Debug.Log($"[EnemySpawner] 웨이브 {waveLevel} 스폰 시작. 총 {totalEnemiesToSpawn}마리 예정.");
    }

    public void StopSpawning()
    {
        if (!IsServer) return;
        isSpawning = false;
    }

    private void Update()
    {
        if (!IsServer || !isSpawning) return;

        if (spawnedEnemies < totalEnemiesToSpawn)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                SpawnEnemy();
            }
        }
    }

    private void SpawnEnemy()
    {
        Vector3 spawnPos = GetOptimalSpawnPosition();
        
        GameObject enemy = Instantiate(dummyMonsterPrefab, spawnPos, Quaternion.identity);
        var netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }

        var monster = enemy.GetComponent<MonsterController>();
        if (monster != null)
        {
            ActiveEnemyCount++;
            monster.OnDied += HandleEnemyDeath;
            // 코어 타겟팅은 수정된 MonsterController 내부에서 ContainmentCore.Instance를 찾아 처리하도록 구성 예정
        }
        
        spawnedEnemies++;
    }

    private void HandleEnemyDeath()
    {
        ActiveEnemyCount--;
        if (ActiveEnemyCount < 0) ActiveEnemyCount = 0;
    }

    private Vector3 GetOptimalSpawnPosition()
    {
        if (ContainmentCore.Instance == null) return Vector3.zero;

        Vector3 corePos = ContainmentCore.Instance.transform.position;

        // 1. 등록된 방 스폰 포인트 중 가장 먼 곳 찾기
        if (roomSpawnPoints.Count > 0)
        {
            Transform furthest = null;
            float maxDist = 0f;
            foreach (var sp in roomSpawnPoints)
            {
                float dist = Vector3.SqrMagnitude(sp.position - corePos);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    furthest = sp;
                }
            }
            if (furthest != null) return furthest.position;
        }

        // 2. 스폰 포인트가 없다면 맵 외곽(예: 코어 반경 40m 밖) 임의의 위치 계산 (Fallback)
        Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
        Vector3 fallbackPos = corePos + new Vector3(randomDir.x * 40f, 0, randomDir.y * 40f);
        
        // NavMesh 위의 점으로 보정
        if (UnityEngine.AI.NavMesh.SamplePosition(fallbackPos, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }

        return fallbackPos;
    }
}
