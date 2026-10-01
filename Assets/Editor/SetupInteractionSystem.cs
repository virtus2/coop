using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class SetupInteractionSystem
{
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string PICKABLE_BOX_PREFAB_PATH = "Assets/Prefabs/PickableBox.prefab";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Setup Complete Interaction System")]
    public static void ExecuteCompleteSetup()
    {
        UpdatePlayerPrefab();
        GameObject pickableBoxPrefab = CreateOrUpdatePickableBoxPrefab();
        SetupGameScene(pickableBoxPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupInteractionSystem] 상호작용 시스템(플레이어 프리팹, PickableBox 프리팹, GameScene HUD 및 샘플 오브젝트) 전체 셋업 완료!");
    }

    [MenuItem("Tools/Update PlayerPrefab with Interaction")]
    public static void UpdatePlayerPrefab()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (playerPrefab == null)
        {
            Debug.LogError($"[SetupInteractionSystem] {PLAYER_PREFAB_PATH}을 찾을 수 없습니다.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(playerPrefab);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            // 1. PlayerInteraction 컴포넌트 추가
            var playerInteraction = prefabRoot.GetComponent<PlayerInteraction>();
            if (playerInteraction == null)
            {
                playerInteraction = prefabRoot.AddComponent<PlayerInteraction>();
            }

            // 2. CameraTarget 하위에 HoldPoint 생성/탐색
            Transform cameraTarget = prefabRoot.transform.Find("CameraTarget");
            Transform holdPoint = null;
            if (cameraTarget != null)
            {
                holdPoint = cameraTarget.Find("HoldPoint");
                if (holdPoint == null)
                {
                    var holdGO = new GameObject("HoldPoint");
                    holdGO.transform.SetParent(cameraTarget, false);
                    holdGO.transform.localPosition = new Vector3(0.32f, -0.25f, 0.65f);
                    holdGO.transform.localRotation = Quaternion.identity;
                    holdPoint = holdGO.transform;
                }
            }
            else
            {
                holdPoint = prefabRoot.transform.Find("HoldPoint");
                if (holdPoint == null)
                {
                    var holdGO = new GameObject("HoldPoint");
                    holdGO.transform.SetParent(prefabRoot.transform, false);
                    holdGO.transform.localPosition = new Vector3(0.32f, 1.2f, 0.65f);
                    holdPoint = holdGO.transform;
                }
            }

            // 필드 할당 (리플렉션)
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(PlayerInteraction).GetField("_holdPoint", flags)?.SetValue(playerInteraction, holdPoint);
            typeof(PlayerInteraction).GetField("_interactionRange", flags)?.SetValue(playerInteraction, 4.5f);
            typeof(PlayerInteraction).GetField("_maxDropReach", flags)?.SetValue(playerInteraction, 5.0f);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            Debug.Log("[SetupInteractionSystem] PlayerPrefab에 PlayerInteraction 및 HoldPoint 설정 완료.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem("Tools/Create Pickable Box Prefab")]
    public static GameObject CreateOrUpdatePickableBoxPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        var boxGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxGO.name = "PickableBox";
        boxGO.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);

        // Rigidbody 설정
        var rb = boxGO.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = boxGO.AddComponent<Rigidbody>();
        }
        rb.mass = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // URP Material 확인 및 주황/노란색 머티리얼 적용
        var renderer = boxGO.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PickableBoxMat.mat");
            if (mat == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit == null)
                {
                    urpLit = Shader.Find("Standard");
                }

                mat = new Material(urpLit);
                mat.color = new Color(0.95f, 0.6f, 0.15f, 1f); // 오렌지색 상자

                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets", "Materials");
                }
                AssetDatabase.CreateAsset(mat, "Assets/Materials/PickableBoxMat.mat");
            }
            renderer.sharedMaterial = mat;
        }

        // Netcode NetworkObject & PickableItem 부착
        var netObj = boxGO.AddComponent<NetworkObject>();
        var pickable = boxGO.AddComponent<PickableItem>();

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(PickableItem).GetField("_promptText", flags)?.SetValue(pickable, "상자 들기");
        typeof(PickableItem).GetField("_dropVerticalOffset", flags)?.SetValue(pickable, 0.23f);

        // 프리팹으로 저장
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(boxGO, PICKABLE_BOX_PREFAB_PATH);
        Object.DestroyImmediate(boxGO);

        // DefaultNetworkPrefabs 등록
        RegisterNetworkPrefab(savedPrefab);

        Debug.Log($"[SetupInteractionSystem] PickableBox 프리팹 생성 완료: {PICKABLE_BOX_PREFAB_PATH}");
        return savedPrefab;
    }

    private static void RegisterNetworkPrefab(GameObject prefab)
    {
        var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (networkPrefabs != null)
        {
            if (!networkPrefabs.Contains(prefab))
            {
                networkPrefabs.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(networkPrefabs);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupInteractionSystem] DefaultNetworkPrefabs에 PickableBox 등록 완료.");
            }
        }
    }

    [MenuItem("Tools/Setup GameScene Interaction & Samples")]
    public static void SetupGameSceneMenu()
    {
        var pickablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PICKABLE_BOX_PREFAB_PATH);
        if (pickablePrefab == null)
        {
            pickablePrefab = CreateOrUpdatePickableBoxPrefab();
        }
        SetupGameScene(pickablePrefab);
    }

    public static void SetupGameScene(GameObject pickablePrefab)
    {
        bool needToCloseScene = false;
        var currentScene = SceneManager.GetActiveScene();
        if (currentScene.path != GAME_SCENE_PATH)
        {
            currentScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            needToCloseScene = true;
        }

        // 1. InteractionHUD 캔버스 생성/확인
        var existingHUD = GameObject.Find("InteractionHUD");
        if (existingHUD != null)
        {
            Undo.DestroyObjectImmediate(existingHUD);
        }

        var hudCanvasGO = new GameObject("InteractionHUD");
        Undo.RegisterCreatedObjectUndo(hudCanvasGO, "Create Interaction HUD");

        var canvas = hudCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = hudCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        hudCanvasGO.AddComponent<GraphicRaycaster>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Crosshair Dot
        var crosshairGO = new GameObject("CrosshairDot");
        crosshairGO.transform.SetParent(hudCanvasGO.transform, false);
        var crosshairImg = crosshairGO.AddComponent<Image>();
        crosshairImg.color = new Color(1f, 1f, 1f, 0.9f);
        var crosshairRect = crosshairGO.GetComponent<RectTransform>();
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.sizeDelta = new Vector2(6f, 6f);
        crosshairRect.anchoredPosition = Vector2.zero;

        // Prompt Panel
        var promptGO = new GameObject("PromptPanel");
        promptGO.transform.SetParent(hudCanvasGO.transform, false);
        var promptBg = promptGO.AddComponent<Image>();
        promptBg.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);
        var promptRect = promptGO.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = new Vector2(200f, 44f);
        promptRect.anchoredPosition = new Vector2(0f, -55f);

        // Key Box
        var keyBoxGO = new GameObject("KeyBox");
        keyBoxGO.transform.SetParent(promptGO.transform, false);
        var keyBoxImg = keyBoxGO.AddComponent<Image>();
        keyBoxImg.color = new Color(0.25f, 0.25f, 0.32f, 0.95f);
        var keyBoxRect = keyBoxGO.GetComponent<RectTransform>();
        keyBoxRect.anchorMin = new Vector2(0.06f, 0.15f);
        keyBoxRect.anchorMax = new Vector2(0.28f, 0.85f);
        keyBoxRect.offsetMin = Vector2.zero;
        keyBoxRect.offsetMax = Vector2.zero;

        var keyTextGO = new GameObject("KeyText");
        keyTextGO.transform.SetParent(keyBoxGO.transform, false);
        var keyText = keyTextGO.AddComponent<TextMeshProUGUI>();
        // keyText.font = font; /* TMP Font */
        keyText.text = "E";
        keyText.fontSize = 20;
        keyText.fontStyle = FontStyles.Bold;
        keyText.alignment = TextAlignmentOptions.Center;
        keyText.color = Color.white;
        var keyTextRect = keyTextGO.GetComponent<RectTransform>();
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.offsetMin = Vector2.zero;
        keyTextRect.offsetMax = Vector2.zero;

        // Action Text
        var actionTextGO = new GameObject("ActionText");
        actionTextGO.transform.SetParent(promptGO.transform, false);
        var actionText = actionTextGO.AddComponent<TextMeshProUGUI>();
        // actionText.font = font; /* TMP Font */
        actionText.text = "상호작용";
        actionText.fontSize = 18;
        actionText.fontStyle = FontStyles.Normal;
        actionText.alignment = TextAlignmentOptions.Left;
        actionText.color = Color.white;
        var actionTextRect = actionTextGO.GetComponent<RectTransform>();
        actionTextRect.anchorMin = new Vector2(0.34f, 0f);
        actionTextRect.anchorMax = new Vector2(0.95f, 1f);
        actionTextRect.offsetMin = Vector2.zero;
        actionTextRect.offsetMax = Vector2.zero;

        // Held Hint Panel
        var heldGO = new GameObject("HeldHintPanel");
        heldGO.transform.SetParent(hudCanvasGO.transform, false);
        var heldBg = heldGO.AddComponent<Image>();
        heldBg.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);
        var heldRect = heldGO.GetComponent<RectTransform>();
        heldRect.anchorMin = new Vector2(0.5f, 0.5f);
        heldRect.anchorMax = new Vector2(0.5f, 0.5f);
        heldRect.pivot = new Vector2(0.5f, 0.5f);
        heldRect.sizeDelta = new Vector2(240f, 36f);
        heldRect.anchoredPosition = new Vector2(0f, -105f);

        var heldTextGO = new GameObject("HeldText");
        heldTextGO.transform.SetParent(heldGO.transform, false);
        var heldText = heldTextGO.AddComponent<TextMeshProUGUI>();
        // heldText.font = font; /* TMP Font */
        heldText.text = "[좌클릭] 바닥에 내려놓기";
        heldText.fontSize = 16;
        heldText.fontStyle = FontStyles.Normal;
        heldText.alignment = TextAlignmentOptions.Center;
        heldText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var heldTextRect = heldTextGO.GetComponent<RectTransform>();
        heldTextRect.anchorMin = Vector2.zero;
        heldTextRect.anchorMax = Vector2.one;
        heldTextRect.offsetMin = Vector2.zero;
        heldTextRect.offsetMax = Vector2.zero;

        var uiComp = hudCanvasGO.AddComponent<InteractionUI>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(InteractionUI).GetField("_crosshairRoot", flags)?.SetValue(uiComp, crosshairGO);
        typeof(InteractionUI).GetField("_promptPanel", flags)?.SetValue(uiComp, promptGO);
        typeof(InteractionUI).GetField("_keyText", flags)?.SetValue(uiComp, keyText);
        typeof(InteractionUI).GetField("_promptText", flags)?.SetValue(uiComp, actionText);
        typeof(InteractionUI).GetField("_heldHintPanel", flags)?.SetValue(uiComp, heldGO);
        typeof(InteractionUI).GetField("_heldHintText", flags)?.SetValue(uiComp, heldText);

        promptGO.SetActive(false);
        heldGO.SetActive(false);

        // 2. 바닥 위에 테스트용 PickableBox 인스턴스 배치
        // 기존 샘플 박스 정리
        var oldBoxes = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (var b in oldBoxes)
        {
            if (b != null && b.name.StartsWith("SamplePickableBox"))
            {
                Undo.DestroyObjectImmediate(b);
            }
        }

        if (pickablePrefab != null)
        {
            Vector3[] samplePositions = new Vector3[]
            {
                new Vector3(0f, 0.25f, 2.2f),
                new Vector3(1.2f, 0.25f, 2.5f),
                new Vector3(-1.2f, 0.25f, 2.5f)
            };

            for (int i = 0; i < samplePositions.Length; i++)
            {
                GameObject boxInstance = (GameObject)PrefabUtility.InstantiatePrefab(pickablePrefab, currentScene);
                boxInstance.name = $"SamplePickableBox_{i + 1}";
                boxInstance.transform.position = samplePositions[i];
                boxInstance.transform.rotation = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f);
                Undo.RegisterCreatedObjectUndo(boxInstance, "Create Sample Pickable Box");
            }
        }

        // 3. GameSceneBootstrap 생성/확인 (에디터 단독 플레이 테스트 지원)
        var existingBootstrap = GameObject.Find("GameSceneBootstrap");
        if (existingBootstrap == null)
        {
            var bootstrapGO = new GameObject("GameSceneBootstrap");
            bootstrapGO.AddComponent<GameSceneBootstrap>();
            Undo.RegisterCreatedObjectUndo(bootstrapGO, "Create GameSceneBootstrap");
        }

        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        Debug.Log("[SetupInteractionSystem] GameScene에 InteractionHUD, GameSceneBootstrap 및 테스트용 SamplePickableBox 배치 완료.");
    }
}

