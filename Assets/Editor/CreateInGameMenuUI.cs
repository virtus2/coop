using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class CreateInGameMenuUI
{
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

        // 2. Menu Panel (어두운 반투명 박스)
        var panelGO = new GameObject("MenuPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);

        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 360f);

        // 3. Title Text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleText = titleGO.AddComponent<Text>();
        titleText.font = font;
        titleText.text = "PAUSE MENU";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;

        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.75f);
        titleRect.anchorMax = new Vector2(0.95f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // 4. Resume Button (계속하기)
        var resumeBtnGO = new GameObject("ResumeButton");
        resumeBtnGO.transform.SetParent(panelGO.transform, false);
        var resumeBtnImg = resumeBtnGO.AddComponent<Image>();
        resumeBtnImg.color = new Color(0.2f, 0.22f, 0.25f, 1f);
        var resumeBtn = resumeBtnGO.AddComponent<Button>();

        var resumeBtnRect = resumeBtnGO.GetComponent<RectTransform>();
        resumeBtnRect.anchorMin = new Vector2(0.12f, 0.48f);
        resumeBtnRect.anchorMax = new Vector2(0.88f, 0.68f);
        resumeBtnRect.offsetMin = Vector2.zero;
        resumeBtnRect.offsetMax = Vector2.zero;

        var resumeTxtGO = new GameObject("Text");
        resumeTxtGO.transform.SetParent(resumeBtnGO.transform, false);
        var resumeTxt = resumeTxtGO.AddComponent<Text>();
        resumeTxt.font = font;
        resumeTxt.text = "계속하기";
        resumeTxt.fontSize = 24;
        resumeTxt.fontStyle = FontStyle.Bold;
        resumeTxt.color = Color.white;
        resumeTxt.alignment = TextAnchor.MiddleCenter;

        var rtrRect = resumeTxtGO.GetComponent<RectTransform>();
        rtrRect.anchorMin = Vector2.zero;
        rtrRect.anchorMax = Vector2.one;
        rtrRect.offsetMin = Vector2.zero;
        rtrRect.offsetMax = Vector2.zero;

        // 5. Exit Button (종료하기)
        var exitBtnGO = new GameObject("ExitButton");
        exitBtnGO.transform.SetParent(panelGO.transform, false);
        var exitBtnImg = exitBtnGO.AddComponent<Image>();
        exitBtnImg.color = new Color(0.55f, 0.18f, 0.18f, 1f);
        var exitBtn = exitBtnGO.AddComponent<Button>();

        var exitBtnRect = exitBtnGO.GetComponent<RectTransform>();
        exitBtnRect.anchorMin = new Vector2(0.12f, 0.22f);
        exitBtnRect.anchorMax = new Vector2(0.88f, 0.42f);
        exitBtnRect.offsetMin = Vector2.zero;
        exitBtnRect.offsetMax = Vector2.zero;

        var exitTxtGO = new GameObject("Text");
        exitTxtGO.transform.SetParent(exitBtnGO.transform, false);
        var exitTxt = exitTxtGO.AddComponent<Text>();
        exitTxt.font = font;
        exitTxt.text = "종료하기";
        exitTxt.fontSize = 24;
        exitTxt.fontStyle = FontStyle.Bold;
        exitTxt.color = Color.white;
        exitTxt.alignment = TextAnchor.MiddleCenter;

        var etrRect = exitTxtGO.GetComponent<RectTransform>();
        etrRect.anchorMin = Vector2.zero;
        etrRect.anchorMax = Vector2.one;
        etrRect.offsetMin = Vector2.zero;
        etrRect.offsetMax = Vector2.zero;

        // 6. Status Text (저장 중 및 종료 안내)
        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(panelGO.transform, false);
        var statusText = statusGO.AddComponent<Text>();
        statusText.font = font;
        statusText.text = "";
        statusText.fontSize = 18;
        statusText.color = new Color(1f, 0.85f, 0.3f, 1f);
        statusText.alignment = TextAnchor.MiddleCenter;
        statusGO.SetActive(false);

        var stRect = statusGO.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0.05f, 0.04f);
        stRect.anchorMax = new Vector2(0.95f, 0.18f);
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        // 7. Controller 부착 및 레퍼런스 연결
        var controller = canvasGO.AddComponent<InGameMenuController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        controller.GetType().GetField("_menuPanel", flags).SetValue(controller, panelGO);
        controller.GetType().GetField("_resumeButton", flags).SetValue(controller, resumeBtn);
        controller.GetType().GetField("_exitButton", flags).SetValue(controller, exitBtn);
        controller.GetType().GetField("_statusText", flags).SetValue(controller, statusText);

        // 기본 상태에서는 패널 비활성화
        panelGO.SetActive(false);

        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("[CreateInGameMenuUI] GameScene에 인게임 메뉴 UI를 성공적으로 생성했습니다.");
    }
}
