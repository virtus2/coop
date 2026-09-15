using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameScene에서 플레이 도중 ESC 키 입력을 감지하여 인게임 메뉴(계속하기, 종료하기)를 토글하고,
/// 호스트/클라이언트 연결 종료, 게임 상태 저장, MainScene 복귀를 관장하는 컨트롤러입니다.
/// </summary>
public class InGameMenuController : MonoBehaviour
{
    public static InGameMenuController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Text _statusText;

    private bool _isMenuOpen = false;
    private bool _isExiting = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(false);
        }

        if (_resumeButton != null)
        {
            _resumeButton.onClick.RemoveAllListeners();
            _resumeButton.onClick.AddListener(ResumeGame);
        }

        if (_exitButton != null)
        {
            _exitButton.onClick.RemoveAllListeners();
            _exitButton.onClick.AddListener(OnExitButtonClicked);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        }
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
    }

    private void Update()
    {
        if (_isExiting) return;

        // Unity New Input System을 통한 ESC 키 입력 감지
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (_isMenuOpen)
        {
            ResumeGame();
        }
        else
        {
            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        _isMenuOpen = true;
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(true);
        }

        // 마우스 커서 잠금 해제 및 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 로컬 플레이어 캐릭터의 시점 및 이동 입력 차단
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.SetInputEnabled(false);
        }
    }

    public void ResumeGame()
    {
        _isMenuOpen = false;
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(false);
        }

        // 마우스 커서 다시 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 로컬 플레이어 캐릭터의 시점 및 이동 입력 재개
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.SetInputEnabled(true);
        }
    }

    public void OnExitButtonClicked()
    {
        if (_isExiting) return;
        _isExiting = true;

        if (_resumeButton != null) _resumeButton.interactable = false;
        if (_exitButton != null) _exitButton.interactable = false;

        StartCoroutine(ExitRoutine());
    }

    private IEnumerator ExitRoutine()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        if (_statusText != null)
        {
            _statusText.gameObject.SetActive(true);
            _statusText.text = isHost ? "게임 상태를 저장하고 종료하는 중..." : "연결을 종료하는 중...";
        }

        // 호스트인 경우: 게임 상태를 비동기로 저장 완료 후 셧다운 진행
        if (isHost)
        {
            if (SaveLoadManager.Instance != null)
            {
                Task saveTask = SaveLoadManager.Instance.SaveGameAsync();
                yield return new WaitUntil(() => saveTask.IsCompleted);

                if (saveTask.IsFaulted)
                {
                    Debug.LogError($"[InGameMenuController] 게임 저장 실패: {saveTask.Exception?.Message}");
                }
            }
        }

        // 네트워크 세션 셧다운
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 스팀 로비 정리
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }

        // 커서 원상복구
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // MainScene으로 복귀
        SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
    }

    /// <summary>
    /// 클라이언트 플레이 도중 호스트가 종료되었거나 원격 연결이 해제되었을 때 MainScene으로 자동 복귀
    /// </summary>
    private void HandleClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;

        // 로컬 클라이언트의 연결이 끊긴 경우
        if (clientId == NetworkManager.Singleton.LocalClientId || !NetworkManager.Singleton.IsConnectedClient)
        {
            if (!_isExiting)
            {
                Debug.Log("[InGameMenuController] 호스트와의 연결 종료를 감지했습니다. MainScene으로 복귀합니다.");
                _isExiting = true;

                if (SteamLobbyManager.Instance != null)
                {
                    SteamLobbyManager.Instance.LeaveLobby();
                }

                if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.Shutdown();
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
            }
        }
    }
}
