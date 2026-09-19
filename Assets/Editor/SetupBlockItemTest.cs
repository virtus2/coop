using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 마인크래프트 스타일 블록 아이템 시스템과 바닥 쿼드 + 그리드 셰이더(WorldGridShader) 및 프리뷰 고스트를
/// GameScene에 자동 구성하는 에디터 도구입니다.
/// </summary>
public static class SetupBlockItemTest
{
    private const string PREFAB_DIR = "Assets/Prefabs";
    private const string MATERIAL_DIR = "Assets/Materials";
    private const string BLOCK_ITEM_PREFAB_PATH = "Assets/Prefabs/Block_Cube_Item.prefab";
    private const string CUBE_BUILDING_PATH = "Assets/Prefabs/Grid/Building_Cube_1x1.prefab";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    private const string MAT_VALID_PATH = "Assets/Materials/Grid_Preview_Valid.mat";
    private const string MAT_INVALID_PATH = "Assets/Materials/Grid_Preview_Invalid.mat";
    private const string MAT_SHADER_GRID_PATH = "Assets/Materials/Grid_ShaderMat.mat";
    private const string MAT_OUTLINE_PATH = "Assets/Materials/Grid_OutlineMat.mat";

    [MenuItem("Tools/Setup Block Item System in GameScene")]
    public static void ExecuteCompleteSetup()
    {
        EnsureDirectories();

        // 1. URP 투명 프리뷰 머티리얼 및 그리드 셰이더 머티리얼 생성
        Material validMat = GetOrCreateTransparentMaterial(MAT_VALID_PATH, new Color(0f, 1f, 0.4f, 0.45f));
        Material invalidMat = GetOrCreateTransparentMaterial(MAT_INVALID_PATH, new Color(1f, 0.15f, 0.15f, 0.45f));
        Material gridShaderMat = GetOrCreateGridShaderMaterial(MAT_SHADER_GRID_PATH);
        Material outlineMat = GetOrCreateUnlitMaterial(MAT_OUTLINE_PATH, new Color(0.2f, 1f, 0.5f, 0.9f));

        // 2. 설치될 건물 프리팹 로드 확인
        PlaceableObject cubeBuilding = AssetDatabase.LoadAssetAtPath<PlaceableObject>(CUBE_BUILDING_PATH);
        if (cubeBuilding == null)
        {
            SetupGridTest.ExecuteSetup();
            cubeBuilding = AssetDatabase.LoadAssetAtPath<PlaceableObject>(CUBE_BUILDING_PATH);
        }

        // 3. 손에 들 수 있는 블록 아이템 프리팹 생성 (PickableItem + PlaceableItem)
        GameObject blockItemPrefab = CreateOrUpdateBlockItemPrefab(cubeBuilding);

        // 4. GameScene에 GridSystem 갱신 및 블록 아이템 스폰 (바닥 쿼드 + 그리드 셰이더 방식)
        SetupSceneGridAndItems(validMat, invalidMat, gridShaderMat, outlineMat, blockItemPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[SetupBlockItemTest]</color> 블록 아이템 및 바닥 쿼드 + 그리드 셰이더 시스템 구성 완료!");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(MATERIAL_DIR))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
    }

    private static Material GetOrCreateGridShaderMaterial(string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader gridShader = Shader.Find("Custom/WorldGridShader");

        if (gridShader == null)
        {
            gridShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (mat == null)
        {
            mat = new Material(gridShader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = gridShader;
        }

        mat.SetColor("_LineColor", new Color(0.8f, 0.92f, 1.0f, 0.6f));
        mat.SetColor("_FillColor", new Color(0.2f, 0.6f, 1.0f, 0.02f));
        mat.SetFloat("_LineWidth", 0.035f);
        mat.SetFloat("_FadeRadius", 16.0f);
        mat.SetFloat("_FadeFalloff", 4.0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateTransparentMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            urpLit = Shader.Find("Standard");
        }

        if (mat == null)
        {
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = urpLit;
        }

        // URP Transparent 설정
        mat.SetFloat("_Surface", 1); // 1 = Transparent
        mat.SetFloat("_Blend", 0);   // 0 = Alpha blend
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateUnlitMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlit == null)
        {
            urpUnlit = Shader.Find("Unlit/Color");
        }

        if (mat == null)
        {
            mat = new Material(urpUnlit);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = urpUnlit;
        }

        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject CreateOrUpdateBlockItemPrefab(PlaceableObject buildingPrefab)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Block_Cube_Item";
        go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        // 머티리얼 적용
        Material itemMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Grid_CubeMat.mat");
        if (itemMat != null)
        {
            go.GetComponent<Renderer>().sharedMaterial = itemMat;
        }

        // Rigidbody
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // NetworkObject
        go.AddComponent<NetworkObject>();

        // PickableItem
        PickableItem pickable = go.AddComponent<PickableItem>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(PickableItem).GetField("_promptText", flags)?.SetValue(pickable, "큐브 블록 들기");
        typeof(PickableItem).GetField("_dropVerticalOffset", flags)?.SetValue(pickable, 0.18f);

        // PlaceableItem
        PlaceableItem placeable = go.AddComponent<PlaceableItem>();
        typeof(PlaceableItem).GetField("_buildingPrefab", flags)?.SetValue(placeable, buildingPrefab);
        typeof(PlaceableItem).GetField("_amount", flags)?.SetValue(placeable, 5); // 5개 넉넉하게 지급
        typeof(PlaceableItem).GetField("_consumeOnPlace", flags)?.SetValue(placeable, true);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BLOCK_ITEM_PREFAB_PATH);
        Object.DestroyImmediate(go);

        RegisterNetworkPrefab(prefab);
        return prefab;
    }

    private static void SetupSceneGridAndItems(Material validMat, Material invalidMat, Material gridShaderMat, Material outlineMat, GameObject blockItemPrefab)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool openedScene = false;

        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            openedScene = true;
        }

