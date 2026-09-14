using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LobbyCanvas에 붙는 순수 UI 컨트롤러 (NetworkBehaviour 아님)
/// LobbyNetworkSync 이벤트를 구독하여 UI를 갱신한다.
/// </summary>
public class LobbyUIController : MonoBehaviour
{
    [SerializeField] private GameObject m_LobbyPanel;
    [SerializeField] private Text[] m_PlayerNameTexts;
    [SerializeField] private Text[] m_PlayerReadyTexts;
    [SerializeField] private Button m_ReadyButton;
    [SerializeField] private Button m_StartButton;

    private LobbyNetworkSync m_Sync;

    private void Start()
    {
        // 시작하면 패널 숨김 — 접속 후 OnConnected에서 표시
        m_LobbyPanel.SetActive(false);

        m_ReadyButton.onClick.AddListener(OnReadyClicked);
        m_StartButton.onClick.AddListener(OnStartClicked);

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
        if (m_Sync != null)
            m_Sync.OnLobbyStateChanged -= RefreshUI;
    }

    // 로컬 클라이언트가 연결되었을 때 (Host 포함)
    private void OnConnected(ulong clientId)
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        // 내 클라이언트 ID일 때만 패널 표시
        if (clientId != nm.LocalClientId && !nm.IsHost) return;

        m_LobbyPanel.SetActive(true);

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

        m_Sync = LobbyNetworkSync.Instance;
        if (m_Sync != null)
        {
            m_Sync.OnLobbyStateChanged += RefreshUI;
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
            m_LobbyPanel.SetActive(false);
            if (m_Sync != null) m_Sync.OnLobbyStateChanged -= RefreshUI;
            m_Sync = null;
        }
    }

    private void RefreshUI()
    {
        if (m_Sync == null) return;

        bool isServer = Unity.Netcode.NetworkManager.Singleton.IsServer;

        for (int i = 0; i < 4; i++)
        {
            if (i < m_Sync.PlayerCount)
            {
                var p = m_Sync.GetPlayer(i);
                m_PlayerNameTexts[i].text = p.PlayerName.ToString();
                m_PlayerReadyTexts[i].text = p.IsReady ? "Ready ✓" : "Waiting...";
                m_PlayerReadyTexts[i].color = p.IsReady ? Color.green : Color.red;
            }
            else
            {
                m_PlayerNameTexts[i].text = "Empty Slot";
                m_PlayerReadyTexts[i].text = "";
            }
        }

        m_StartButton.gameObject.SetActive(isServer);
        m_StartButton.interactable = m_Sync.AllReady && m_Sync.PlayerCount > 0;
    }

    private void OnReadyClicked()
    {
        if (m_Sync != null) m_Sync.ToggleReadyServerRpc();
    }

    private void OnStartClicked()
    {
        if (m_Sync != null) m_Sync.StartGame();
    }
}
