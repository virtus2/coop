using System;
using UnityEngine;
using Steamworks;
using Unity.Netcode;
using RaybelCreation.Netcode.Transports.Steam;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    public CSteamID CurrentLobbyID { get; private set; } = CSteamID.Nil;

    protected Callback<LobbyCreated_t> _lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> _joinRequest;
    protected Callback<LobbyEnter_t> _lobbyEntered;

    private const string HostAddressKey = "HostAddress";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("[SteamLobbyManager] SteamManager is not initialized. Steam matchmaking callbacks skipped.");
            return;
        }

        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _joinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);

        // NetworkManager 연결 해제 시 로비 정리
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        }

        // 게임 미실행 상태에서 초대 수락으로 실행된 경우 (+connect_lobby) 확인
        CheckCommandLineArgs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        LeaveLobby();
    }

    /// <summary>
    /// 친구 전용 스팀 로비 생성 및 호스트 시작
    /// </summary>
    public void HostLobby(int maxMembers = 4)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("[SteamLobbyManager] Cannot host lobby: Steam is not initialized.");
            return;
        }

        Debug.Log("[SteamLobbyManager] Creating Steam FriendsOnly Lobby...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
    }

    /// <summary>
    /// 현재 로비로 스팀 친구를 초대하는 오버레이 다이얼로그 오픈 (클립보드에 로비 ID 복사 포함)
    /// </summary>
    public void InviteFriends()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("[SteamLobbyManager] Cannot invite friends: Steam is not initialized.");
            return;
        }

        if (CurrentLobbyID == CSteamID.Nil || !CurrentLobbyID.IsValid())
        {
            Debug.LogWarning("[SteamLobbyManager] Cannot invite friends: Current Lobby ID is invalid.");
            return;
        }

        // 스팀 오버레이가 비활성화되어 있는 환경에서도 친구에게 공유할 수 있도록 클립보드에 로비 ID 복사
        GUIUtility.systemCopyBuffer = CurrentLobbyID.m_SteamID.ToString();
        Debug.Log($"[SteamLobbyManager] Lobby ID ({CurrentLobbyID.m_SteamID}) copied to clipboard.");

        Debug.Log($"[SteamLobbyManager] Opening Steam Invite Dialog for Lobby: {CurrentLobbyID}");
        SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyID);
        SteamFriends.ActivateGameOverlay("LobbyInvite");
    }

    /// <summary>
    /// Lobby ID 문자열을 파싱하여 로비에 접속
    /// </summary>
    public void JoinLobby(string lobbyIdString)
    {
        if (string.IsNullOrWhiteSpace(lobbyIdString))
        {
            Debug.LogWarning("[SteamLobbyManager] Lobby ID string is empty.");
            return;
        }

        if (ulong.TryParse(lobbyIdString.Trim(), out ulong lobbyId))
        {
            JoinLobby(lobbyId);
        }
        else
        {
            Debug.LogWarning($"[SteamLobbyManager] Failed to parse Lobby ID: {lobbyIdString}");
        }
    }

    /// <summary>
    /// 64비트 Lobby ID로 스팀 로비에 접속
    /// </summary>
    public void JoinLobby(ulong lobbyId)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("[SteamLobbyManager] Cannot join lobby: Steam is not initialized.");
            return;
        }

        CSteamID targetLobby = new CSteamID(lobbyId);
        if (!targetLobby.IsValid())
        {
            Debug.LogWarning($"[SteamLobbyManager] Invalid target Lobby ID: {lobbyId}");
            return;
        }

        Debug.Log($"[SteamLobbyManager] Joining Steam Lobby: {lobbyId}");
        SteamMatchmaking.JoinLobby(targetLobby);
    }

    /// <summary>
    /// 로비 나가기
    /// </summary>
    public void LeaveLobby()
    {
        if (SteamManager.Initialized && CurrentLobbyID != CSteamID.Nil && CurrentLobbyID.IsValid())
        {
            Debug.Log($"[SteamLobbyManager] Leaving Lobby: {CurrentLobbyID}");
            SteamMatchmaking.LeaveLobby(CurrentLobbyID);
        }
        CurrentLobbyID = CSteamID.Nil;
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamLobbyManager] Failed to create Steam Lobby. Result: {callback.m_eResult}");
            return;
        }

        CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log($"[SteamLobbyManager] Steam Lobby created successfully! LobbyID: {CurrentLobbyID}");

        // 로비 메타데이터 설정
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "name", SteamFriends.GetPersonaName() + "'s Lobby");

        // 세이브 데이터 정보 추가
        if (SaveLoadManager.Instance != null)
        {
            var saveData = SaveLoadManager.Instance.GetCurrentSaveData();
            if (saveData != null)
            {
                SteamMatchmaking.SetLobbyData(CurrentLobbyID, "SaveDay", saveData.inGameDays.ToString());
            }
        }

        // Transport 세팅 확인 및 호스트 시작
        SetupSteamTransportForHost();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.StartHost())
        {
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[SteamLobbyManager] NetworkManager failed to StartHost.");
        }
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log($"[SteamLobbyManager] Received join request for lobby: {callback.m_steamIDLobby}");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"[SteamLobbyManager] Failed to enter lobby. Error response: {callback.m_EChatRoomEnterResponse}");
            return;
        }

        CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log($"[SteamLobbyManager] Successfully entered Steam Lobby: {CurrentLobbyID}");

        // 자신이 방장인 경우 StartHost는 이미 실행되었으므로 클라이언트 연결 작업 불필요
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            return;
        }

        // 이미 기존 세션에 연결되어 있는 경우 먼저 연결 종료
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            Debug.Log("[SteamLobbyManager] Shutting down active network session before joining new lobby.");
            NetworkManager.Singleton.Shutdown();
        }

        CSteamID hostId = SteamMatchmaking.GetLobbyOwner(CurrentLobbyID);
        Debug.Log($"[SteamLobbyManager] Lobby Owner SteamID: {hostId}");

        if (!hostId.IsValid())
        {
            Debug.LogError("[SteamLobbyManager] Invalid lobby host SteamID.");
            return;
        }

        SetupSteamTransportForClient(hostId.m_SteamID);

        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[SteamLobbyManager] Starting NGO Client to connect to host...");
            NetworkManager.Singleton.StartClient();
        }
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[SteamLobbyManager] Local client disconnected from server.");
            LeaveLobby();
        }
    }

    private void SetupSteamTransportForHost()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

