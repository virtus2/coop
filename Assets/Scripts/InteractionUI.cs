using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 중앙의 조준선(크로스헤어) 및 상호작용 키 안내(HUD)를 관리하는 UI 컴포넌트입니다.
/// </summary>
public class InteractionUI : MonoBehaviour
{
    public static InteractionUI Instance { get; private set; }

    [Header("Crosshair")]
    [SerializeField] private GameObject _crosshairRoot;

    [Header("Prompt UI")]
    [SerializeField] private GameObject _promptPanel;
    [SerializeField] private Text _keyText;
    [SerializeField] private Text _promptText;

    [Header("Held Item UI")]
    [SerializeField] private GameObject _heldHintPanel;
    [SerializeField] private Text _heldHintText;

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

        // 시작 시 프롬프트 및 들기 힌트는 비활성화
        if (_promptPanel != null)
        {
            _promptPanel.SetActive(false);
        }

        if (_heldHintPanel != null)
        {
            _heldHintPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 상호작용 가능한 물체에 조준선이 위치할 때 상호작용 키와 설명 텍스트를 표시합니다.
    /// </summary>
    /// <param name="keyName">키 이름 (기본: "E")</param>
    /// <param name="actionText">동작 설명 (예: "들기", "상호작용", "열기")</param>
    public void ShowPrompt(string keyName, string actionText)
    {
        if (_promptPanel == null)
        {
            return;
        }

        if (_keyText != null)
        {
            _keyText.text = keyName;
        }

        if (_promptText != null)
        {
            _promptText.text = actionText;
        }

        if (!_promptPanel.activeSelf)
        {
            _promptPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 조준선이 물체에서 벗어나거나 상호작용이 불가능할 때 프롬프트를 즉시 숨깁니다.
    /// </summary>
    public void HidePrompt()
    {
        if (_promptPanel != null && _promptPanel.activeSelf)
        {
            _promptPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 물체를 손에 들고 있을 때 내려놓기 안내 텍스트를 표시합니다.
    /// </summary>
    public void ShowHeldHint(string hintText = "클릭하여 내려놓기")
    {
        if (_heldHintPanel == null)
        {
            return;
        }

        if (_heldHintText != null)
        {
            _heldHintText.text = hintText;
        }

        if (!_heldHintPanel.activeSelf)
        {
            _heldHintPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 물체를 내려놓았을 때 안내 텍스트를 숨깁니다.
    /// </summary>
    public void HideHeldHint()
    {
        if (_heldHintPanel != null && _heldHintPanel.activeSelf)
        {
            _heldHintPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 씬에 InteractionUI가 없는 경우 동적으로 기본 HUD를 생성합니다.
    /// </summary>
    public static InteractionUI EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        InteractionUI existing = FindAnyObjectByType<InteractionUI>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return CreateDefaultHUD();
    }

    private static InteractionUI CreateDefaultHUD()
    {
        var canvasGO = new GameObject("InteractionHUD");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGO.AddComponent<GraphicRaycaster>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. Crosshair Dot (화면 중앙)
        var crosshairGO = new GameObject("CrosshairDot");
        crosshairGO.transform.SetParent(canvasGO.transform, false);
        var crosshairImg = crosshairGO.AddComponent<Image>();
        crosshairImg.color = new Color(1f, 1f, 1f, 0.85f);
        var crosshairRect = crosshairGO.GetComponent<RectTransform>();
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.sizeDelta = new Vector2(6f, 6f);
        crosshairRect.anchoredPosition = Vector2.zero;

        // 2. Prompt Panel (조준선 아래 위치)
        var promptGO = new GameObject("PromptPanel");
        promptGO.transform.SetParent(canvasGO.transform, false);
        var promptBg = promptGO.AddComponent<Image>();
        promptBg.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);
        var promptRect = promptGO.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = new Vector2(220f, 44f);
        promptRect.anchoredPosition = new Vector2(0f, -60f);

        // Key Box
        var keyBoxGO = new GameObject("KeyBox");
        keyBoxGO.transform.SetParent(promptGO.transform, false);
        var keyBoxImg = keyBoxGO.AddComponent<Image>();
        keyBoxImg.color = new Color(0.25f, 0.25f, 0.3f, 0.95f);
        var keyBoxRect = keyBoxGO.GetComponent<RectTransform>();
        keyBoxRect.anchorMin = new Vector2(0.05f, 0.15f);
        keyBoxRect.anchorMax = new Vector2(0.28f, 0.85f);
        keyBoxRect.offsetMin = Vector2.zero;
        keyBoxRect.offsetMax = Vector2.zero;

        var keyTextGO = new GameObject("KeyText");
        keyTextGO.transform.SetParent(keyBoxGO.transform, false);
        var keyText = keyTextGO.AddComponent<Text>();
        keyText.font = font;
        keyText.text = "E";
        keyText.fontSize = 20;
        keyText.fontStyle = FontStyle.Bold;
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.color = Color.white;
        var keyTextRect = keyTextGO.GetComponent<RectTransform>();
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.offsetMin = Vector2.zero;
        keyTextRect.offsetMax = Vector2.zero;

        // Action Text
        var actionTextGO = new GameObject("ActionText");
        actionTextGO.transform.SetParent(promptGO.transform, false);
        var actionText = actionTextGO.AddComponent<Text>();
        actionText.font = font;
        actionText.text = "상호작용";
        actionText.fontSize = 18;
        actionText.fontStyle = FontStyle.Normal;
        actionText.alignment = TextAnchor.MiddleLeft;
        actionText.color = Color.white;
        var actionTextRect = actionTextGO.GetComponent<RectTransform>();
        actionTextRect.anchorMin = new Vector2(0.33f, 0f);
        actionTextRect.anchorMax = new Vector2(0.95f, 1f);
        actionTextRect.offsetMin = Vector2.zero;
        actionTextRect.offsetMax = Vector2.zero;

        // 3. Held Item Hint Panel (물체 들고 있을 때)
        var heldGO = new GameObject("HeldHintPanel");
        heldGO.transform.SetParent(canvasGO.transform, false);
        var heldBg = heldGO.AddComponent<Image>();
        heldBg.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);
        var heldRect = heldGO.GetComponent<RectTransform>();
        heldRect.anchorMin = new Vector2(0.5f, 0.5f);
        heldRect.anchorMax = new Vector2(0.5f, 0.5f);
        heldRect.pivot = new Vector2(0.5f, 0.5f);
        heldRect.sizeDelta = new Vector2(260f, 38f);
        heldRect.anchoredPosition = new Vector2(0f, -110f);

        var heldTextGO = new GameObject("HeldText");
        heldTextGO.transform.SetParent(heldGO.transform, false);
        var heldText = heldTextGO.AddComponent<Text>();
        heldText.font = font;
        heldText.text = "[좌클릭] 바닥에 내려놓기";
        heldText.fontSize = 16;
        heldText.fontStyle = FontStyle.Normal;
        heldText.alignment = TextAnchor.MiddleCenter;
        heldText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var heldTextRect = heldTextGO.GetComponent<RectTransform>();
        heldTextRect.anchorMin = Vector2.zero;
        heldTextRect.anchorMax = Vector2.one;
        heldTextRect.offsetMin = Vector2.zero;
        heldTextRect.offsetMax = Vector2.zero;

        // InteractionUI 컴포넌트 부착
        var ui = canvasGO.AddComponent<InteractionUI>();
        ui._crosshairRoot = crosshairGO;
        ui._promptPanel = promptGO;
        ui._keyText = keyText;
        ui._promptText = actionText;
        ui._heldHintPanel = heldGO;
        ui._heldHintText = heldText;

        promptGO.SetActive(false);
        heldGO.SetActive(false);

        Instance = ui;
        return ui;
    }
}
