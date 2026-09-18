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

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
#if UNITY_EDITOR
        SetupTransport();
#endif
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
        if (utp == null)
        {
            utp = gameObject.AddComponent<UnityTransport>();
        }
        
        utp.ConnectionData.Address = "127.0.0.1";
        utp.ConnectionData.Port = 7777;
        utp.ConnectionData.ServerListenAddress = "0.0.0.0";
        
        _networkManager.NetworkConfig.NetworkTransport = utp;

        // 에디터에서 SteamNetworkTransport 비활성화
        var steamTransport = GetComponent<SteamNetworkTransport>();
        if (steamTransport != null)
        {
            steamTransport.enabled = false;
        }

        Debug.Log("Editor Mode: Switched to UnityTransport (Localhost) for local testing.");
#else
        // 빌드된 게임(실제 배포판)에서는 Steam Transport를 사용
        var steamTransport = GetComponent<SteamNetworkTransport>();
        if (steamTransport == null)
        {
            steamTransport = gameObject.AddComponent<SteamNetworkTransport>();
        }
        steamTransport.enabled = true;
        
        _networkManager.NetworkConfig.NetworkTransport = steamTransport;
        Debug.Log("Build Mode: Switched to SteamNetworkTransport.");
#endif
    }

    /// <summary>
    /// 로비 만들기 (Host 시작)
    /// </summary>
    public void HostLobby()
    {
        if (_networkManager == null)
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        if (_networkManager == null)
        {
            Debug.LogError("[NetworkBootstrap] NetworkManager is missing.");
            return;
        }

        SetupTransport();

#if UNITY_EDITOR
        if (_networkManager.StartHost())
        {
            _networkManager.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[NetworkBootstrap] Failed to start host in Editor.");
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
            else
            {
                Debug.LogError("[NetworkBootstrap] Failed to start host in Build mode.");
            }
        }
#endif
    }

    /// <summary>
    /// 로비 번호(Lobby ID)를 통해 로비 참가
    /// </summary>
    public void JoinLobby(string lobbyId)
    {
        if (_networkManager == null)
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        if (_networkManager == null)
        {
            Debug.LogError("[NetworkBootstrap] NetworkManager is missing.");
            return;
        }

        SetupTransport();

#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            // 에디터에서 번호가 비어있으면 로컬 클라이언트로 바로 접속
            Debug.Log("[NetworkBootstrap] Editor mode with empty lobby ID: starting local client.");
            _networkManager.StartClient();
        }
        else
        {
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.JoinLobby(lobbyId.Trim());
            }
            else
            {
                Debug.LogWarning("[NetworkBootstrap] SteamLobbyManager is null. Starting local client fallback.");
                _networkManager.StartClient();
            }
        }
#else
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            Debug.LogWarning("[NetworkBootstrap] Cannot join lobby: Lobby ID is empty.");
            return;
        }

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.JoinLobby(lobbyId.Trim());
        }
        else
        {
            var lobbyManager = GetComponent<SteamLobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.JoinLobby(lobbyId.Trim());
            }
            else
            {
                Debug.LogError("[NetworkBootstrap] SteamLobbyManager is missing in Build mode.");
            }
        }
#endif
    }

    /// <summary>
    /// 네트워크 세션 연결 해제
    /// </summary>
    public void Disconnect()
    {
        if (_networkManager != null && (_networkManager.IsClient || _networkManager.IsServer))
        {
            _networkManager.Shutdown();
        }
    }
}
