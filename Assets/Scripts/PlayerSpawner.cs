using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NetworkManager 오브젝트 또는 영구 매니저에 상주하며,
/// GameScene 로딩 완료 시 각 클라이언트의 월드 캐릭터(PlayerCharacter)를 스폰하고
/// 해당 클라이언트의 NetworkPlayer(플레이어 세션)에 빙의(Possess)시키는 스포너 매니저입니다.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("월드에 스폰될 플레이어 캐릭터 프리팹 (PlayerCharacter 컴포넌트 포함)")]
    [SerializeField] private GameObject _characterPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    private readonly HashSet<ulong> _spawnedClients = new HashSet<ulong>();
    private bool _isSubscribed;

    public static PlayerSpawner Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

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

        // 연결 끊김 시 해당 클라이언트의 캐릭터 즉시 디스폰 (기획 예외케이스 4)
        NetworkPlayer player = NetworkPlayer.GetPlayer(clientId);
        if (player != null && player.HasCharacter)
        {
            PlayerCharacter character = player.CurrentCharacter;
            player.UnpossessCharacter();
            if (character != null && character.NetworkObject != null && character.NetworkObject.IsSpawned)
            {
                character.NetworkObject.Despawn(true);
                Debug.Log($"[PlayerSpawner] 클라이언트 {clientId} 연결 끊김으로 캐릭터를 즉시 디스폰했습니다.");
            }
        }
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
                SpawnCharacterForClient(sceneEvent.ClientId);
            }
            // 모든 클라이언트가 GameScene 로드를 마쳤을 때
            else if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                Debug.Log("[PlayerSpawner] 모든 클라이언트의 GameScene LoadEventCompleted 수신");
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    SpawnCharacterForClient(client.ClientId);
                }
            }
        }
    }

    /// <summary>
    /// 지정된 클라이언트를 위한 PlayerCharacter를 생성하고, NetworkPlayer 세션에 빙의(Possess)시킵니다.
    /// </summary>
    public void SpawnCharacterForClient(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (_spawnedClients.Contains(clientId))
        {
            return;
        }

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            return;
        }

        NetworkPlayer sessionPlayer = NetworkPlayer.GetPlayer(clientId);
        if (sessionPlayer == null)
        {
            // NetworkManager.ConnectedClients[clientId].PlayerObject로부터 탐색 fallback
            var clientObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            if (clientObj != null)
            {
                sessionPlayer = clientObj.GetComponent<NetworkPlayer>();
            }
        }

        // 이미 살아있는 캐릭터를 조종 중이면 중복 스폰 방지
        if (sessionPlayer != null && sessionPlayer.HasCharacter)
        {
            _spawnedClients.Add(clientId);
            return;
        }

        EnsureCharacterPrefab();
        if (_characterPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] CharacterPrefab이 할당되지 않았습니다!");
            return;
        }

        Scene targetScene = SceneManager.GetSceneByName("GameScene");
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogWarning("[PlayerSpawner] GameScene이 아직 유효하지 않거나 로드되지 않아 스폰을 대기합니다.");
            return;
        }

        // 기획 확정: 기본 스폰 포인트에서 스폰
        Vector3 spawnPosition = GetSpawnPosition(_spawnedClients.Count);
        Quaternion spawnRotation = Quaternion.identity;

        GameObject characterInstance = Instantiate(_characterPrefab, spawnPosition, spawnRotation);
        SceneManager.MoveGameObjectToScene(characterInstance, targetScene);

        var networkObject = characterInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // 캐릭터 스폰 시 소유권을 해당 클라이언트로 지정
            networkObject.SpawnWithOwnership(clientId, true);
            _spawnedClients.Add(clientId);

            var playerCharacter = characterInstance.GetComponent<PlayerCharacter>();
            if (playerCharacter != null && sessionPlayer != null)
            {
                sessionPlayer.PossessCharacter(playerCharacter);
            }

            Debug.Log($"[PlayerSpawner] 클라이언트 {clientId}의 캐릭터 스폰 및 빙의 완료 (위치: {spawnPosition})");

            // 세이브된 인벤토리 등 적용
            if (SaveLoadManager.Instance != null && sessionPlayer != null)
            {
                SaveLoadManager.Instance.ApplySaveDataToPlayer(clientId, sessionPlayer.NetworkObject);
            }
        }
        else
        {
            Debug.LogError("[PlayerSpawner] 생성된 CharacterPrefab에 NetworkObject 컴포넌트가 없습니다!");
        }
    }

    private void EnsureCharacterPrefab()
    {
        if (_characterPrefab != null)
        {
            return;
        }

        _characterPrefab = Resources.Load<GameObject>("PlayerDummyPrefab");
        if (_characterPrefab == null)
        {
            _characterPrefab = Resources.Load<GameObject>("PlayerCharacterPrefab");
        }
        if (_characterPrefab == null)
        {
            _characterPrefab = Resources.Load<GameObject>("PlayerPrefab");
        }

#if UNITY_EDITOR
        if (_characterPrefab == null)
        {
            _characterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerDummyPrefab.prefab");
            if (_characterPrefab == null)
            {
                _characterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerCharacterPrefab.prefab");
            }
            if (_characterPrefab == null)
            {
                _characterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab");
            }
        }
#endif
    }

    public void ClearClientSpawnState(ulong clientId)
    {
        _spawnedClients.Remove(clientId);
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
