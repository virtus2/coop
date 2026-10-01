using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 플레이어 캐릭터 머리 위에 uGUI World Space Canvas로 닉네임을 표시하고,
/// 실시간으로 카메라를 향해 회전하는 빌보드(Billboard) 컴포넌트입니다.
/// </summary>
public class PlayerNamePlate : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TMP_Text _nameText;

    [Header("Display Settings")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 2.2f, 0f);
    [Tooltip("로컬 플레이어 본인의 머리 위에도 닉네임을 표시할지 여부 (기본: false)")]
    [SerializeField] private bool _showLocalPlayerName = false;

    private readonly NetworkVariable<FixedString32Bytes> _playerName = new NetworkVariable<FixedString32Bytes>(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Camera _cachedCamera;
    private Transform _canvasTransform;

    private void Awake()
    {
        if (_canvas == null)
        {
            _canvas = GetComponentInChildren<Canvas>(true);
        }

        if (_canvas != null)
        {
            _canvasTransform = _canvas.transform;
        }

        if (_nameText == null && _canvas != null)
        {
            _nameText = _canvas.GetComponentInChildren<TMP_Text>(true);
        }

        // 프리팹이나 씬에 Canvas/Text가 없을 경우 안전하게 기본 World Space UI 자동 생성
        if (_canvas == null || _nameText == null)
        {
            CreateDefaultNamePlateUI();
        }
    }

    private void Start()
    {
        UpdateUIVisibility();
    }

    public override void OnNetworkSpawn()
    {
        _playerName.OnValueChanged += HandlePlayerNameChanged;

        if (IsOwner)
        {
            string myName = GetLocalPlayerName();
            RegisterNameServerRpc(myName);
        }

        // 초기 이름 반영
        if (!string.IsNullOrEmpty(_playerName.Value.ToString()))
        {
            ApplyPlayerName(_playerName.Value.ToString());
        }

        UpdateUIVisibility();
    }

    public override void OnNetworkDespawn()
    {
        _playerName.OnValueChanged -= HandlePlayerNameChanged;
    }

    private void LateUpdate()
    {
        if (_canvas == null || !_canvas.gameObject.activeInHierarchy)
        {
            return;
        }

        // 카메라 참조 캐싱 및 갱신
        if (_cachedCamera == null || !_cachedCamera.gameObject.activeInHierarchy)
        {
            _cachedCamera = Camera.main;
            if (_cachedCamera == null)
            {
                return;
            }
        }

        // 항상 부모 위치 기준 Offset 유지
        if (_canvasTransform != null)
        {
            _canvasTransform.position = transform.position + _offset;

            // 빌보드 처리: 카메라와 동일한 회전을 부여하여 반전 없이 언제나 정면으로 표시
            _canvasTransform.rotation = _cachedCamera.transform.rotation;
        }
    }

    private void HandlePlayerNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        ApplyPlayerName(newValue.ToString());
    }

    private void ApplyPlayerName(string nameString)
    {
        if (_nameText != null)
        {
            _nameText.text = nameString;
        }
    }

    private void UpdateUIVisibility()
    {
        if (_canvas == null)
        {
            return;
        }

        // 로컬 플레이어 본인 머리 위 닉네임 표시 여부 처리
        if (IsOwner && !_showLocalPlayerName)
        {
            _canvas.gameObject.SetActive(false);
        }
        else
        {
            _canvas.gameObject.SetActive(true);
        }
    }

    private string GetLocalPlayerName()
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.Initialized)
        {
            string personaName = Steamworks.SteamFriends.GetPersonaName();
            if (!string.IsNullOrEmpty(personaName))
            {
                return personaName;
            }
        }
#endif

        if (NetworkManager.Singleton != null)
        {
            return $"Player {NetworkManager.Singleton.LocalClientId}";
        }

        return "Player";
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = $"Player {rpcParams.Receive.SenderClientId}";
        }

        _playerName.Value = playerName;
    }

    /// <summary>
    /// Canvas 또는 Text가 없을 때 런타임에 동적으로 World Space UI를 생성합니다.
    /// </summary>
    private void CreateDefaultNamePlateUI()
    {
        var canvasGO = new GameObject("NamePlateCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = _offset;

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvasTransform = canvasGO.transform;

        // 월드 좌표계에 맞추어 스케일 조정 (1 유닛 = 100 픽셀 기준)
        _canvasTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var rectTransform = canvasGO.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(250f, 60f);
        }

        // 텍스트 생성
        var textGO = new GameObject("NameText");
        textGO.transform.SetParent(canvasGO.transform, false);

        var textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(250f, 60f);
        textRect.anchoredPosition = Vector2.zero;

        _nameText = textGO.AddComponent<TextMeshProUGUI>();
        // // // _nameText.font = null; /* TMP Font */ /* TMP Font */ /* TMP font */
        _nameText.fontSize = 24;
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = Color.white;
        _nameText.text = "Player";

        // 외곽선 효과(Outline)로 가독성 향상
        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }
}

