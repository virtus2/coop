using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CoopGame.SaveSystem;
#if !UNITY_SERVER || UNITY_EDITOR
using Steamworks;
using RaybelCreation.Netcode.Transports.Steam;
#endif

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }
    public bool IsLoaded => _isLoaded;

    private string _currentSlotName = "Slot_1";
    private GameSaveData _currentSaveData = new GameSaveData();
    private readonly Dictionary<ulong, string> _clientIdToPlayerId = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, PlayerTransformData> _lastKnownTransforms = new Dictionary<ulong, PlayerTransformData>();
    private bool _isLoaded = false;
    private bool _isSubscribed = false;

    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
    private string CurrentSaveFilePath => Path.Combine(SaveDirectory, $"{_currentSlotName}.json");

    public struct PlayerTransformData
    {
        public Vector3 position;
        public Vector3 rotation;
        public float cameraPitch;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
        }
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SubscribeToNetworkEvents()
    {
        if (_isSubscribed || NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnect;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        _isSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_isSubscribed || NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnect;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        _isSubscribed = false;
    }

    private void HandleClientConnect(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        string pId = GetPlayerId(clientId);
        Debug.Log($"[SaveLoadManager] 클라이언트 연결됨. ClientId: {clientId}, PlayerId: {pId}");

        if (_isLoaded)
        {
            var data = _currentSaveData.players.Find(p => p.playerId == pId);
            if (data != null)
            {
                _lastKnownTransforms[clientId] = new PlayerTransformData
                {
                    position = data.position,
                    rotation = data.rotation,
                    cameraPitch = data.cameraPitch
                };
            }
        }
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

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

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            lastPos = client.PlayerObject.transform.position;
            lastRot = client.PlayerObject.transform.eulerAngles;
            var pc = client.PlayerObject.GetComponent<PlayerCharacter>();
            if (pc != null) lastPitch = pc.CameraPitch;
            hasState = true;
        }
        else if (_lastKnownTransforms.TryGetValue(clientId, out var known))
        {
            lastPos = known.position;
            lastRot = known.rotation;
            lastPitch = known.cameraPitch;
            hasState = true;
        }

        if (hasState)
        {
            _ = SavePlayerStateAsync(clientId, lastPos, lastRot, lastPitch);
        }

        _clientIdToPlayerId.Remove(clientId);
        _lastKnownTransforms.Remove(clientId);
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
            NetworkObject targetObj = playerObj;
            var netPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (netPlayer != null && netPlayer.CurrentCharacter != null)
            {
                targetObj = netPlayer.CurrentCharacter.NetworkObject;
            }

            Debug.Log($"[SaveLoadManager] 플레이어({pId}, ClientId: {clientId})의 저장된 상태를 서버에서 복원합니다: 위치 {data.position}");

            var playerCharacter = targetObj.GetComponent<PlayerCharacter>();
            if (playerCharacter != null)
            {
                Quaternion rot = Quaternion.Euler(data.rotation);
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    playerCharacter.Teleport(data.position, rot, data.cameraPitch);
                }
                else
                {
                    var clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                    };
                    playerCharacter.TeleportClientRpc(data.position, rot, data.cameraPitch, clientRpcParams);
                }
            }
            else
            {
                var cc = targetObj.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                targetObj.transform.position = data.position;
                targetObj.transform.eulerAngles = data.rotation;
                if (cc != null) cc.enabled = true;
            }
        }
    }

    public bool TryGetSavedTransform(ulong clientId, out Vector3 position, out Vector3 rotation, out float cameraPitch)
    {
        position = Vector3.zero;
        rotation = Vector3.zero;
        cameraPitch = 0f;

        string pId = GetPlayerId(clientId);
        var data = _currentSaveData.players.Find(p => p.playerId == pId);
        if (data != null)
        {
            position = data.position;
            rotation = data.rotation;
            cameraPitch = data.cameraPitch;
            return true;
        }
        return false;
    }

    public string GetPlayerId(ulong clientId)
    {
        bool isSteamTransport = NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig.NetworkTransport is SteamNetworkTransport;

        if (_clientIdToPlayerId.TryGetValue(clientId, out string cachedId) && !string.IsNullOrEmpty(cachedId))
        {
            if (!(isSteamTransport && cachedId.StartsWith("local_")))
            {
                return cachedId;
            }
        }

#if !UNITY_SERVER || UNITY_EDITOR
        if (SteamManager.Initialized)
        {
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                string hostSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
                _clientIdToPlayerId[clientId] = hostSteamId;
                return hostSteamId;
            }

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

    public async Task AutoSaveGameAsync()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        Debug.Log("[SaveLoadManager] 자동 저장을 시작합니다..");
        await SaveGameAsync();
    }

    public async Task SaveGameAsync()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        _currentSaveData.version = "1.1.0";
        _currentSaveData.timestamp = System.DateTime.Now.ToString("o");
        _currentSaveData.slotName = _currentSlotName;

        // Save Players
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            ulong cId = kvp.Key;
            NetworkClient client = kvp.Value;
            Vector3 pos = Vector3.zero;
            Vector3 rot = Vector3.zero;
            float pitch = 0f;
            float health = 100f;
            bool isDead = false;

            if (client.PlayerObject != null)
            {
                var targetObj = client.PlayerObject;
                var netPlayer = targetObj.GetComponent<NetworkPlayer>();
                if (netPlayer != null && netPlayer.CurrentCharacter != null)
                {
                    targetObj = netPlayer.CurrentCharacter.NetworkObject;
                }
                
                pos = targetObj.transform.position;
                rot = targetObj.transform.eulerAngles;
                var pCtrl = targetObj.GetComponent<PlayerCharacter>();
                if (pCtrl != null) pitch = pCtrl.CameraPitch;
                
                // var playerChar = targetObj.GetComponent<PlayerCharacter>();
                // if (playerChar != null) { health = playerChar.Health; isDead = playerChar.IsDead; }
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
                existing.health = health;
                existing.isDead = isDead;
            }
            else
            {
                _currentSaveData.players.Add(new PlayerSaveData
                {
                    playerId = pId,
                    position = pos,
                    rotation = rot,
                    cameraPitch = pitch,
                    health = health,
                    isDead = isDead
                });
            }
        }

        SaveMapDeltaState();

        await SaveGameToDiskAsync(_currentSaveData);
    }

    private void SaveMapDeltaState()
    {
        // Add Map/World state save logic from the spec here (e.g. MapSeed, Destructible props)
        /*
        _currentSaveData.mapSeed = MapGenerator.Instance.CurrentSeed;
        if(WaveManager.Instance != null) {
            _currentSaveData.currentWave = WaveManager.Instance.CurrentWave;
        }
        */
    }

    public void SaveGame()
    {
        _ = SaveGameAsync();
    }

    private async Task SaveGameToDiskAsync(GameSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            using (StreamWriter writer = new StreamWriter(CurrentSaveFilePath, false))
            {
                await writer.WriteAsync(json);
            }
            Debug.Log($"[SaveLoadManager] 게임이 디스크에 저장되었습니다: {CurrentSaveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 저장 실패: {e.Message}");
        }
    }

    public void LoadGame()
    {
        if (!File.Exists(CurrentSaveFilePath))
        {
            Debug.Log("[SaveLoadManager] 기존 세이브 파일이 없습니다. 새로 시작합니다.");
            _isLoaded = true;
            return;
        }

        try
        {
            string json = File.ReadAllText(CurrentSaveFilePath);
            _currentSaveData = JsonUtility.FromJson<GameSaveData>(json) ?? new GameSaveData();
            _isLoaded = true;
            Debug.Log($"[SaveLoadManager] 세이브 파일을 동기 로드했습니다. 저장된 플레이어 수: {_currentSaveData.players.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 동기 로드 실패: {e.Message}");
        }
    }

    public async Task LoadGameAsync()
    {
        if (!File.Exists(CurrentSaveFilePath))
        {
            Debug.Log("[SaveLoadManager] 기존 세이브 파일이 없습니다. 새로 시작합니다.");
            _isLoaded = true;
            return;
        }

        try
        {
            string json;
            using (StreamReader reader = new StreamReader(CurrentSaveFilePath))
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

    public void SetCurrentSlot(string slotName)
    {
        _currentSlotName = slotName;
    }

    public GameSaveData GetCurrentSaveData()
    {
        return _currentSaveData;
    }

    public (bool exists, bool isCorrupted, GameSaveData data) GetSaveFileInfo(string slotName)
    {
        string path = Path.Combine(SaveDirectory, $"{slotName}.json");
        if (!File.Exists(path))
        {
            return (false, false, null);
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<GameSaveData>(json);
            if (data == null) return (true, true, null);
            return (true, false, data);
        }
        catch
        {
            return (true, true, null);
        }
    }

    public void DeleteSaveFile(string slotName)
    {
        string path = Path.Combine(SaveDirectory, $"{slotName}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void CreateNewSave(string slotName)
    {
        SetCurrentSlot(slotName);
        _currentSaveData = new GameSaveData { slotName = slotName };
        _isLoaded = true;
    }
}