#if !UNITY_EDITOR
        var steamTransport = NetworkManager.Singleton.GetComponent<SteamNetworkTransport>();
        if (steamTransport == null)
        {
            steamTransport = NetworkManager.Singleton.gameObject.AddComponent<SteamNetworkTransport>();
        }
        NetworkManager.Singleton.NetworkConfig.NetworkTransport = steamTransport;
#endif
    }

    private void SetupSteamTransportForClient(ulong hostSteamId)
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

#if !UNITY_EDITOR
        var steamTransport = NetworkManager.Singleton.GetComponent<SteamNetworkTransport>();
        if (steamTransport == null)
        {
            steamTransport = NetworkManager.Singleton.gameObject.AddComponent<SteamNetworkTransport>();
        }
        NetworkManager.Singleton.NetworkConfig.NetworkTransport = steamTransport;
        steamTransport.SetRemoteSteamId(hostSteamId);
#endif
    }

    private void CheckCommandLineArgs()
    {
        // 1. System.Environment 커맨드 라인 인자 확인 (+connect_lobby <lobby_id>)
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "+connect_lobby" && i + 1 < args.Length)
            {
                if (ulong.TryParse(args[i + 1], out ulong lobbyId))
                {
                    Debug.Log($"[SteamLobbyManager] Found +connect_lobby in cmd line: {lobbyId}");
                    SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
                    return;
                }
            }
        }

        // 2. SteamApps.GetLaunchCommandLine 확인
        if (SteamApps.GetLaunchCommandLine(out string steamCmdLine, 1024) > 0 && !string.IsNullOrEmpty(steamCmdLine))
        {
            Debug.Log($"[SteamLobbyManager] Steam LaunchCommandLine: {steamCmdLine}");
            string[] tokens = steamCmdLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "+connect_lobby" && i + 1 < tokens.Length)
                {
                    if (ulong.TryParse(tokens[i + 1], out ulong lobbyId))
                    {
                        Debug.Log($"[SteamLobbyManager] Found +connect_lobby in SteamLaunchCommandLine: {lobbyId}");
                        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
                        return;
                    }
                }
            }
        }
    }
}
