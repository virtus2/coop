using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameScene에 그리드 매니저와 테스트용 PlaceableObject 프리팹들을 생성하고 배치하는 에디터 스크립트입니다.
/// </summary>
public static class SetupGridTest
{
    private const string PREFAB_DIR = "Assets/Prefabs/Grid";
    private const string CUBE_PREFAB_PATH = "Assets/Prefabs/Grid/Building_Cube_1x1.prefab";
    private const string WALL_PREFAB_PATH = "Assets/Prefabs/Grid/Building_Wall_2x1.prefab";
    private const string PLATFORM_PREFAB_PATH = "Assets/Prefabs/Grid/Building_Platform_2x2.prefab";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Setup Grid Test in GameScene")]
    public static void ExecuteSetup()
    {
        EnsureDirectories();

        // 1. 테스트용 머티리얼 생성
        Material cubeMat = GetOrCreateMaterial("Assets/Materials/Grid_CubeMat.mat", new Color(0.2f, 0.6f, 1f, 1f));
        Material wallMat = GetOrCreateMaterial("Assets/Materials/Grid_WallMat.mat", new Color(0.9f, 0.6f, 0.2f, 1f));
        Material platMat = GetOrCreateMaterial("Assets/Materials/Grid_PlatformMat.mat", new Color(0.4f, 0.8f, 0.4f, 1f));

        // 2. 프리팹 3종 생성
        PlaceableObject cubePrefab = CreateCubePrefab(cubeMat);
        PlaceableObject wallPrefab = CreateWallPrefab(wallMat);
        PlaceableObject platformPrefab = CreatePlatformPrefab(platMat);

        // 3. GameScene에 GridSystem 배치 및 컨트롤러 연결
        SetupSceneGrid(cubePrefab, wallPrefab, platformPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[SetupGridTest]</color> GameScene 그리드 시스템 및 테스트용 PlaceableObject 3종(큐브 1x1, 벽 2x1, 발판 2x2) 설정 완료!");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Grid");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                urpLitShader = Shader.Find("Standard");
            }

            mat = new Material(urpLitShader);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static PlaceableObject CreateCubePrefab(Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Building_Cube_1x1";
        go.transform.position = Vector3.zero;

        // 콜라이더 및 렌더러 설정
        BoxCollider col = go.GetComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.5f, 0f);
        col.size = Vector3.one;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        // 메시 피벗을 바닥에 맞춤 (자식 오브젝트로 시각 메시 분리)
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform);
        visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        visual.transform.localScale = Vector3.one;
        visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

        // 부모 오브젝트의 메시 컴포넌트 제거
        Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(go.GetComponent<MeshFilter>());

        PlaceableObject po = go.AddComponent<PlaceableObject>();
        SerializedObject so = new SerializedObject(po);
        so.FindProperty("_gridSize").vector2IntValue = new Vector2Int(1, 1);
        so.FindProperty("_displayName").stringValue = "큐브 (1x1)";
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, CUBE_PREFAB_PATH);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<PlaceableObject>();
    }

    private static PlaceableObject CreateWallPrefab(Material mat)
    {
        GameObject go = new GameObject("Building_Wall_2x1");
        go.transform.position = Vector3.zero;

        BoxCollider col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1f, 0f);
        col.size = new Vector3(2f, 2f, 0.3f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(2f, 2f, 0.3f);
        visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

        PlaceableObject po = go.AddComponent<PlaceableObject>();
        SerializedObject so = new SerializedObject(po);
        so.FindProperty("_gridSize").vector2IntValue = new Vector2Int(2, 1);
        so.FindProperty("_displayName").stringValue = "벽 (2x1)";
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, WALL_PREFAB_PATH);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<PlaceableObject>();
    }

    private static PlaceableObject CreatePlatformPrefab(Material mat)
    {
        GameObject go = new GameObject("Building_Platform_2x2");
        go.transform.position = Vector3.zero;

        BoxCollider col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.1f, 0f);
        col.size = new Vector3(2f, 0.2f, 2f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform);
        visual.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        visual.transform.localScale = new Vector3(2f, 0.2f, 2f);
        visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

        PlaceableObject po = go.AddComponent<PlaceableObject>();
        SerializedObject so = new SerializedObject(po);
        so.FindProperty("_gridSize").vector2IntValue = new Vector2Int(2, 2);
        so.FindProperty("_displayName").stringValue = "발판 (2x2)";
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PLATFORM_PREFAB_PATH);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<PlaceableObject>();
    }

    private static void SetupSceneGrid(PlaceableObject cube, PlaceableObject wall, PlaceableObject platform)
    {
        Scene currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != GAME_SCENE_PATH)
        {
            currentScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
        }

        GameObject gridSystemGo = GameObject.Find("GridSystem");
        if (gridSystemGo == null)
        {
            gridSystemGo = new GameObject("GridSystem");
            Undo.RegisterCreatedObjectUndo(gridSystemGo, "Create GridSystem");
        }

        // 1. WorldGridManager 설정
        WorldGridManager gridManager = gridSystemGo.GetComponent<WorldGridManager>();
        if (gridManager == null)
        {
            gridManager = gridSystemGo.AddComponent<WorldGridManager>();
        }

        SerializedObject managerSo = new SerializedObject(gridManager);
        managerSo.FindProperty("_gridWidth").intValue = 30;
        managerSo.FindProperty("_gridLength").intValue = 30;
        managerSo.FindProperty("_cellSize").floatValue = 1.0f;
        managerSo.FindProperty("_gridOriginOffset").vector3Value = new Vector3(-15f, 0f, -15f);
        managerSo.FindProperty("_showGridDebug").boolValue = true;
        managerSo.FindProperty("_showWhenSelectedOnly").boolValue = false;
        managerSo.FindProperty("_showOccupiedFill").boolValue = true;
        managerSo.ApplyModifiedProperties();

        // 2. GridBuildingController 설정
        GridBuildingController buildingCtrl = gridSystemGo.GetComponent<GridBuildingController>();
        if (buildingCtrl == null)
        {
            buildingCtrl = gridSystemGo.AddComponent<GridBuildingController>();
        }

        SerializedObject ctrlSo = new SerializedObject(buildingCtrl);
        SerializedProperty prefabsProp = ctrlSo.FindProperty("_placeablePrefabs");
        prefabsProp.ClearArray();
        prefabsProp.InsertArrayElementAtIndex(0);
        prefabsProp.GetArrayElementAtIndex(0).objectReferenceValue = cube;
        prefabsProp.InsertArrayElementAtIndex(1);
        prefabsProp.GetArrayElementAtIndex(1).objectReferenceValue = wall;
        prefabsProp.InsertArrayElementAtIndex(2);
        prefabsProp.GetArrayElementAtIndex(2).objectReferenceValue = platform;
        ctrlSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
    }
}
