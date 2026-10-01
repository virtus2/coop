using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using Steamworks;

public class LobbyManager : NetworkBehaviour
{
    [SerializeField] private TMP_Text[] _playerNameTexts;
    [SerializeField] private TMP_Text[] _playerReadyTexts;
    [SerializeField] private Button _readyButton;
    [SerializeField] private Button _inviteButton;
    [SerializeField] private Button _startGameButton;
    [SerializeField] private GameObject _lobbyUIPanel;

    [Header("Lobby ID UI")]
    [SerializeField] private GameObject _lobbyIdContainer;
    [SerializeField] private TMP_Text _lobbyIdText;
    [SerializeField] private Button _showIdButton;
    [SerializeField] private Button _copyIdButton;

    private const string MaskedId = "••••••••••••••••••";
    private bool _isShowingId = false;
    private Coroutine _hideIdCoroutine;
    private bool _isStartingGame = false;

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
        // 로비 씬에서는 커서 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public override void OnNetworkSpawn()
    {
        _isStartingGame = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_lobbyUIPanel != null)
        {
            _lobbyUIPanel.SetActive(true);
        }

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

            _lobbyPlayers.Clear();

            // 현재 연결되어 있는 클라이언트들을 모두 로비 목록에 추가
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                string pName = (client.ClientId == NetworkManager.Singleton.LocalClientId) 
                    ? GetMyPlayerName() 
                    : $"Player {client.ClientId}";
                AddLobbyPlayer(client.ClientId, pName);
            }
        }
        else
        {
            SubmitPlayerNameServerRpc(GetMyPlayerName());
        }

        _lobbyPlayers.OnListChanged += HandleLobbyPlayersStateChanged;
        UpdateUI();

        if (_readyButton != null)
        {
            _readyButton.onClick.RemoveAllListeners();
            _readyButton.onClick.AddListener(() => ToggleReadyServerRpc());
        }

        if (_inviteButton != null)
        {
            _inviteButton.onClick.RemoveAllListeners();
            _inviteButton.onClick.AddListener(OnInviteButtonClicked);
            // 스팀이 초기화된 상태(스팀 빌드 환경 등)에서만 친구 초대 버튼 표시
            _inviteButton.gameObject.SetActive(SteamManager.Initialized);
        }

        if (_startGameButton != null)
        {
            _startGameButton.onClick.RemoveAllListeners();
            _startGameButton.onClick.AddListener(() => StartGame());
        }

        SetupLobbyIdUI();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        if (_lobbyPlayers != null)
        {
            _lobbyPlayers.OnListChanged -= HandleLobbyPlayersStateChanged;
        }

        if (_hideIdCoroutine != null)
        {
            StopCoroutine(_hideIdCoroutine);
            _hideIdCoroutine = null;
        }

        if (_lobbyUIPanel != null)
        {
            _lobbyUIPanel.SetActive(false);
        }

        _isStartingGame = false;
    }

    private string GetMyPlayerName()
    {
        if (SteamManager.Initialized)
        {
            return SteamFriends.GetPersonaName();
        }
        return "Player " + (NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
    }

    private void AddLobbyPlayer(ulong clientId, string playerName)
    {
        for (int i = 0; i < _lobbyPlayers.Count; i++)
        {
            if (_lobbyPlayers[i].ClientId == clientId)
            {
                return;
            }
        }

        _lobbyPlayers.Add(new LobbyPlayerState
        {
            ClientId = clientId,
            PlayerName = playerName,
            IsReady = false
        });
    }

    private void HandleClientConnected(ulong clientId)
    {
        // 로컬 서버 호스트는 OnNetworkSpawn에서 이미 처리됨
        AddLobbyPlayer(clientId, $"Player {clientId}");
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
        ulong senderId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < _lobbyPlayers.Count; i++)
        {
            if (_lobbyPlayers[i].ClientId == senderId)
            {
                var state = _lobbyPlayers[i];
                state.PlayerName = playerName;
                _lobbyPlayers[i] = state;
                return;
            }
        }

        // 목록에 아직 없는 경우 추가
        _lobbyPlayers.Add(new LobbyPlayerState
        {
            ClientId = senderId,
            PlayerName = playerName,
            IsReady = false
        });
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < _lobbyPlayers.Count; i++)
        {
            if (_lobbyPlayers[i].ClientId == senderId)
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
        if (_playerNameTexts == null || _playerReadyTexts == null)
        {
            return;
        }

        bool allReady = true;

        for (int i = 0; i < 4; i++)
        {
            if (i < _playerNameTexts.Length && i < _playerReadyTexts.Length)
            {
                if (i < _lobbyPlayers.Count)
                {
                    _playerNameTexts[i].text = _lobbyPlayers[i].PlayerName.ToString();
                    _playerReadyTexts[i].text = _lobbyPlayers[i].IsReady ? "Ready" : "Waiting";
                    _playerReadyTexts[i].color = _lobbyPlayers[i].IsReady ? Color.green : Color.red;

                    if (!_lobbyPlayers[i].IsReady)
                    {
                        allReady = false;
                    }
                }
                else
                {
                    _playerNameTexts[i].text = "Empty Slot";
                    _playerReadyTexts[i].text = "";
                }
            }
        }

        if (_startGameButton != null)
        {
            _startGameButton.gameObject.SetActive(IsServer);
            _startGameButton.interactable = allReady && _lobbyPlayers.Count > 0;
        }
    }

    private void OnInviteButtonClicked()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.InviteFriends();
            StartCoroutine(ShowCopiedFeedback());
        }
        else
        {
            Debug.LogWarning("[LobbyManager] SteamLobbyManager instance is null.");
        }
    }

    private System.Collections.IEnumerator ShowCopiedFeedback()
    {
        if (_inviteButton != null)
        {
            var btnText = _inviteButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                string original = btnText.text;
                btnText.text = "Copied ID!";
                yield return new WaitForSeconds(2f);
                btnText.text = original;
            }
        }
    }

    private void SetupLobbyIdUI()
    {
        bool hasSteamLobby = SteamManager.Initialized &&
                             SteamLobbyManager.Instance != null &&
                             SteamLobbyManager.Instance.CurrentLobbyID.IsValid();

        if (_lobbyIdContainer != null)
        {
            _lobbyIdContainer.SetActive(hasSteamLobby);
        }

        if (_lobbyIdText != null)
        {
            _lobbyIdText.text = hasSteamLobby ? $"Lobby ID: {MaskedId}" : "";
        }

        if (_showIdButton != null)
        {
            _showIdButton.onClick.RemoveAllListeners();
            _showIdButton.onClick.AddListener(OnShowIdClicked);
            var txt = _showIdButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "Show";
        }

        if (_copyIdButton != null)
        {
            _copyIdButton.onClick.RemoveAllListeners();
            _copyIdButton.onClick.AddListener(OnCopyIdClicked);
            var txt = _copyIdButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "Copy";
        }
    }

    private void OnShowIdClicked()
    {
        if (SteamLobbyManager.Instance == null || !SteamLobbyManager.Instance.CurrentLobbyID.IsValid())
        {
            return;
        }

        if (_isShowingId)
        {
            if (_hideIdCoroutine != null)
            {
                StopCoroutine(_hideIdCoroutine);
                _hideIdCoroutine = null;
            }
            _isShowingId = false;
            if (_lobbyIdText != null)
            {
                _lobbyIdText.text = $"Lobby ID: {MaskedId}";
            }
            if (_showIdButton != null)
            {
                var txt = _showIdButton.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "Show";
            }
        }
        else
        {
            _isShowingId = true;
            if (_lobbyIdText != null)
            {
                _lobbyIdText.text = $"Lobby ID: {SteamLobbyManager.Instance.CurrentLobbyID.m_SteamID}";
            }
            if (_hideIdCoroutine != null)
            {
                StopCoroutine(_hideIdCoroutine);
            }
            _hideIdCoroutine = StartCoroutine(HideIdAfterSeconds(3f));
        }
    }

    private System.Collections.IEnumerator HideIdAfterSeconds(float seconds)
    {
        if (_showIdButton != null)
        {
            var txt = _showIdButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "Hide";
        }

        yield return new WaitForSeconds(seconds);

        _isShowingId = false;
        if (_lobbyIdText != null)
        {
            _lobbyIdText.text = $"Lobby ID: {MaskedId}";
        }
        if (_showIdButton != null)
        {
            var txt = _showIdButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "Show";
        }
        _hideIdCoroutine = null;
    }

    private void OnCopyIdClicked()
    {
        if (SteamLobbyManager.Instance == null || !SteamLobbyManager.Instance.CurrentLobbyID.IsValid())
        {
            Debug.LogWarning("[LobbyManager] Cannot copy: Steam Lobby ID is not valid.");
            return;
        }

        string idStr = SteamLobbyManager.Instance.CurrentLobbyID.m_SteamID.ToString();
        GUIUtility.systemCopyBuffer = idStr;
        Debug.Log($"[LobbyManager] Copied Lobby ID to clipboard: {idStr}");
        StartCoroutine(ShowCopyButtonFeedback());
    }

    private System.Collections.IEnumerator ShowCopyButtonFeedback()
    {
        if (_copyIdButton != null)
        {
            var txt = _copyIdButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                string orig = txt.text;
                txt.text = "Copied!";
                yield return new WaitForSeconds(2f);
                txt.text = orig;
            }
        }
    }

    private void StartGame()
    {
        if (!IsServer || _isStartingGame)
        {
            return;
        }

        _isStartingGame = true;

        if (_startGameButton != null)
        {
            _startGameButton.interactable = false;
        }

        // 모든 클라이언트에 1.5초 페이드아웃 및 GameScene 진입 시 페이드인 지시
        StartGameTransitionClientRpc();

        // 1.5초 페이드아웃 완료 후 씬 전환
        StartCoroutine(StartGameWithFadeRoutine());
    }

    [ClientRpc]
    private void StartGameTransitionClientRpc()
    {
        // 로비 버튼 상호작용 비활성화
        if (_startGameButton != null) _startGameButton.interactable = false;
        if (_readyButton != null) _readyButton.interactable = false;
        if (_inviteButton != null) _inviteButton.interactable = false;

        // 1.5초 페이드아웃 시작 및 GameScene 로드 시 1.5초 페이드인 예약
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.StartGameTransition(1.5f, 1.5f);
        }
    }

    private System.Collections.IEnumerator StartGameWithFadeRoutine()
    {
        // 1.5초 페이드아웃 대기
        yield return new WaitForSecondsRealtime(1.5f);

        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            // GameScene으로 씬 전환 (NetworkSceneManager 사용)
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}

