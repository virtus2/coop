using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using Steamworks;

public class LobbyManager : NetworkBehaviour
{
    public Text[] PlayerNameTexts;
    public Text[] PlayerReadyTexts;
    public Button ReadyButton;
    public Button StartGameButton;
    public GameObject LobbyUIPanel;

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

    private NetworkList<LobbyPlayerState> m_LobbyPlayers;

    private void Awake()
    {
        m_LobbyPlayers = new NetworkList<LobbyPlayerState>();
        // 처음엔 로비 UI 숨김 (접속 후 표시)
        if (LobbyUIPanel != null) LobbyUIPanel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (LobbyUIPanel != null) LobbyUIPanel.SetActive(true);

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            
            m_LobbyPlayers.Add(new LobbyPlayerState { 
                ClientId = NetworkManager.Singleton.LocalClientId, 
                PlayerName = GetMyPlayerName(), 
                IsReady = false 
            });
        }
        else
        {
            SubmitPlayerNameServerRpc(GetMyPlayerName());
        }

        m_LobbyPlayers.OnListChanged += HandleLobbyPlayersStateChanged;
        UpdateUI();

        ReadyButton.onClick.AddListener(() => ToggleReadyServerRpc());
        StartGameButton.onClick.AddListener(() => StartGame());
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
        m_LobbyPlayers.OnListChanged -= HandleLobbyPlayersStateChanged;
        
        if (LobbyUIPanel != null) LobbyUIPanel.SetActive(false);
    }

    private string GetMyPlayerName()
    {
        if (SteamManager.Initialized)
            return SteamFriends.GetPersonaName();
        return "Player " + NetworkManager.Singleton.LocalClientId;
    }

    private void HandleClientConnected(ulong clientId)
    {
        m_LobbyPlayers.Add(new LobbyPlayerState { ClientId = clientId, PlayerName = "Connecting...", IsReady = false });
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        for (int i = 0; i < m_LobbyPlayers.Count; i++)
        {
            if (m_LobbyPlayers[i].ClientId == clientId)
            {
                m_LobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        for (int i = 0; i < m_LobbyPlayers.Count; i++)
        {
            if (m_LobbyPlayers[i].ClientId == rpcParams.Receive.SenderClientId)
            {
                var state = m_LobbyPlayers[i];
                state.PlayerName = playerName;
                m_LobbyPlayers[i] = state;
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        for (int i = 0; i < m_LobbyPlayers.Count; i++)
        {
            if (m_LobbyPlayers[i].ClientId == rpcParams.Receive.SenderClientId)
            {
                var state = m_LobbyPlayers[i];
                state.IsReady = !state.IsReady;
                m_LobbyPlayers[i] = state;
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
            if (i < m_LobbyPlayers.Count)
            {
                PlayerNameTexts[i].text = m_LobbyPlayers[i].PlayerName.ToString();
                PlayerReadyTexts[i].text = m_LobbyPlayers[i].IsReady ? "Ready" : "Waiting";
                PlayerReadyTexts[i].color = m_LobbyPlayers[i].IsReady ? Color.green : Color.red;

                if (!m_LobbyPlayers[i].IsReady) allReady = false;
            }
            else
            {
                PlayerNameTexts[i].text = "Empty Slot";
                PlayerReadyTexts[i].text = "";
            }
        }

        StartGameButton.gameObject.SetActive(IsServer);
        StartGameButton.interactable = allReady && m_LobbyPlayers.Count > 0;
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
