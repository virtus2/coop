using UnityEngine;
using Unity.Netcode;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
#if !UNITY_SERVER || UNITY_EDITOR
using Steamworks;
using RaybelCreation.Netcode.Transports.Steam;
#endif

public class SaveLoadManager : NetworkBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    private string _saveFilePath;

    [Serializable]
    public class PlayerSaveData
    {
        // SteamID 또는 로컬용 ID (예: "steam_123456789", "local_1")
        public string playerId; 
        public Vector3 position;
    }

    [Serializable]
    public class GameSaveData
    {
        public List<PlayerSaveData> players = new List<PlayerSaveData>();
    }

    private GameSaveData _currentSaveData = new GameSaveData();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _saveFilePath = Path.Combine(Application.persistentDataPath, "gamesave.json");
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _ = LoadGameAsync();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                ApplySaveDataToPlayer(clientId, client.PlayerObject);
            }
        }
    }

    public void ApplySaveDataToPlayer(ulong clientId, NetworkObject playerObj)
    {
        if (!IsServer) return;

        string pId = GetPlayerId(clientId);
        var data = _currentSaveData.players.Find(p => p.playerId == pId);
        
        if (data != null)
        {
            playerObj.transform.position = data.position;
            Debug.Log($"[SaveLoadManager] 접속한 클라이언트({pId})의 위치를 로드했습니다: {data.position}");
        }
    }

    public async void SaveGame()
    {
        if (!IsServer) return;

        _currentSaveData.players.Clear();

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            ulong clientId = kvp.Key;
            NetworkClient client = kvp.Value;
            
            if (client.PlayerObject != null)
            {
                _currentSaveData.players.Add(new PlayerSaveData
                {
                    playerId = GetPlayerId(clientId),
                    position = client.PlayerObject.transform.position
                });
            }
        }

        await SaveGameToDiskAsync(_currentSaveData);
    }

    private string GetPlayerId(ulong clientId)
    {
#if !UNITY_SERVER || UNITY_EDITOR
        // 에디터 테스트거나 스팀 미초기화 시 임시 ID 반환
        if (!SteamManager.Initialized)
        {
            return "local_" + clientId.ToString();
        }

        // 호스트(서버 본인)인 경우 로컬 SteamID 사용
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            return SteamUser.GetSteamID().m_SteamID.ToString();
        }

        // 다른 클라이언트인 경우 트랜스포트를 통해 SteamID 획득 시도
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as SteamNetworkTransport;
        if (transport != null)
        {
            if (transport.TryGetAuthenticatedRemoteSteamId(clientId, out ulong steamId))
            {
                return steamId.ToString();
            }
        }
#endif
        // 기타 실패 시 임시 ID
        return "local_" + clientId.ToString();
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
            Debug.Log($"[SaveLoadManager] 게임이 비동기로 저장되었습니다. 경로: {_saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 저장 실패: {e.Message}");
        }
    }

    private async Task LoadGameAsync()
    {
        if (!File.Exists(_saveFilePath))
        {
            Debug.Log("[SaveLoadManager] 기존 세이브 파일이 없습니다. 새로 시작합니다.");
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
            Debug.Log($"[SaveLoadManager] 세이브 파일을 성공적으로 로드했습니다. 저장된 플레이어 수: {_currentSaveData.players.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 로드 실패: {e.Message}");
        }
    }
}
