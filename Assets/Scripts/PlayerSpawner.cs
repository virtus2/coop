using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NetworkManager 오브젝트 또는 영구 매니저에 상주하며,
/// GameScene 로딩 완료 시 각 클라이언트의 캐릭터(PlayerPrefab)를 스폰하는 매니저입니다.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    private readonly HashSet<ulong> _spawnedClients = new HashSet<ulong>();
    private bool _isSubscribed;

    private void Start()
    {
        SubscribeToEvents();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_isSubscribed)
        {
            return;
        }

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleSceneEvent;
                _isSubscribed = true;
            }
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
        }
    }

    private void HandleServerStarted()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null && !_isSubscribed)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleSceneEvent;
            _isSubscribed = true;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (!_isSubscribed)
        {
            return;
        }

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleSceneEvent;
            }
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }

        _isSubscribed = false;
        _spawnedClients.Clear();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        _spawnedClients.Remove(clientId);
    }

    private void HandleSceneEvent(SceneEvent sceneEvent)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // GameScene이 아닌 다른 씬으로 전환 시 스폰 기록 초기화
        if (sceneEvent.SceneEventType == SceneEventType.Load && sceneEvent.SceneName != "GameScene")
        {
            _spawnedClients.Clear();
            return;
        }

        if (sceneEvent.SceneName == "GameScene")
        {
            // 각 클라이언트가 GameScene 로드를 마쳤을 때
            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
            {
                Debug.Log($"[PlayerSpawner] 클라이언트 {sceneEvent.ClientId}의 GameScene LoadComplete 수신");
                SpawnPlayerForClient(sceneEvent.ClientId);
            }
            // 모든 클라이언트가 GameScene 로드를 마쳤을 때
            else if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                Debug.Log("[PlayerSpawner] 모든 클라이언트의 GameScene LoadEventCompleted 수신");
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    SpawnPlayerForClient(client.ClientId);
                }
            }
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (_spawnedClients.Contains(clientId))
        {
            return;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            return;
        }

        var client = NetworkManager.Singleton.ConnectedClients[clientId];
        if (client.PlayerObject != null)
        {
            _spawnedClients.Add(clientId);
            return;
        }

        if (_playerPrefab == null)
        {
            if (NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs.Count > 0)
            {
                _playerPrefab = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs[0].Prefab;
            }

            if (_playerPrefab == null)
            {
                _playerPrefab = Resources.Load<GameObject>("PlayerPrefab");
            }

            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] PlayerPrefab이 할당되지 않았습니다!");
                return;
            }
        }

        Scene targetScene = SceneManager.GetSceneByName("GameScene");
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogWarning("[PlayerSpawner] GameScene이 아직 유효하지 않거나 로드되지 않아 스폰을 대기합니다.");
            return;
        }

        Vector3 spawnPosition;
        Quaternion spawnRotation = Quaternion.identity;

        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.TryGetSavedTransform(clientId, out Vector3 savedPos, out Quaternion savedRot, out _))
        {
            spawnPosition = savedPos;
            spawnRotation = savedRot;
            Debug.Log($"[PlayerSpawner] 클라이언트 {clientId}의 세이브 위치 및 각도를 적용하여 스폰합니다 (위치: {spawnPosition}, 각도: {spawnRotation.eulerAngles})");
        }
        else
        {
            spawnPosition = GetSpawnPosition(_spawnedClients.Count);
            Debug.Log($"[PlayerSpawner] 클라이언트 {clientId}의 세이브 데이터가 없어 기본 위치를 사용합니다: {spawnPosition}");
        }

        GameObject playerInstance = Instantiate(_playerPrefab, spawnPosition, spawnRotation);
        SceneManager.MoveGameObjectToScene(playerInstance, targetScene);

        var networkObject = playerInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId, true);
            _spawnedClients.Add(clientId);
            Debug.Log($"[PlayerSpawner] 클라이언트 {clientId}의 플레이어 캐릭터 스폰 완료 (위치: {spawnPosition}, 각도: {spawnRotation.eulerAngles})");

            if (SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.ApplySaveDataToPlayer(clientId, networkObject);
            }
        }
        else
        {
            Debug.LogError("[PlayerSpawner] 생성된 PlayerPrefab에 NetworkObject 컴포넌트가 없습니다!");
        }
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            int pointIndex = index % _spawnPoints.Length;
            if (_spawnPoints[pointIndex] != null)
            {
                return _spawnPoints[pointIndex].position;
            }
        }

        // 기본 분산 스폰 위치 (바닥 위 Y = 1f)
        return new Vector3(index * 2.5f, 1f, 0f);
    }
}
