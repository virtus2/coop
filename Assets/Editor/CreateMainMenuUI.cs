using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class CreateMainMenuUI
{
    private const string OPTION_PREFAB_PATH = "Assets/Prefabs/OptionWindow.prefab";
    private const string MAIN_SCENE_PATH = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Create Main Menu UI")]
    public static void CreateUI()
    {
        var currentScene = SceneManager.GetActiveScene();
        if (currentScene.path != MAIN_SCENE_PATH)
        {
            if (EditorUtility.DisplayDialog("씬 전환", "MainScene을 열어 메인 메뉴 UI를 생성하시겠습니까?", "확인", "취소"))
            {
                currentScene = EditorSceneManager.OpenScene(MAIN_SCENE_PATH);
            }
            else
            {
                return;
            }
        }

        // OptionWindow 프리팹 확인
        var optionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OPTION_PREFAB_PATH);
        if (optionPrefab == null)
        {
            Debug.LogWarning("[CreateMainMenuUI] OptionWindow 프리팹이 없어 CreateInGameMenuUI에서 생성합니다.");
            optionPrefab = CreateInGameMenuUI.CreateOrUpdateOptionWindowPrefab();
        }

        // 기존 MainMenuCanvas가 있으면 제거
        var existingCanvas = GameObject.Find("MainMenuCanvas");
        if (existingCanvas != null)
        {
            Undo.DestroyObjectImmediate(existingCanvas);
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. MainMenuCanvas 생성
        var canvasGO = new GameObject("MainMenuCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Main Menu UI");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 2. MenuPanel (중앙 패널)
        var panelGO = new GameObject("MenuPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);

        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(480f, 520f);

        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 36, 36);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var csf = panelGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 3. Title Text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleText = titleGO.AddComponent<Text>();
        titleText.font = font;
        titleText.text = "MAIN MENU";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 50f;

        // 4. Host Lobby Button (로비 만들기)
        var hostBtn = CreateButton(panelGO.transform, font, "HostLobbyButton", "로비 만들기", 
            new Color(0.18f, 0.45f, 0.82f, 1f), 54f);

        // 5. Lobby ID Input Field (로비 숫자 입력하는 칸)
        var inputField = CreateInputField(panelGO.transform, font, "LobbyIdInputField", "로비 번호 입력...", 50f);

        // 6. Join Lobby Button (로비 참가하기)
        var joinBtn = CreateButton(panelGO.transform, font, "JoinLobbyButton", "로비 참가하기", 
            new Color(0.22f, 0.58f, 0.38f, 1f), 54f);

        // 7. Option Button (옵션)
        var optionBtn = CreateButton(panelGO.transform, font, "OptionButton", "옵션", 
            new Color(0.24f, 0.26f, 0.30f, 1f), 54f);

        // 8. Quit Button (게임 종료)
        var quitBtn = CreateButton(panelGO.transform, font, "QuitButton", "게임 종료", 
            new Color(0.62f, 0.22f, 0.22f, 1f), 54f);

        // 9. OptionWindow 프리팹 인스턴스화
        var optionWindowGO = (GameObject)PrefabUtility.InstantiatePrefab(optionPrefab, canvasGO.transform);
        optionWindowGO.name = "OptionWindow";
        var optionWindowUI = optionWindowGO.GetComponent<OptionWindowUI>();
        optionWindowGO.SetActive(false);

        // 10. MainMenuUIController 부착 및 필드 바인딩
        var controller = canvasGO.AddComponent<MainMenuUIController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        var networkBootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();

        controller.GetType().GetField("_networkBootstrap", flags).SetValue(controller, networkBootstrap);
        controller.GetType().GetField("_menuPanel", flags).SetValue(controller, panelGO);
        controller.GetType().GetField("_hostLobbyButton", flags).SetValue(controller, hostBtn);
        controller.GetType().GetField("_lobbyIdInputField", flags).SetValue(controller, inputField);
        controller.GetType().GetField("_joinLobbyButton", flags).SetValue(controller, joinBtn);
        controller.GetType().GetField("_optionButton", flags).SetValue(controller, optionBtn);
        controller.GetType().GetField("_quitButton", flags).SetValue(controller, quitBtn);
        controller.GetType().GetField("_optionWindow", flags).SetValue(controller, optionWindowUI);

        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);

        Debug.Log("[CreateMainMenuUI] MainScene에 UGUI 메인 메뉴가 성공적으로 생성 및 저장되었습니다!");
    }

    private static Button CreateButton(Transform parent, Font font, string name, string label, Color baseColor, float height)
    {
        var btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        var img = btnGO.AddComponent<Image>();
        img.color = baseColor;

        var btn = btnGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;

        var le = btnGO.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(btnGO.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.font = font;
        txt.text = label;
        txt.fontSize = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        var txtRect = txtGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        return btn;
    }

    private static InputField CreateInputField(Transform parent, Font font, string name, string placeholderText, float height)
    {
        var inputGO = new GameObject(name);
        inputGO.transform.SetParent(parent, false);

        var bgImg = inputGO.AddComponent<Image>();
        bgImg.color = new Color(0.14f, 0.16f, 0.19f, 1f);

        var inputField = inputGO.AddComponent<InputField>();
        inputField.contentType = InputField.ContentType.IntegerNumber;

        var le = inputGO.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        // Text Component
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, false);
        var textComp = textGO.AddComponent<Text>();
        textComp.font = font;
        textComp.fontSize = 20;
        textComp.color = Color.white;
        textComp.alignment = TextAnchor.MiddleLeft;
        textComp.supportRichText = false;

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);

        // Placeholder Component
        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(inputGO.transform, false);
        var phComp = phGO.AddComponent<Text>();
        phComp.font = font;
        phComp.fontSize = 20;
        phComp.fontStyle = FontStyle.Italic;
        phComp.color = new Color(0.6f, 0.65f, 0.7f, 0.6f);
        phComp.alignment = TextAnchor.MiddleLeft;
        phComp.text = placeholderText;

        var phRect = phGO.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(16f, 0f);
        phRect.offsetMax = new Vector2(-16f, 0f);

        inputField.textComponent = textComp;
        inputField.placeholder = phComp;

        return inputField;
    }
}
