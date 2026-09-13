using UnityEngine;
using Unity.Netcode;
using Steamworks;

public class NetworkBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (SteamManager.Initialized)
        {
            Debug.Log($"Steamworks Initialized: {SteamFriends.GetPersonaName()}");
        }
        else
        {
            Debug.LogError("Steamworks is not initialized. Make sure steam_appid.txt is present and Steam is running.");
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Start Host"))
            {
                NetworkManager.Singleton.StartHost();
            }

            if (GUILayout.Button("Start Client"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            GUILayout.Label($"Mode: {(NetworkManager.Singleton.IsHost ? "Host" : "Client")}");
            if (GUILayout.Button("Disconnect"))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}
