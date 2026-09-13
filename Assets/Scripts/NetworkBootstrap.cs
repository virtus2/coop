using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Unity.Netcode.Transports.UTP;
using RaybelCreation.Netcode.Transports.Steam;

[RequireComponent(typeof(NetworkManager))]
public class NetworkBootstrap : MonoBehaviour
{
    private NetworkManager m_NetworkManager;

    private void Awake()
    {
        m_NetworkManager = GetComponent<NetworkManager>();
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
        
        m_NetworkManager.NetworkConfig.NetworkTransport = utp;
        Debug.Log("Editor Mode: Switched to UnityTransport (Localhost) for local testing.");
#else
        // 빌드된 게임(실제 배포판)에서는 Steam Transport를 사용
        var steamTransport = GetComponent<SteamNetworkTransport>();
        if (steamTransport == null) steamTransport = gameObject.AddComponent<SteamNetworkTransport>();
        
        m_NetworkManager.NetworkConfig.NetworkTransport = steamTransport;
        Debug.Log("Build Mode: Switched to SteamNetworkTransport.");
#endif
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        
        if (m_NetworkManager == null)
        {
            GUILayout.Label("NetworkManager is not ready or missing.");
            GUILayout.EndArea();
            return;
        }

        if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
        {
            if (GUILayout.Button("Start Host"))
            {
                SetupTransport();
#if UNITY_EDITOR
                m_NetworkManager.StartHost();
#else
                var lobbyManager = GetComponent<SteamLobbyManager>();
                if (lobbyManager != null) {
                    lobbyManager.HostLobby();
                } else {
                    m_NetworkManager.StartHost();
                }
#endif
            }

            if (GUILayout.Button("Start Client (Local Only)"))
            {
                SetupTransport();
                m_NetworkManager.StartClient();
            }
        }
        else
        {
            GUILayout.Label($"Mode: {(m_NetworkManager.IsHost ? "Host" : "Client")}");
            if (GUILayout.Button("Disconnect"))
            {
                m_NetworkManager.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}
