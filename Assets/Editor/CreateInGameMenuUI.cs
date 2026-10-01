using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.IO;

public class CreateInGameMenuUI
{
    private const string OPTION_PREFAB_PATH = "Assets/Prefabs/OptionWindow.prefab";

    [MenuItem("Tools/Create Option Window Prefab")]
    public static GameObject CreateOrUpdateOptionWindowPrefab()
    {
        // Prefabs 폴더 확인 및 생성
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. Root Container (전체 화면 앵커)
        var rootGO = new GameObject("OptionWindow");
        var rootRect = rootGO.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // 2. Window Panel (중앙 패널)
        var panelGO = new GameObject("WindowPanel");
        panelGO.transform.SetParent(rootGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.08f, 0.94f);

        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(480f, 360f);

        // 3. Title Text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        // titleText.font = font; /* TMP Font */
        titleText.text = "OPTIONS";
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;

        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.78f);
        titleRect.anchorMax = new Vector2(0.95f, 0.94f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // 4. Sensitivity Container
        var sensContainerGO = new GameObject("SensitivityContainer");
        sensContainerGO.transform.SetParent(panelGO.transform, false);
        var sensContainerRect = sensContainerGO.AddComponent<RectTransform>();
        sensContainerRect.anchorMin = new Vector2(0.08f, 0.40f);
        sensContainerRect.anchorMax = new Vector2(0.92f, 0.70f);
        sensContainerRect.offsetMin = Vector2.zero;
        sensContainerRect.offsetMax = Vector2.zero;

        // 4-1. Label Text
        var sensLabelGO = new GameObject("SensitivityLabel");
        sensLabelGO.transform.SetParent(sensContainerGO.transform, false);
        var sensLabelText = sensLabelGO.AddComponent<TextMeshProUGUI>();
        // sensLabelText.font = font; /* TMP Font */
        sensLabelText.text = "마우스 감도";
        sensLabelText.fontSize = 20;
        sensLabelText.fontStyle = FontStyles.Bold;
        sensLabelText.color = Color.white;
        sensLabelText.alignment = TextAlignmentOptions.Left;

        var sensLabelRect = sensLabelGO.GetComponent<RectTransform>();
        sensLabelRect.anchorMin = new Vector2(0f, 0.55f);
        sensLabelRect.anchorMax = new Vector2(0.7f, 1f);
        sensLabelRect.offsetMin = Vector2.zero;
        sensLabelRect.offsetMax = Vector2.zero;

        // 4-2. Value Text
        var sensValueGO = new GameObject("SensitivityValueText");
        sensValueGO.transform.SetParent(sensContainerGO.transform, false);
        var sensValueText = sensValueGO.AddComponent<TextMeshProUGUI>();
        // sensValueText.font = font; /* TMP Font */
        sensValueText.text = $"{SettingsManager.DEFAULT_MOUSE_SENSITIVITY:0.00}";
        sensValueText.fontSize = 20;
        sensValueText.fontStyle = FontStyles.Bold;
        sensValueText.color = new Color(0.4f, 0.8f, 1f, 1f);
        sensValueText.alignment = TextAlignmentOptions.Right;

        var sensValueRect = sensValueGO.GetComponent<RectTransform>();
        sensValueRect.anchorMin = new Vector2(0.7f, 0.55f);
        sensValueRect.anchorMax = new Vector2(1f, 1f);
        sensValueRect.offsetMin = Vector2.zero;
        sensValueRect.offsetMax = Vector2.zero;

        // 4-3. Slider
        var sliderGO = new GameObject("SensitivitySlider");
        sliderGO.transform.SetParent(sensContainerGO.transform, false);
        var sliderRect = sliderGO.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0.45f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<Slider>();

        // Slider Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.18f, 0.20f, 0.24f, 1f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Slider Fill Area
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.55f, 0.95f, 1f);
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Slider Handle Area
        var handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(0.95f, 0.95f, 0.98f, 1f);
        var handleRect = handleGO.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(20f, 26f);

        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = SettingsManager.MIN_MOUSE_SENSITIVITY;
        slider.maxValue = SettingsManager.MAX_MOUSE_SENSITIVITY;
        slider.value = SettingsManager.DEFAULT_MOUSE_SENSITIVITY;
        slider.targetGraphic = handleImg;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;

