using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Unity.Netcode.Transports.UTP;
using RaybelCreation.Netcode.Transports.Steam;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
public class NetworkBootstrap : MonoBehaviour
{
    private NetworkManager _networkManager;
    private string _lobbyIdInput = "";

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
    }

    private void Start()
    {
        if (SteamManager.Initialized)
        {
            Debug.Log($"Steamworks Initialized: {SteamFriends.GetPersonaName()}");
        }
        else
        {
            Debug.LogWarning("Steamworks is not initialized. Make sure steam_appid.txt is present and Steam is running.");
        }
    }

    // 통신 방식(Transport)을 동적으로 변경하는 함수
    private void SetupTransport()
    {
#if UNITY_EDITOR
        // 유니티 에디터에서는 로컬 테스트를 위해 UnityTransport (127.0.0.1)를 강제로 사용
        var utp = GetComponent<UnityTransport>();
        if (utp == null) utp = gameObject.AddComponent<UnityTransport>();
        
        utp.ConnectionData.Address = "127.0.0.1";
        utp.ConnectionData.Port = 7777;
        utp.ConnectionData.ServerListenAddress = "0.0.0.0";
        
        _networkManager.NetworkConfig.NetworkTransport = utp;
        Debug.Log("Editor Mode: Switched to UnityTransport (Localhost) for local testing.");
#else
        // 빌드된 게임(실제 배포판)에서는 Steam Transport를 사용
        var steamTransport = GetComponent<SteamNetworkTransport>();
        if (steamTransport == null) steamTransport = gameObject.AddComponent<SteamNetworkTransport>();
        
        _networkManager.NetworkConfig.NetworkTransport = steamTransport;
        Debug.Log("Build Mode: Switched to SteamNetworkTransport.");
#endif
    }

    private void OnGUI()
    {
        // LobbyScene이나 GameScene에서는 호스트/클라이언트 연결 버튼 숨김
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (activeSceneName == "LobbyScene" || activeSceneName == "GameScene")
        {
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 320, 350));
        
        if (_networkManager == null)
        {
            GUILayout.Label("NetworkManager is not ready or missing.");
            GUILayout.EndArea();
            return;
        }

        if (!_networkManager.IsClient && !_networkManager.IsServer)
        {
            if (GUILayout.Button("Start Host", GUILayout.Height(40)))
            {
                SetupTransport();
#if UNITY_EDITOR
                if (_networkManager.StartHost())
                {
                    _networkManager.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
                }
#else
                if (SteamLobbyManager.Instance != null)
                {
                    SteamLobbyManager.Instance.HostLobby();
                }
                else
                {
                    var lobbyManager = GetComponent<SteamLobbyManager>();
                    if (lobbyManager != null) 
                    {
                        lobbyManager.HostLobby();
                    } 
                    else if (_networkManager.StartHost())
                    {
                        _networkManager.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
                    }
                }
#endif
            }

            GUILayout.Space(15);

#if UNITY_EDITOR
            if (GUILayout.Button("Start Client (Local Only)", GUILayout.Height(40)))
            {
                SetupTransport();
                _networkManager.StartClient();
            }

            GUILayout.Space(10);
            GUILayout.Label("Join by Steam Lobby ID (Editor):");
            _lobbyIdInput = GUILayout.TextField(_lobbyIdInput, GUILayout.Height(25));
            if (GUILayout.Button("Join Lobby", GUILayout.Height(30)))
            {
                if (SteamLobbyManager.Instance != null)
                {
                    SteamLobbyManager.Instance.JoinLobby(_lobbyIdInput);
                }
            }
#else
            GUILayout.Label("Steam Lobby ID:");
            _lobbyIdInput = GUILayout.TextField(_lobbyIdInput, GUILayout.Height(30));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Paste", GUILayout.Height(35), GUILayout.Width(70)))
            {
                _lobbyIdInput = GUIUtility.systemCopyBuffer;
            }
            if (GUILayout.Button("Join Lobby", GUILayout.Height(35)))
            {
                if (SteamLobbyManager.Instance != null)
                {
                    SteamLobbyManager.Instance.JoinLobby(_lobbyIdInput);
                }
                else
                {
                    Debug.LogWarning("[NetworkBootstrap] SteamLobbyManager.Instance is null.");
                }
            }
            GUILayout.EndHorizontal();
#endif
        }
        else
        {
            GUILayout.Label($"Mode: {(_networkManager.IsHost ? "Host" : "Client")}");
            if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
            {
                _networkManager.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}
