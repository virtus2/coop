using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LobbyCanvas에 붙는 순수 UI 컨트롤러 (NetworkBehaviour 아님)
/// LobbyNetworkSync 이벤트를 구독하여 UI를 갱신한다.
/// </summary>
public class LobbyUIController : MonoBehaviour
{
    [SerializeField] private GameObject _lobbyPanel;
    [SerializeField] private Text[] _playerNameTexts;
    [SerializeField] private Text[] _playerReadyTexts;
    [SerializeField] private Button _readyButton;
    [SerializeField] private Button _startButton;

    private LobbyNetworkSync _sync;

    private void Start()
    {
        // 시작하면 패널 숨김 — 접속 후 OnConnected에서 표시
        _lobbyPanel.SetActive(false);

        _readyButton.onClick.AddListener(OnReadyClicked);
        _startButton.onClick.AddListener(OnStartClicked);

        // NetworkManager 이벤트 구독
        Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback += OnConnected;
        Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    private void OnDestroy()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
        }
        if (_sync != null)
            _sync.OnLobbyStateChanged -= RefreshUI;
    }

    // 로컬 클라이언트가 연결되었을 때 (Host 포함)
    private void OnConnected(ulong clientId)
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        // 내 클라이언트 ID일 때만 패널 표시
        if (clientId != nm.LocalClientId && !nm.IsHost) return;

        _lobbyPanel.SetActive(true);

        // LobbyNetworkSync가 Spawn될 때까지 잠깐 기다려야 할 수 있으므로 Coroutine으로 구독
        StartCoroutine(WaitAndSubscribe());
    }

    private System.Collections.IEnumerator WaitAndSubscribe()
    {
        // LobbyNetworkSync가 spawn될 때까지 최대 3초 대기
        float elapsed = 0f;
        while (LobbyNetworkSync.Instance == null && elapsed < 3f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _sync = LobbyNetworkSync.Instance;
        if (_sync != null)
        {
            _sync.OnLobbyStateChanged += RefreshUI;
            RefreshUI();
        }
        else
        {
            Debug.LogWarning("[LobbyUIController] LobbyNetworkSync not found after waiting.");
        }
    }

    private void OnDisconnected(ulong clientId)
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (clientId == nm.LocalClientId)
        {
            _lobbyPanel.SetActive(false);
            if (_sync != null) _sync.OnLobbyStateChanged -= RefreshUI;
            _sync = null;
        }
    }

    private void RefreshUI()
    {
        if (_sync == null) return;

        bool isServer = Unity.Netcode.NetworkManager.Singleton.IsServer;

        for (int i = 0; i < 4; i++)
        {
            if (i < _sync.PlayerCount)
            {
                var p = _sync.GetPlayer(i);
                _playerNameTexts[i].text = p.PlayerName.ToString();
                _playerReadyTexts[i].text = p.IsReady ? "Ready ✓" : "Waiting...";
                _playerReadyTexts[i].color = p.IsReady ? Color.green : Color.red;
            }
            else
            {
                _playerNameTexts[i].text = "Empty Slot";
                _playerReadyTexts[i].text = "";
            }
        }

        _startButton.gameObject.SetActive(isServer);
        _startButton.interactable = _sync.AllReady && _sync.PlayerCount > 0;
    }

    private void OnReadyClicked()
    {
        if (_sync != null) _sync.ToggleReadyServerRpc();
    }

    private void OnStartClicked()
    {
        if (_sync != null) _sync.StartGame();
    }
}
