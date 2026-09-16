using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Steamworks;

/// <summary>
/// NetworkManager GO에 붙어있는 로비 상태 동기화 담당 (NetworkBehaviour)
/// UI 표현은 LobbyUIController에서 담당
/// </summary>
public class LobbyNetworkSync : NetworkBehaviour
{
    public static LobbyNetworkSync Instance { get; private set; }

    // 외부에서 구독할 수 있는 이벤트
    public event Action OnLobbyStateChanged;

    public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
    {
        public ulong ClientId;
        public FixedString32Bytes PlayerName;
        public bool IsReady;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsReady);
        }

        public bool Equals(LobbyPlayerState other) =>
            ClientId == other.ClientId && PlayerName == other.PlayerName && IsReady == other.IsReady;
    }

    private NetworkList<LobbyPlayerState> _players;

    // 외부 읽기 전용
    public int PlayerCount => _players?.Count ?? 0;
    public LobbyPlayerState GetPlayer(int index) => _players[index];
    public bool AllReady
    {
        get
        {
            if (_players == null || _players.Count == 0) return false;
            foreach (var p in _players)
                if (!p.IsReady) return false;
            return true;
        }
    }

    // static 변수는 Domain Reload 비활성화 대응
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        _players = new NetworkList<LobbyPlayerState>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        _players.OnListChanged += _ => OnLobbyStateChanged?.Invoke();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            AddPlayer(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            RegisterSelfServerRpc(GetMyName());
        }

        OnLobbyStateChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        _players.OnListChanged -= _ => OnLobbyStateChanged?.Invoke();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer) AddPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].ClientId == clientId) { _players.RemoveAt(i); return; }
        }
    }

    private void AddPlayer(ulong clientId)
    {
        _players.Add(new LobbyPlayerState { ClientId = clientId, PlayerName = "...", IsReady = false });
    }

    private string GetMyName()
    {
        if (SteamManager.Initialized) return SteamFriends.GetPersonaName();
        return "Player " + NetworkManager.Singleton.LocalClientId;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterSelfServerRpc(string name, ServerRpcParams p = default)
    {
        ulong id = p.Receive.SenderClientId;
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].ClientId == id)
            {
                var s = _players[i]; s.PlayerName = name; _players[i] = s; return;
            }
        }
        // 아직 안 들어온 경우 추가
        _players.Add(new LobbyPlayerState { ClientId = id, PlayerName = name, IsReady = false });
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleReadyServerRpc(ServerRpcParams p = default)
    {
        ulong id = p.Receive.SenderClientId;
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].ClientId == id)
            {
                var s = _players[i]; s.IsReady = !s.IsReady; _players[i] = s; return;
            }
        }
    }

    private bool _isStartingGame = false;

    public void StartGame()
    {
        if (!IsServer || !AllReady || _isStartingGame) return;
        _isStartingGame = true;

        StartGameTransitionClientRpc();
        StartCoroutine(StartGameWithFadeRoutine());
    }

    [ClientRpc]
    private void StartGameTransitionClientRpc()
    {
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.StartGameTransition(1.5f, 1.5f);
        }
    }

    private System.Collections.IEnumerator StartGameWithFadeRoutine()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
