using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MainScene에서 UGUI 기반 메인 메뉴(로비 생성, 로비 참가, 옵션, 게임 종료)를 제어하는 컨트롤러입니다.
/// </summary>
public class MainMenuUIController : MonoBehaviour
{
    [Header("Network Reference")]
    [SerializeField] private NetworkBootstrap _networkBootstrap;

    [Header("Main Menu UI")]
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private Button _hostLobbyButton;
    [SerializeField] private InputField _lobbyIdInputField;
    [SerializeField] private Button _joinLobbyButton;
    [SerializeField] private Button _optionButton;
    [SerializeField] private Button _quitButton;

    [Header("Option Window UI")]
    [SerializeField] private OptionWindowUI _optionWindow;
    [SerializeField] private SaveSelectionUIController _saveSelectionUI;

    private void Awake()
    {
        if (_networkBootstrap == null)
        {
            _networkBootstrap = FindFirstObjectByType<NetworkBootstrap>();
        }

        if (_lobbyIdInputField != null)
        {
            _lobbyIdInputField.contentType = InputField.ContentType.IntegerNumber;
#if UNITY_EDITOR
            if (NetworkBootstrap.IsParrelSyncClone())
            {
                if (_lobbyIdInputField.placeholder is Text ph)
                {
                    ph.text = "클론: 비워두고 참가 시 로컬 접속...";
                }
            }
#endif
        }
    }

    private void Start()
    {
        RegisterListeners();
    }

    private void OnDestroy()
    {
        UnregisterListeners();
    }

    private void RegisterListeners()
    {
        if (_hostLobbyButton != null)
        {
            _hostLobbyButton.onClick.AddListener(OnHostLobbyClicked);
        }

        if (_joinLobbyButton != null)
        {
            _joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        }

        if (_optionButton != null)
        {
            _optionButton.onClick.AddListener(OnOptionClicked);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(OnQuitClicked);
        }

        if (_optionWindow != null)
        {
            _optionWindow.OnClosed += HandleOptionClosed;
        }
    }

    private void UnregisterListeners()
    {
        if (_hostLobbyButton != null)
        {
            _hostLobbyButton.onClick.RemoveListener(OnHostLobbyClicked);
        }

        if (_joinLobbyButton != null)
        {
            _joinLobbyButton.onClick.RemoveListener(OnJoinLobbyClicked);
        }

        if (_optionButton != null)
        {
            _optionButton.onClick.RemoveListener(OnOptionClicked);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        if (_optionWindow != null)
        {
            _optionWindow.OnClosed -= HandleOptionClosed;
        }
    }

    private NetworkBootstrap GetNetworkBootstrap()
    {
        if (_networkBootstrap == null)
        {
            _networkBootstrap = FindFirstObjectByType<NetworkBootstrap>();
        }
        return _networkBootstrap;
    }

    private void OnHostLobbyClicked()
    {
        var bootstrap = GetNetworkBootstrap();
        if (bootstrap != null)
        {
            if (_saveSelectionUI != null)
            {
                _saveSelectionUI.Open(() => bootstrap.HostLobby());
            }
            else
            {
                bootstrap.HostLobby();
            }
        }
        else
        {
            Debug.LogError("[MainMenuUIController] NetworkBootstrap is not referenced or could not be found.");
        }
    }

    private void OnJoinLobbyClicked()
    {
        var bootstrap = GetNetworkBootstrap();
        if (bootstrap == null)
        {
            Debug.LogError("[MainMenuUIController] NetworkBootstrap is not referenced or could not be found.");
            return;
        }

        string lobbyId = _lobbyIdInputField != null ? _lobbyIdInputField.text : string.Empty;
        bootstrap.JoinLobby(lobbyId);
    }

    private void OnOptionClicked()
    {
        if (_optionWindow != null)
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
            }
            _optionWindow.Open();
        }
        else
        {
            Debug.LogWarning("[MainMenuUIController] OptionWindow is not referenced.");
        }
    }

    private void HandleOptionClosed()
    {
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(true);
        }
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
