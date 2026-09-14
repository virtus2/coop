using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using Steamworks;

public class LobbyManager : NetworkBehaviour
{
    [SerializeField] private Text[] _playerNameTexts;
    [SerializeField] private Text[] _playerReadyTexts;
    [SerializeField] private Button _readyButton;
    [SerializeField] private Button _startGameButton;
    [SerializeField] private GameObject _lobbyUIPanel;

    public struct LobbyPlayerState : INetworkSerializable, System.IEquatable<LobbyPlayerState>
    {
        public ulong ClientId;
        public Unity.Collections.FixedString32Bytes PlayerName;
        public bool IsReady;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsReady);
        }

        public bool Equals(LobbyPlayerState other)
        {
            return ClientId == other.ClientId && PlayerName == other.PlayerName && IsReady == other.IsReady;
        }
    }

    private NetworkList<LobbyPlayerState> _lobbyPlayers;

    private void Awake()
    {
        _lobbyPlayers = new NetworkList<LobbyPlayerState>();
        // 처음엔 로비 UI 숨김 (접속 후 표시)
        if (_lobbyUIPanel != null) _lobbyUIPanel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (_lobbyUIPanel != null) _lobbyUIPanel.SetActive(true);

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            
            _lobbyPlayers.Add(new LobbyPlayerState { 
                ClientId = NetworkManager.Singleton.LocalClientId, 
                PlayerName = GetMyPlayerName(), 
                IsReady = false 
            });
        }
        else
        {
            SubmitPlayerNameServerRpc(GetMyPlayerName());
        }

        _lobbyPlayers.OnListChanged += HandleLobbyPlayersStateChanged;
        UpdateUI();

        _readyButton.onClick.AddListener(() => ToggleReadyServerRpc());
        _startGameButton.onClick.AddListener(() => StartGame());
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
        _lobbyPlayers.OnListChanged -= HandleLobbyPlayersStateChanged;
        
        if (_lobbyUIPanel != null) _lobbyUIPanel.SetActive(false);
    }

    private string GetMyPlayerName()
    {
        if (SteamManager.Initialized)
            return SteamFriends.GetPersonaName();
        return "Player " + NetworkManager.Singleton.LocalClientId;
    }

    private void HandleClientConnected(ulong clientId)
    {
        _lobbyPlayers.Add(new LobbyPlayerState { ClientId = clientId, PlayerName = "Connecting...", IsReady = false });
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        for (int i = 0; i < _lobbyPlayers.Count; i++)
        {
            if (_lobbyPlayers[i].ClientId == clientId)
            {
                _lobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        for (int i = 0; i < _lobbyPlayers.Count; i++)
        {
            if (_lobbyPlayers[i].ClientId == rpcParams.Receive.SenderClientId)
            {
                var state = _lobbyPlayers[i];
                state.PlayerName = playerName;
                _lobbyPlayers[i] = state;
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        for (int i = 0; i < _lobbyPlayers.Count; i++)
        {
            if (_lobbyPlayers[i].ClientId == rpcParams.Receive.SenderClientId)
            {
                var state = _lobbyPlayers[i];
                state.IsReady = !state.IsReady;
                _lobbyPlayers[i] = state;
                break;
            }
        }
    }

    private void HandleLobbyPlayersStateChanged(NetworkListEvent<LobbyPlayerState> changeEvent)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        bool allReady = true;

        for (int i = 0; i < 4; i++)
        {
            if (i < _lobbyPlayers.Count)
            {
                _playerNameTexts[i].text = _lobbyPlayers[i].PlayerName.ToString();
                _playerReadyTexts[i].text = _lobbyPlayers[i].IsReady ? "Ready" : "Waiting";
                _playerReadyTexts[i].color = _lobbyPlayers[i].IsReady ? Color.green : Color.red;

                if (!_lobbyPlayers[i].IsReady) allReady = false;
            }
            else
            {
                _playerNameTexts[i].text = "Empty Slot";
                _playerReadyTexts[i].text = "";
            }
        }

        _startGameButton.gameObject.SetActive(IsServer);
        _startGameButton.interactable = allReady && _lobbyPlayers.Count > 0;
    }

    private void StartGame()
    {
        if (IsServer)
        {
            // GameScene으로 씬 전환 (NetworkSceneManager 사용)
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
