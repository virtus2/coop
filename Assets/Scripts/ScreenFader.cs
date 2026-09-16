using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 전환 및 화면 페이드인/페이드아웃을 전담하는 싱글톤 매니저입니다.
/// 최상단 ScreenSpaceOverlay Canvas를 동적으로 생성하여 씬 전환 중에도 깜빡임 없이 유지됩니다.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader _instance;
    public static ScreenFader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ScreenFader>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[ScreenFader]");
                    _instance = go.AddComponent<ScreenFader>();
                }
            }
            return _instance;
        }
    }

    [Header("Fade Settings")]
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Image _fadeImage;

    private Coroutine _fadeCoroutine;
    private bool _isTransitioningToGame = false;
    private float _targetFadeInDuration = 1.5f;

    public float CurrentAlpha => _canvasGroup != null ? _canvasGroup.alpha : 0f;
    public bool IsFading => _fadeCoroutine != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void InitializeUI()
    {
        if (_canvas != null) return;

        // 1. Canvas 설정
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767; // 최상단에 렌더링

        // 2. CanvasScaler 설정
        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 3. CanvasGroup 설정
        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        // 4. Fade Image 설정 (검은색 전체화면)
        GameObject imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(transform, false);

        _fadeImage = imgGo.AddComponent<Image>();
        _fadeImage.color = Color.black;
        _fadeImage.raycastTarget = true;

        RectTransform rect = _fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 로비에서 게임 시작 시 호출: 페이드아웃 후 GameScene 로드 시 자동 페이드인 준비
    /// </summary>
    public void StartGameTransition(float fadeOutDuration = 1.5f, float fadeInDuration = 1.5f)
    {
        _isTransitioningToGame = true;
        _targetFadeInDuration = fadeInDuration;
        FadeOut(fadeOutDuration);
    }

    /// <summary>
    /// 화면을 검은색으로 어둡게 만듭니다 (알파 0 -> 1)
    /// </summary>
    public void FadeOut(float duration, Action onComplete = null)
    {
        InitializeUI();
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(1f, duration, onComplete));
    }

    /// <summary>
    /// 화면을 밝게 만듭니다 (알파 1 -> 0)
    /// </summary>
    public void FadeIn(float duration, Action onComplete = null)
    {
        InitializeUI();
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(0f, duration, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onComplete)
    {
        if (_canvasGroup == null) yield break;

        float startAlpha = _canvasGroup.alpha;
        _canvasGroup.blocksRaycasts = true;

        if (duration <= 0f)
        {
            _canvasGroup.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }
            _canvasGroup.alpha = targetAlpha;
        }

        // 완전히 투명해졌을 때 클릭 입력 차단 해제
        if (Mathf.Approximately(targetAlpha, 0f))
        {
            _canvasGroup.blocksRaycasts = false;
        }

        _fadeCoroutine = null;
        onComplete?.Invoke();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // GameScene에 진입했을 때 전환 페이드인 처리
        if (scene.name == "GameScene" && (_isTransitioningToGame || CurrentAlpha > 0.9f))
        {
            _isTransitioningToGame = false;
            StartCoroutine(WaitForFrameAndFadeIn());
        }
    }

    private IEnumerator WaitForFrameAndFadeIn()
    {
        // 씬 내 카메라, 오브젝트, 렌더링 등이 안정화되도록 1프레임 대기
        yield return null;
        FadeIn(_targetFadeInDuration);
    }
}
