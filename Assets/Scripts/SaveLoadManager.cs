using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
#if !UNITY_SERVER || UNITY_EDITOR
using Steamworks;
using RaybelCreation.Netcode.Transports.Steam;
#endif

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }
    public bool IsLoaded => _isLoaded;

    private string _saveFilePath;
    private bool _isLoaded = false;

    [Serializable]
    public class PlayerSaveData
    {
        // SteamID 또는 로컬용 ID (예: "steam_123456789", "local_1")
        public string playerId; 
        public Vector3 position;
        public Vector3 rotation;
        public float cameraPitch;
    }

    public struct PlayerTransformData
    {
        public Vector3 position;
        public Vector3 rotation;
        public float cameraPitch;
    }

    [Serializable]
    public class GameSaveData
    {
        public List<PlayerSaveData> players = new List<PlayerSaveData>();
    }

    private GameSaveData _currentSaveData = new GameSaveData();
    private readonly Dictionary<ulong, string> _clientIdToPlayerId = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, PlayerTransformData> _lastKnownTransforms = new Dictionary<ulong, PlayerTransformData>();
    private bool _isSubscribed = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _saveFilePath = Path.Combine(Application.persistentDataPath, "gamesave.json");
    }

    private void Start()
    {
        SubscribeNetworkEvents();
    }

    private void OnEnable()
    {
        SubscribeNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeNetworkEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeNetworkEvents();
    }

    private void SubscribeNetworkEvents()
    {
        if (_isSubscribed || NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        _isSubscribed = true;

        if (NetworkManager.Singleton.IsServer)
        {
            LoadGame();
        }
    }

    private void UnsubscribeNetworkEvents()
    {
        if (!_isSubscribed || NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        _isSubscribed = false;
    }

    private void HandleServerStarted()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            LoadGame();
            // 서버 본인(호스트) ID 캐싱
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            string localPlayerId = GetPlayerId(localClientId);
            _clientIdToPlayerId[localClientId] = localPlayerId;
        }
    }

    private void Update()
    {
        // 서버에서 활성 플레이어의 위치, 회전, 카메라 피치를 주기적으로 캐싱해둠 (디스커넥트 시 정확한 마지막 상태 확보)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                if (kvp.Value.PlayerObject != null)
                {
                    var playerObj = kvp.Value.PlayerObject;
                    var playerCtrl = playerObj.GetComponent<PlayerController>();
                    _lastKnownTransforms[kvp.Key] = new PlayerTransformData
                    {
                        position = playerObj.transform.position,
                        rotation = playerObj.transform.eulerAngles,
                        cameraPitch = playerCtrl != null ? playerCtrl.CameraPitch : 0f
                    };
                }
            }
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        string pId = GetPlayerId(clientId);
        _clientIdToPlayerId[clientId] = pId;
        Debug.Log($"[SaveLoadManager] 클라이언트 연결됨. ClientId: {clientId}, PlayerId: {pId}");

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                ApplySaveDataToPlayer(clientId, client.PlayerObject);
            }
        }
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // 호스트 본인이 셧다운될 때(서버 종료)는 일괄 저장 로직에서 처리하므로 개별 핸들러는 제외
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            _clientIdToPlayerId.Remove(clientId);
            _lastKnownTransforms.Remove(clientId);
            return;
        }

        Debug.Log($"[SaveLoadManager] 클라이언트 {clientId} 연결 종료 감지. 상태를 저장합니다.");

        Vector3 lastPos = Vector3.zero;
        Vector3 lastRot = Vector3.zero;
        float lastPitch = 0f;
        bool hasState = false;

        // 1. 현재 PlayerObject가 아직 살아있는지 확인
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            var pObj = client.PlayerObject;
            var pCtrl = pObj.GetComponent<PlayerController>();
            lastPos = pObj.transform.position;
            lastRot = pObj.transform.eulerAngles;
            lastPitch = pCtrl != null ? pCtrl.CameraPitch : 0f;
            hasState = true;
        }
        // 2. 이미 오브젝트가 정리되었을 경우 캐싱된 마지막 상태 사용
        else if (_lastKnownTransforms.TryGetValue(clientId, out var cachedData))
        {
            lastPos = cachedData.position;
            lastRot = cachedData.rotation;
            lastPitch = cachedData.cameraPitch;
            hasState = true;
        }

        if (hasState)
        {
            _ = SavePlayerStateAsync(clientId, lastPos, lastRot, lastPitch);
        }

        _clientIdToPlayerId.Remove(clientId);
        _lastKnownTransforms.Remove(clientId);
    }

    /// <summary>
    /// 세이브 파일에 해당 클라이언트의 저장된 위치, 회전, 카메라 피치가 있는지 확인하고 반환합니다.
    /// </summary>
    public bool TryGetSavedTransform(ulong clientId, out Vector3 position, out Quaternion rotation, out float cameraPitch)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            cameraPitch = 0f;
            return false;
        }

        if (!_isLoaded)
        {
            LoadGame();
        }

        string pId = GetPlayerId(clientId);
        var data = _currentSaveData.players.Find(p => p.playerId == pId);
        if (data != null)
        {
            position = data.position;
            rotation = Quaternion.Euler(data.rotation);
            cameraPitch = data.cameraPitch;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        cameraPitch = 0f;
        return false;
    }

    /// <summary>
    /// 세이브 파일에 해당 클라이언트의 저장된 위치가 있는지 확인하고 반환합니다. (기존 호환용)
    /// </summary>
    public bool TryGetSavedPosition(ulong clientId, out Vector3 position)
    {
        return TryGetSavedTransform(clientId, out position, out _, out _);
    }

    public void ApplySaveDataToPlayer(ulong clientId, NetworkObject playerObj)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || playerObj == null) return;

        if (!_isLoaded)
        {
            LoadGame();
        }

        string pId = GetPlayerId(clientId);
        var data = _currentSaveData.players.Find(p => p.playerId == pId);

        if (data != null)
        {
            // 서버 측 CharacterController 일시 비활성화 후 위치 및 회전 설정
            var cc = playerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerObj.transform.position = data.position;
            playerObj.transform.rotation = Quaternion.Euler(data.rotation);

            if (cc != null) cc.enabled = true;

            _lastKnownTransforms[clientId] = new PlayerTransformData
            {
                position = data.position,
                rotation = data.rotation,
                cameraPitch = data.cameraPitch
            };

            Debug.Log($"[SaveLoadManager] 플레이어({pId}, ClientId: {clientId})의 저장된 상태를 서버에서 설정했습니다: 위치 {data.position}, 각도 {data.rotation}, 카메라 {data.cameraPitch}");

            // 소유 클라이언트에게 텔레포트 명령 (ClientNetworkTransform 클라이언트 권한 동기화)
            var playerController = playerObj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                Quaternion rot = Quaternion.Euler(data.rotation);
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    // 서버 본인(호스트)인 경우 로컬에서 즉시 Teleport 실행
                    playerController.Teleport(data.position, rot, data.cameraPitch);
                }
                else
                {
                    // 원격 클라이언트인 경우 해당 클라이언트에게 ClientRpc로 Teleport 지시
                    var clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { clientId }
                        }
                    };
                    playerController.TeleportClientRpc(data.position, rot, data.cameraPitch, clientRpcParams);
                }
            }
        }
        else
        {
            Debug.Log($"[SaveLoadManager] 플레이어({pId}, ClientId: {clientId})의 기존 세이브 데이터가 없습니다.");
        }
    }

    /// <summary>
    /// 특정 클라이언트 한 명의 위치, 각도, 카메라 피치를 세이브 데이터에 갱신하고 디스크에 저장합니다.
    /// </summary>
    public async Task SavePlayerStateAsync(ulong clientId, Vector3 position, Vector3 rotation, float cameraPitch)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        string pId = GetPlayerId(clientId);
        var existing = _currentSaveData.players.Find(p => p.playerId == pId);
        if (existing != null)
        {
            existing.position = position;
            existing.rotation = rotation;
            existing.cameraPitch = cameraPitch;
        }
        else
        {
            _currentSaveData.players.Add(new PlayerSaveData
            {
                playerId = pId,
                position = position,
                rotation = rotation,
                cameraPitch = cameraPitch
            });
        }

        Debug.Log($"[SaveLoadManager] 플레이어({pId}) 상태 갱신 (위치: {position}, 각도: {rotation}, 카메라: {cameraPitch}). 디스크에 저장합니다.");
        await SaveGameToDiskAsync(_currentSaveData);
    }

    public async Task SavePlayerStateAsync(ulong clientId, Vector3 position)
    {
        Vector3 rot = Vector3.zero;
        float pitch = 0f;
        if (_lastKnownTransforms.TryGetValue(clientId, out var t))
        {
            rot = t.rotation;
            pitch = t.cameraPitch;
        }
        await SavePlayerStateAsync(clientId, position, rot, pitch);
    }

    /// <summary>
    /// 현재 접속 중인 모든 플레이어(호스트 및 클라이언트)의 위치, 각도, 카메라 피치를 저장합니다.
    /// </summary>
    public async Task SaveGameAsync()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            ulong cId = kvp.Key;
            NetworkClient client = kvp.Value;
            Vector3 pos;
            Vector3 rot;
            float pitch;

            if (client.PlayerObject != null)
            {
                var pObj = client.PlayerObject;
                var pCtrl = pObj.GetComponent<PlayerController>();
                pos = pObj.transform.position;
                rot = pObj.transform.eulerAngles;
                pitch = pCtrl != null ? pCtrl.CameraPitch : 0f;
            }
            else if (_lastKnownTransforms.TryGetValue(cId, out var cachedData))
            {
                pos = cachedData.position;
                rot = cachedData.rotation;
                pitch = cachedData.cameraPitch;
            }
            else
            {
                continue;
            }

            string pId = GetPlayerId(cId);
            var existing = _currentSaveData.players.Find(p => p.playerId == pId);
            if (existing != null)
            {
                existing.position = pos;
                existing.rotation = rot;
                existing.cameraPitch = pitch;
            }
            else
            {
                _currentSaveData.players.Add(new PlayerSaveData
                {
                    playerId = pId,
                    position = pos,
                    rotation = rot,
                    cameraPitch = pitch
                });
            }
        }

        await SaveGameToDiskAsync(_currentSaveData);
    }

    public void SaveGame()
    {
        _ = SaveGameAsync();
    }

    public string GetPlayerId(ulong clientId)
    {
        bool isSteamTransport = NetworkManager.Singleton != null &&
                                NetworkManager.Singleton.NetworkConfig.NetworkTransport is SteamNetworkTransport;

        if (_clientIdToPlayerId.TryGetValue(clientId, out string cachedId) && !string.IsNullOrEmpty(cachedId))
        {
            // SteamTransport 환경인데 이전에 local_ 로 임시 저장되었던 것이라면 재조회 허용
            if (!(isSteamTransport && cachedId.StartsWith("local_")))
            {
                return cachedId;
            }
        }

#if !UNITY_SERVER || UNITY_EDITOR
        if (SteamManager.Initialized)
        {
            // 호스트(서버 본인)인 경우 로컬 SteamID 사용
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                string hostSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
                _clientIdToPlayerId[clientId] = hostSteamId;
                return hostSteamId;
            }

            // 다른 클라이언트인 경우 트랜스포트를 통해 SteamID 획득 시도
            if (NetworkManager.Singleton != null)
            {
                var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as SteamNetworkTransport;
                if (transport != null)
                {
                    ulong transportId = NetworkManager.Singleton.GetTransportIdFromClientId(clientId);
                    if (transportId != ulong.MaxValue && transport.TryGetAuthenticatedRemoteSteamId(transportId, out ulong steamId))
                    {
                        string clientSteamId = steamId.ToString();
                        _clientIdToPlayerId[clientId] = clientSteamId;
                        return clientSteamId;
                    }
                }
            }
        }
