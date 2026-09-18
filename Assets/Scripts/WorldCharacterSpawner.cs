using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 서버(IsServer)에서 월드 상의 몬스터 및 NPC를 생성(Spawn)하여 배치하는 스포너입니다.
/// 플레이어 스포너(PlayerSpawner)와 완전히 분리되어 동작하며,
/// 소유자(Owner)를 지정하지 않고 서버 객체(Server-Authoritative Object)로 스폰합니다.
/// </summary>
public class WorldCharacterSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnEntry
    {
        public string EntryName;
        public GameObject Prefab;
        public Transform SpawnPoint;
        public Vector3 DefaultPosition;
        public Quaternion DefaultRotation;
    }

    [Header("Spawn Configuration")]
    [SerializeField] private List<SpawnEntry> _characterSpawnList = new List<SpawnEntry>();
    [SerializeField] private bool _spawnOnServerStart = true;

    private readonly List<NetworkObject> _spawnedCharacters = new List<NetworkObject>();
    private bool _hasSpawned;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

            if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
            {
                HandleServerStarted();
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }
    }

    private void HandleServerStarted()
    {
        if (!NetworkManager.Singleton.IsServer || _hasSpawned)
        {
            return;
        }

        if (_spawnOnServerStart)
        {
            SpawnAllWorldCharacters();
        }
    }

    /// <summary>
    /// 등록된 모든 몬스터 및 NPC를 서버 권한 객체로 스폰합니다.
    /// </summary>
    public void SpawnAllWorldCharacters()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[WorldCharacterSpawner] 스폰은 오직 서버에서만 실행할 수 있습니다.");
            return;
        }

        if (_hasSpawned)
        {
            return;
        }

        _hasSpawned = true;

        foreach (var entry in _characterSpawnList)
        {
            if (entry.Prefab == null)
            {
                continue;
            }

            Vector3 spawnPos = entry.SpawnPoint != null ? entry.SpawnPoint.position : entry.DefaultPosition;
            Quaternion spawnRot = entry.SpawnPoint != null ? entry.SpawnPoint.rotation : entry.DefaultRotation;

            GameObject instance = Instantiate(entry.Prefab, spawnPos, spawnRot);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                // [중요] 소유자 지정(SpawnWithOwnership) 없이 기본 Spawn 호출
                // 서버 권한(Server-Authoritative) 엔티티로 월드에 등록됨
                netObj.Spawn(destroyWithScene: true);
                _spawnedCharacters.Add(netObj);

                Debug.Log($"[WorldCharacterSpawner] 월드 엔티티 스폰 완료: {entry.EntryName} (위치: {spawnPos})");
            }
            else
            {
                Debug.LogError($"[WorldCharacterSpawner] 프리팹 '{entry.Prefab.name}'에 NetworkObject 컴포넌트가 없습니다!");
                Destroy(instance);
            }
        }
    }

    /// <summary>
    /// 스폰된 모든 월드 캐릭터를 서버에서 디스폰합니다.
    /// </summary>
    public void DespawnAllWorldCharacters()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (var netObj in _spawnedCharacters)
        {
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }

        _spawnedCharacters.Clear();
        _hasSpawned = false;
    }
}
