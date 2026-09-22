using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerHUDCanvas 프리팹을 생성하고 GameScene에 배치하는 에디터 유틸리티입니다.
/// </summary>
[InitializeOnLoad]
public static class SetupPlayerHUD
{
    public const string PREFAB_PATH = "Assets/Prefabs/PlayerHUDCanvas.prefab";
    public const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";
    public const string FONT_ASSET_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static SetupPlayerHUD()
    {
        EditorApplication.delayCall += OnEditorReady;
    }

    private static void OnEditorReady()
    {
        if (!File.Exists(PREFAB_PATH))
        {
            ExecuteSetup();
        }
    }

    [MenuItem("Tools/Setup Player HUD Canvas")]
    public static void ExecuteSetup()
    {
        // 1. 프리팹 생성/갱신
        GameObject prefab = CreateOrUpdatePlayerHUDCanvasPrefab();
        if (prefab == null)
        {
            Debug.LogError("[SetupPlayerHUD] PlayerHUDCanvas 프리팹 생성 실패!");
            return;
        }

        // 2. GameScene에 배치
        PlaceInGameScene(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[SetupPlayerHUD] PlayerHUDCanvas 생성 및 GameScene 배치 완료!</color>");
    }

    public static GameObject CreateOrUpdatePlayerHUDCanvasPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
        if (fontAsset == null)
        {
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        // Canvas Root
        GameObject canvasGO = new GameObject("PlayerHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // 인벤토리(60)보다 아래, 일반 화면보다 위

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // AmmoHUD Root
        GameObject ammoHUDGO = new GameObject("AmmoHUD", typeof(RectTransform), typeof(AmmoHUD));
        ammoHUDGO.transform.SetParent(canvasGO.transform, false);
        RectTransform ammoHUDRect = ammoHUDGO.GetComponent<RectTransform>();
        ammoHUDRect.anchorMin = Vector2.zero;
        ammoHUDRect.anchorMax = Vector2.one;
        ammoHUDRect.offsetMin = Vector2.zero;
        ammoHUDRect.offsetMax = Vector2.zero;

        AmmoHUD ammoHUDComp = ammoHUDGO.GetComponent<AmmoHUD>();

        // AmmoPanel (우측 하단 고정)
        GameObject panelGO = new GameObject("AmmoPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(ammoHUDGO.transform, false);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-40f, 40f);
        panelRect.sizeDelta = new Vector2(240f, 90f);

        Image panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.65f);

        // StatusText (재장전 / 차징)
        GameObject statusGO = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        statusGO.transform.SetParent(panelGO.transform, false);
        RectTransform statusRect = statusGO.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0.6f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.offsetMin = new Vector2(10f, 0f);
        statusRect.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI statusText = statusGO.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) statusText.font = fontAsset;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 16f;
        statusText.fontStyle = FontStyles.Bold;
        statusText.text = "RELOADING... (0%)";
        statusGO.SetActive(false);

        // AmmoText (30 / 120)
        GameObject textGO = new GameObject("AmmoText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panelGO.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0.7f);
        textRect.offsetMin = new Vector2(10f, 5f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI ammoText = textGO.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) ammoText.font = fontAsset;
        ammoText.alignment = TextAlignmentOptions.Center;
        ammoText.fontSize = 32f;
        ammoText.fontStyle = FontStyles.Bold;
        ammoText.text = "30 / 120";

        // 직렬화 프로퍼티 바인딩
        SerializedObject so = new SerializedObject(ammoHUDComp);
        so.FindProperty("_ammoPanel").objectReferenceValue = panelGO;
        so.FindProperty("_ammoText").objectReferenceValue = ammoText;
        so.FindProperty("_statusText").objectReferenceValue = statusText;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 초기에는 비활성화
        panelGO.SetActive(false);

        // 프리팹 저장
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, PREFAB_PATH);
        Object.DestroyImmediate(canvasGO);

        return prefab;
    }

    public static void PlaceInGameScene(GameObject prefab)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        bool openedScene = false;

        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            openedScene = true;
        }

        var existing = GameObject.Find("PlayerHUDCanvas");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeScene);
        instance.name = "PlayerHUDCanvas";
        Undo.RegisterCreatedObjectUndo(instance, "Create Player HUD Canvas");

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"[SetupPlayerHUD] {GAME_SCENE_PATH}에 PlayerHUDCanvas 배치 및 씬 저장 완료.");
    }
}