#endif
        string fallbackId = "local_" + clientId.ToString();
        _clientIdToPlayerId[clientId] = fallbackId;
        return fallbackId;
    }

    private async Task SaveGameToDiskAsync(GameSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            using (StreamWriter writer = new StreamWriter(_saveFilePath, false))
            {
                await writer.WriteAsync(json);
            }
            Debug.Log($"[SaveLoadManager] 게임이 디스크에 저장되었습니다: {_saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 저장 실패: {e.Message}");
        }
    }

    public void LoadGame()
    {
        if (!File.Exists(_saveFilePath))
        {
            Debug.Log("[SaveLoadManager] 기존 세이브 파일이 없습니다. 새로 시작합니다.");
            _isLoaded = true;
            return;
        }

        try
        {
            string json = File.ReadAllText(_saveFilePath);
            _currentSaveData = JsonUtility.FromJson<GameSaveData>(json) ?? new GameSaveData();
            _isLoaded = true;
            Debug.Log($"[SaveLoadManager] 세이브 파일을 동기 로드했습니다. 저장된 플레이어 수: {_currentSaveData.players.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 동기 로드 실패: {e.Message}");
        }
    }

    private async Task LoadGameAsync()
    {
        if (!File.Exists(_saveFilePath))
        {
            Debug.Log("[SaveLoadManager] 기존 세이브 파일이 없습니다. 새로 시작합니다.");
            _isLoaded = true;
            return;
        }

        try
        {
            string json;
            using (StreamReader reader = new StreamReader(_saveFilePath))
            {
                json = await reader.ReadToEndAsync();
            }

            _currentSaveData = JsonUtility.FromJson<GameSaveData>(json) ?? new GameSaveData();
            _isLoaded = true;
            Debug.Log($"[SaveLoadManager] 세이브 파일을 비동기 로드했습니다. 저장된 플레이어 수: {_currentSaveData.players.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 비동기 로드 실패: {e.Message}");
        }
    }
}
