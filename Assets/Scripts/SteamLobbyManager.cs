using UnityEngine;
using Steamworks;
using Unity.Netcode;
using RaybelCreation.Netcode.Transports.Steam;

public class SteamLobbyManager : MonoBehaviour
{
    protected Callback<LobbyCreated_t> _lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> _joinRequest;
    protected Callback<LobbyEnter_t> _lobbyEntered;

    private const string HostAddressKey = "HostAddress";

    private void Start()
    {
        if (!SteamManager.Initialized) { return; }

        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _joinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public void HostLobby()
    {
        // 4인용 친구 전용 로비 생성
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Failed to create Steam Lobby");
            return;
        }

        Debug.Log("Steam Lobby created successfully!");
        
        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        NetworkManager.Singleton.StartHost();
        
        // 현재 호스트의 SteamID를 로비 데이터에 기록 (선택 사항)
        SteamMatchmaking.SetLobbyData(lobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, "name", SteamFriends.GetPersonaName() + "'s Lobby");
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Request to join Lobby via Steam Overlay.");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        // 자신이 호스트라면 무시 (이미 StartHost 실행됨)
        if (NetworkManager.Singleton.IsHost) return;

        Debug.Log("Entered Steam Lobby!");

        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);

        var transport = NetworkManager.Singleton.GetComponent<SteamNetworkTransport>();
        
        // 방장의 SteamID를 타겟으로 지정
        transport.SetRemoteSteamId(hostId.m_SteamID);
        
        NetworkManager.Singleton.StartClient();
    }
}