        // 1. GridSystem 오브젝트 탐색 및 컴포넌트 세팅
        GameObject gridSystemGo = GameObject.Find("GridSystem");
        if (gridSystemGo == null)
        {
            gridSystemGo = new GameObject("GridSystem");
            Undo.RegisterCreatedObjectUndo(gridSystemGo, "Create GridSystem");
        }

        // WorldGridManager
        WorldGridManager gridManager = gridSystemGo.GetComponent<WorldGridManager>();
        if (gridManager == null)
        {
            gridManager = gridSystemGo.AddComponent<WorldGridManager>();
        }

        // 기존 LineRendererGridVisualizer가 있다면 제거 (바닥 쿼드 + 그리드 셰이더로 완전 전환)
        LineRendererGridVisualizer oldLineVisualizer = gridSystemGo.GetComponent<LineRendererGridVisualizer>();
        if (oldLineVisualizer != null)
        {
            Object.DestroyImmediate(oldLineVisualizer);
        }

        // ShaderGridVisualizer 설정
        ShaderGridVisualizer visualizer = gridSystemGo.GetComponent<ShaderGridVisualizer>();
        if (visualizer == null)
        {
            visualizer = gridSystemGo.AddComponent<ShaderGridVisualizer>();
        }
        var visSo = new SerializedObject(visualizer);
        visSo.FindProperty("_gridMaterial").objectReferenceValue = gridShaderMat;
        visSo.FindProperty("_lineWidth").floatValue = 0.035f;
        visSo.FindProperty("_fadeRadius").floatValue = 16.0f;
        visSo.FindProperty("_fadeFalloff").floatValue = 4.0f;
        visSo.FindProperty("_lineColor").colorValue = new Color(0.8f, 0.92f, 1f, 0.6f);
        visSo.ApplyModifiedProperties();

        // GridPlacementPreview
        GridPlacementPreview preview = gridSystemGo.GetComponent<GridPlacementPreview>();
        if (preview == null)
        {
            preview = gridSystemGo.AddComponent<GridPlacementPreview>();
        }
        var prevSo = new SerializedObject(preview);
        prevSo.FindProperty("_validMaterial").objectReferenceValue = validMat;
        prevSo.FindProperty("_invalidMaterial").objectReferenceValue = invalidMat;
        prevSo.FindProperty("_outlineMaterial").objectReferenceValue = outlineMat;
        prevSo.ApplyModifiedProperties();

        // GridBuildingController
        GridBuildingController buildingCtrl = gridSystemGo.GetComponent<GridBuildingController>();
        if (buildingCtrl == null)
        {
            buildingCtrl = gridSystemGo.AddComponent<GridBuildingController>();
        }
        var ctrlSo = new SerializedObject(buildingCtrl);
        ctrlSo.FindProperty("_gridVisualizerComponent").objectReferenceValue = visualizer;
        ctrlSo.FindProperty("_placementPreview").objectReferenceValue = preview;
        ctrlSo.ApplyModifiedProperties();

        // 2. 씬에 주워서 테스트해 볼 수 있는 샘플 블록 아이템 2개 배치
        EnsureSceneItem("BlockItem_Instance_1", blockItemPrefab, new Vector3(0.5f, 0.3f, 3.5f));
        EnsureSceneItem("BlockItem_Instance_2", blockItemPrefab, new Vector3(-0.5f, 0.3f, 3.5f));

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        if (openedScene)
        {
            Debug.Log($"[SetupBlockItemTest] {GAME_SCENE_PATH}에 바닥 쿼드 + 그리드 셰이더 및 블록 아이템 배치 완료.");
        }
    }

    private static void EnsureSceneItem(string name, GameObject prefab, Vector3 position)
    {
        var existing = GameObject.Find(name);
        if (existing == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, $"Create {name}");
        }
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
            }
        }
    }
}
