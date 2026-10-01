using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 설치된 블록을 조준하고 F키를 길게 누를 때 화면 중앙에 철거 진행도(게이지)와 안내 문구를 표시하는 UI 컴포넌트입니다.
/// </summary>
public class DismantleProgressUI : MonoBehaviour
{
    public static DismantleProgressUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private Image _fillImage;
    [SerializeField] private TMP_Text _targetNameText;
    [SerializeField] private TMP_Text _hintText;

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
        EnsureUI();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureUI()
    {
        if (_panelRoot != null) return;

        // 씬 내 Canvas 탐색 또는 생성
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("DismantleUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // Panel Root
        _panelRoot = new GameObject("DismantleProgressPanel", typeof(RectTransform));
        _panelRoot.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -80f);
        panelRect.sizeDelta = new Vector2(280f, 60f);

        // Background
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(_panelRoot.transform, false);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImage = bgGO.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.65f);

        // Progress Slider
        GameObject sliderGO = new GameObject("ProgressBar", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(_panelRoot.transform, false);
        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0.2f);
        sliderRect.anchorMax = new Vector2(0.95f, 0.45f);
        sliderRect.sizeDelta = Vector2.zero;
        _progressSlider = sliderGO.GetComponent<Slider>();
        _progressSlider.minValue = 0f;
        _progressSlider.maxValue = 1f;

        // Slider Fill
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        _fillImage = fillGO.GetComponent<Image>();
        _fillImage.color = new Color(1f, 0.6f, 0.1f, 0.95f); // 주황빛 게이지
        _progressSlider.targetGraphic = _fillImage;
        _progressSlider.fillRect = fillRect;

        // Target Name Text
        GameObject nameTextGO = new GameObject("TargetNameText", typeof(RectTransform), typeof(Text));
        nameTextGO.transform.SetParent(_panelRoot.transform, false);
        RectTransform nameRect = nameTextGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.05f, 0.55f);
        nameRect.anchorMax = new Vector2(0.95f, 0.9f);
        nameRect.sizeDelta = Vector2.zero;
        _targetNameText = nameTextGO.GetComponent<TMP_Text>();
        // // _targetNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"); /* TMP Font */ /* TMP Font */
        _targetNameText.fontSize = 15;
        _targetNameText.alignment = TextAlignmentOptions.Center;
        _targetNameText.color = Color.white;

        // Hint Text
        GameObject hintTextGO = new GameObject("HintText", typeof(RectTransform), typeof(Text));
        hintTextGO.transform.SetParent(_panelRoot.transform, false);
        RectTransform hintRect = hintTextGO.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.05f, 0f);
        hintRect.anchorMax = new Vector2(0.95f, 0.2f);
        hintRect.sizeDelta = Vector2.zero;
        _hintText = hintTextGO.GetComponent<TMP_Text>();
        // // _hintText.font = _targetNameText.font; /* TMP Font */ /* TMP Font */
        _hintText.fontSize = 11;
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        _hintText.text = "[F] 키 유지하여 철거";
    }

    /// <summary>
    /// 철거 진행도와 대상 오브젝트 이름을 표시합니다.
    /// </summary>
    /// <param name="progress">0.0f ~ 1.0f</param>
    /// <param name="displayName">오브젝트 이름</param>
    public void SetProgress(float progress, string displayName)
    {
        EnsureUI();

        if (_panelRoot != null && !_panelRoot.activeSelf)
        {
            _panelRoot.SetActive(true);
        }

        if (_progressSlider != null)
        {
            _progressSlider.value = Mathf.Clamp01(progress);
        }

        if (_targetNameText != null)
        {
            _targetNameText.text = $"철거 중: {displayName}";
        }
    }

    /// <summary>
    /// 진행도 UI를 화면에서 숨깁니다.
    /// </summary>
    public void Hide()
    {
        if (_panelRoot != null && _panelRoot.activeSelf)
        {
            _panelRoot.SetActive(false);
        }

        if (_progressSlider != null)
        {
            _progressSlider.value = 0f;
        }
    }
}