        // 5. Close Button (닫기)
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(panelGO.transform, false);
        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.2f, 0.22f, 0.25f, 1f);
        var closeBtn = closeBtnGO.AddComponent<Button>();

        var closeBtnRect = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0.25f, 0.08f);
        closeBtnRect.anchorMax = new Vector2(0.75f, 0.24f);
        closeBtnRect.offsetMin = Vector2.zero;
        closeBtnRect.offsetMax = Vector2.zero;

        var closeTxtGO = new GameObject("Text");
        closeTxtGO.transform.SetParent(closeBtnGO.transform, false);
        var closeTxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
        // closeTxt.font = font; /* TMP Font */
        closeTxt.text = "닫기";
        closeTxt.fontSize = 22;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;

        var ctrRect = closeTxtGO.GetComponent<RectTransform>();
        ctrRect.anchorMin = Vector2.zero;
        ctrRect.anchorMax = Vector2.one;
        ctrRect.offsetMin = Vector2.zero;
        ctrRect.offsetMax = Vector2.zero;

        // 6. OptionWindowUI 컴포넌트 부착 및 필드 할당
        var optionUI = rootGO.AddComponent<OptionWindowUI>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        optionUI.GetType().GetField("_windowPanel", flags).SetValue(optionUI, rootGO);
        optionUI.GetType().GetField("_mouseSensitivitySlider", flags).SetValue(optionUI, slider);
        optionUI.GetType().GetField("_mouseSensitivityValueText", flags).SetValue(optionUI, sensValueText);
        optionUI.GetType().GetField("_closeButton", flags).SetValue(optionUI, closeBtn);

        // Prefab으로 저장
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, OPTION_PREFAB_PATH);
        Object.DestroyImmediate(rootGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateInGameMenuUI] OptionWindow 프리팹을 생성/저장했습니다: {OPTION_PREFAB_PATH}");
        return savedPrefab;
    }

    [MenuItem("Tools/Create In-Game Menu UI")]
    public static void CreateUI()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "GameScene")
        {
            if (!EditorUtility.DisplayDialog("경고", $"현재 활성 씬이 {activeScene.name} 입니다. GameScene이 맞습니까?", "계속 진행", "취소"))
            {
                return;
            }
        }

        // OptionWindow 프리팹 준비 (없으면 생성)
        var optionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OPTION_PREFAB_PATH);
        if (optionPrefab == null)
        {
            optionPrefab = CreateOrUpdateOptionWindowPrefab();
        }

        // 기존에 혹시 생성되어 있던 메뉴가 있다면 제거 후 재생성
        var existing = GameObject.Find("InGameMenuCanvas");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        // 1. Canvas 생성
        var canvasGO = new GameObject("InGameMenuCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create In-Game Menu UI");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 최상단 노출

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGO.AddComponent<GraphicRaycaster>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 2. Menu Panel (어두운 반투명 박스) - 높이를 440으로 확장하여 옵션 버튼 공간 확보
        var panelGO = new GameObject("MenuPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.08f, 0.90f);

        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 440f);

        // 3. Title Text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        // titleText.font = font; /* TMP Font */
        titleText.text = "PAUSE MENU";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;

        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.80f);
        titleRect.anchorMax = new Vector2(0.95f, 0.96f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // 4. Resume Button (계속하기)
        var resumeBtnGO = new GameObject("ResumeButton");
        resumeBtnGO.transform.SetParent(panelGO.transform, false);
        var resumeBtnImg = resumeBtnGO.AddComponent<Image>();
        resumeBtnImg.color = new Color(0.2f, 0.22f, 0.25f, 1f);
        var resumeBtn = resumeBtnGO.AddComponent<Button>();

        var resumeBtnRect = resumeBtnGO.GetComponent<RectTransform>();
        resumeBtnRect.anchorMin = new Vector2(0.12f, 0.60f);
        resumeBtnRect.anchorMax = new Vector2(0.88f, 0.74f);
        resumeBtnRect.offsetMin = Vector2.zero;
        resumeBtnRect.offsetMax = Vector2.zero;

        var resumeTxtGO = new GameObject("Text");
        resumeTxtGO.transform.SetParent(resumeBtnGO.transform, false);
        var resumeTxt = resumeTxtGO.AddComponent<TextMeshProUGUI>();
        // resumeTxt.font = font; /* TMP Font */
        resumeTxt.text = "계속하기";
        resumeTxt.fontSize = 24;
        resumeTxt.fontStyle = FontStyles.Bold;
        resumeTxt.color = Color.white;
        resumeTxt.alignment = TextAlignmentOptions.Center;

        var rtrRect = resumeTxtGO.GetComponent<RectTransform>();
        rtrRect.anchorMin = Vector2.zero;
        rtrRect.anchorMax = Vector2.one;
        rtrRect.offsetMin = Vector2.zero;
        rtrRect.offsetMax = Vector2.zero;

        // 5. Option Button (옵션)
        var optionBtnGO = new GameObject("OptionButton");
        optionBtnGO.transform.SetParent(panelGO.transform, false);
        var optionBtnImg = optionBtnGO.AddComponent<Image>();
        optionBtnImg.color = new Color(0.2f, 0.22f, 0.25f, 1f);
        var optionBtn = optionBtnGO.AddComponent<Button>();

        var optionBtnRect = optionBtnGO.GetComponent<RectTransform>();
        optionBtnRect.anchorMin = new Vector2(0.12f, 0.42f);
        optionBtnRect.anchorMax = new Vector2(0.88f, 0.56f);
        optionBtnRect.offsetMin = Vector2.zero;
        optionBtnRect.offsetMax = Vector2.zero;

        var optionTxtGO = new GameObject("Text");
        optionTxtGO.transform.SetParent(optionBtnGO.transform, false);
        var optionTxt = optionTxtGO.AddComponent<TextMeshProUGUI>();
        // optionTxt.font = font; /* TMP Font */
        optionTxt.text = "옵션";
        optionTxt.fontSize = 24;
        optionTxt.fontStyle = FontStyles.Bold;
        optionTxt.color = Color.white;
        optionTxt.alignment = TextAlignmentOptions.Center;

        var otrRect = optionTxtGO.GetComponent<RectTransform>();
        otrRect.anchorMin = Vector2.zero;
        otrRect.anchorMax = Vector2.one;
        otrRect.offsetMin = Vector2.zero;
        otrRect.offsetMax = Vector2.zero;

        // 6. Exit Button (종료하기)
        var exitBtnGO = new GameObject("ExitButton");
        exitBtnGO.transform.SetParent(panelGO.transform, false);
        var exitBtnImg = exitBtnGO.AddComponent<Image>();
        exitBtnImg.color = new Color(0.55f, 0.18f, 0.18f, 1f);
        var exitBtn = exitBtnGO.AddComponent<Button>();

        var exitBtnRect = exitBtnGO.GetComponent<RectTransform>();
        exitBtnRect.anchorMin = new Vector2(0.12f, 0.24f);
        exitBtnRect.anchorMax = new Vector2(0.88f, 0.38f);
        exitBtnRect.offsetMin = Vector2.zero;
        exitBtnRect.offsetMax = Vector2.zero;

        var exitTxtGO = new GameObject("Text");
        exitTxtGO.transform.SetParent(exitBtnGO.transform, false);
        var exitTxt = exitTxtGO.AddComponent<TextMeshProUGUI>();
        // exitTxt.font = font; /* TMP Font */
        exitTxt.text = "종료하기";
        exitTxt.fontSize = 24;
        exitTxt.fontStyle = FontStyles.Bold;
        exitTxt.color = Color.white;
        exitTxt.alignment = TextAlignmentOptions.Center;

        var etrRect = exitTxtGO.GetComponent<RectTransform>();
        etrRect.anchorMin = Vector2.zero;
        etrRect.anchorMax = Vector2.one;
        etrRect.offsetMin = Vector2.zero;
        etrRect.offsetMax = Vector2.zero;

        // 7. Status Text (저장 중 및 종료 안내)
        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(panelGO.transform, false);
        var statusText = statusGO.AddComponent<TextMeshProUGUI>();
        // statusText.font = font; /* TMP Font */
        statusText.text = "";
        statusText.fontSize = 18;
        statusText.color = new Color(1f, 0.85f, 0.3f, 1f);
        statusText.alignment = TextAlignmentOptions.Center;
        statusGO.SetActive(false);

        var stRect = statusGO.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0.05f, 0.04f);
        stRect.anchorMax = new Vector2(0.95f, 0.18f);
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        // 8. OptionWindow 프리팹 인스턴스화
        var optionWindowInstance = (GameObject)PrefabUtility.InstantiatePrefab(optionPrefab, canvasGO.transform);
        optionWindowInstance.name = "OptionWindow";
        var optionUIComp = optionWindowInstance.GetComponent<OptionWindowUI>();
        optionWindowInstance.SetActive(false);

        // 9. Controller 부착 및 레퍼런스 연결
        var controller = canvasGO.AddComponent<InGameMenuController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        controller.GetType().GetField("_menuPanel", flags).SetValue(controller, panelGO);
        controller.GetType().GetField("_resumeButton", flags).SetValue(controller, resumeBtn);
        controller.GetType().GetField("_optionButton", flags).SetValue(controller, optionBtn);
        controller.GetType().GetField("_exitButton", flags).SetValue(controller, exitBtn);
        controller.GetType().GetField("_statusText", flags).SetValue(controller, statusText);
        controller.GetType().GetField("_optionWindow", flags).SetValue(controller, optionUIComp);

        // 기본 상태에서는 패널 비활성화
        panelGO.SetActive(false);

        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("[CreateInGameMenuUI] GameScene에 인게임 메뉴 UI 및 모듈형 옵션창 UI를 성공적으로 생성했습니다.");
    }
}

