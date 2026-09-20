using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 유저 피드백을 반영하여 아이템 시스템을 리팩토링하는 에디터 도구:
/// 1. ItemData에 단일 소켓용 HeldMesh, HeldMaterial, 오프셋 및 ActionType 설정
/// 2. 월드 프리팹(SampleGun 등)에서 불필요한 액션 스크립트 제거 및 PickableItem.ItemData 연결
/// 3. 불필요한 Assets/Prefabs/Held 폴더 및 프리팹 5종 삭제
/// 4. GameScene에 배치된 샘플 상자들의 PickableItem.ItemData 연결
/// </summary>
public static class RefactorItemSystem
{
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Execute Item System Refactoring")]
    public static void ExecuteRefactoring()
    {
        Debug.Log("<color=cyan>==== [아이템 시스템 리팩토링 시작] ====</color>");

        // 1. 큐브 메쉬 획득
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tempCube);

        // 2. 머티리얼 로드
        Material gunMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SampleGunMat.mat");
        Material medkitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SampleMedkitMat.mat");
        Material chargedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SampleChargedWeaponMat.mat");
        Material boxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PickableBoxMat.mat");

        // 3. 월드 프리팹 로드
        GameObject gunWorldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SampleGun.prefab");
        GameObject medkitWorldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SampleMedkit.prefab");
        GameObject chargedWorldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SampleChargedWeapon.prefab");
        GameObject boxWorldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PickableBox.prefab");

        // 4. ItemData 에셋 갱신 (Resources/ItemData 및 ItemData 둘 다)
        string[] searchFolders = new string[] { "Assets/Resources/ItemData", "Assets/ItemData" };

        ItemData mainGunData = null;
        ItemData mainMedkitData = null;
        ItemData mainChargedData = null;
        ItemData mainBoxData = null;

        foreach (var folder in searchFolders)
        {
            if (!Directory.Exists(folder)) continue;

            var gunData = UpdateItemData(
                $"{folder}/SampleGunItemData.asset",
                cubeMesh, gunMat,
                new Vector3(0.05f, -0.05f, 0.2f), Vector3.zero, new Vector3(0.18f, 0.22f, 0.55f),
                ItemActionType.Gun, gunWorldPrefab
            );
            if (folder.Contains("Resources")) mainGunData = gunData;

            var medkitData = UpdateItemData(
                $"{folder}/SampleMedkitItemData.asset",
                cubeMesh, medkitMat,
                new Vector3(0.05f, -0.05f, 0.15f), Vector3.zero, new Vector3(0.35f, 0.25f, 0.18f),
                ItemActionType.Medkit, medkitWorldPrefab
            );
            if (folder.Contains("Resources")) mainMedkitData = medkitData;

            var chargedData = UpdateItemData(
                $"{folder}/SampleChargedWeaponItemData.asset",
                cubeMesh, chargedMat,
                new Vector3(0.05f, -0.05f, 0.3f), Vector3.zero, new Vector3(0.22f, 0.25f, 0.75f),
                ItemActionType.ChargedWeapon, chargedWorldPrefab
            );
            if (folder.Contains("Resources")) mainChargedData = chargedData;

            var boxData = UpdateItemData(
                $"{folder}/PickableBoxItemData.asset",
                cubeMesh, boxMat,
                new Vector3(0f, 0f, 0.2f), Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f),
                ItemActionType.None, boxWorldPrefab
            );
            if (folder.Contains("Resources")) mainBoxData = boxData;
        }

        // 5. 월드 프리팹 정리 (액션 스크립트 제거 & PickableItem.ItemData 연결)
        CleanWorldPrefab("Assets/Prefabs/SampleGun.prefab", mainGunData, typeof(SampleGunItem));
        CleanWorldPrefab("Assets/Prefabs/SampleMedkit.prefab", mainMedkitData, typeof(SampleMedkitItem));
        CleanWorldPrefab("Assets/Prefabs/SampleChargedWeapon.prefab", mainChargedData, typeof(SampleChargedWeaponItem));
        CleanWorldPrefab("Assets/Prefabs/PickableBox.prefab", mainBoxData, null);

        // 6. Assets/Prefabs/Held 디렉토리 및 프리팹 삭제
        if (AssetDatabase.IsValidFolder("Assets/Prefabs/Held"))
        {
            AssetDatabase.DeleteAsset("Assets/Prefabs/Held");
            Debug.Log("<color=green>[RefactorItemSystem]</color> 불필요한 Assets/Prefabs/Held 디렉토리 삭제 완료.");
        }

        // 7. GameScene에 배치된 샘플 상자들의 PickableItem.ItemData 연결
        SetupGameScenePickables(mainBoxData);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>==== [아이템 시스템 리팩토링 완료!] ====</color>");
    }

    private static ItemData UpdateItemData(
        string assetPath,
        Mesh mesh, Material mat,
        Vector3 pos, Vector3 rot, Vector3 scale,
        ItemActionType actionType, GameObject worldPrefab)
    {
        var data = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
        if (data == null) return null;

        SerializedObject so = new SerializedObject(data);
        so.FindProperty("_heldMesh").objectReferenceValue = mesh;
        so.FindProperty("_heldMaterial").objectReferenceValue = mat;
        so.FindProperty("_heldLocalPosition").vector3Value = pos;
        so.FindProperty("_heldLocalRotation").vector3Value = rot;
        so.FindProperty("_heldLocalScale").vector3Value = scale;
        so.FindProperty("_actionType").enumValueIndex = (int)actionType;
        so.FindProperty("_worldPrefab").objectReferenceValue = worldPrefab;
        so.FindProperty("_holdPrefab").objectReferenceValue = null; // 손 전용 프리팹 참조 제거

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void CleanWorldPrefab(string prefabPath, ItemData itemData, System.Type actionComponentToRemove)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null) return;

        try
        {
            // 불필요한 액션 스크립트 제거
            if (actionComponentToRemove != null)
            {
                var comp = prefabRoot.GetComponent(actionComponentToRemove);
                if (comp != null)
                {
                    Object.DestroyImmediate(comp, true);
                    Debug.Log($"[RefactorItemSystem] {prefabPath}에서 {actionComponentToRemove.Name} 제거 완료.");
                }
            }

            // PickableItem에 ItemData 연결
            var pickable = prefabRoot.GetComponent<PickableItem>();
            if (pickable == null)
            {
                pickable = prefabRoot.AddComponent<PickableItem>();
                Debug.Log($"[RefactorItemSystem] {prefabPath}에 순수 PickableItem 컴포넌트 추가 완료.");
            }

            if (pickable != null && itemData != null)
            {
                pickable.ItemData = itemData;
                EditorUtility.SetDirty(pickable);
                Debug.Log($"[RefactorItemSystem] {prefabPath}에 ItemData({itemData.ItemName}) 연결 완료.");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void SetupGameScenePickables(ItemData boxData)
    {
        if (boxData == null) return;

        Scene activeScene = SceneManager.GetActiveScene();
        bool opened = false;
        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            opened = true;
        }

        var allPickables = Object.FindObjectsByType<PickableItem>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var p in allPickables)
        {
            if (p != null)
            {
                if (p.ItemData == null)
                {
                    p.ItemData = boxData;
                    EditorUtility.SetDirty(p);
                    count++;
                }
            }
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[RefactorItemSystem] GameScene 내 {count}개의 PickableItem에 ItemData 연결 완료.");
        }
    }

    [MenuItem("Tools/Verify Item System Refactoring")]
    public static bool VerifyRefactoring()
    {
        Debug.Log("<color=cyan>==== [아이템 시스템 리팩토링 사후 검증 시작] ====</color>");
        bool allPass = true;

        void Assert(bool condition, string message)
        {
            if (condition)
            {
                Debug.Log($"<color=green>[PASS]</color> {message}");
            }
            else
            {
                Debug.LogError($"<color=red>[FAIL]</color> {message}");
                allPass = false;
            }
        }

        // 1. Held 디렉토리 삭제 확인
        Assert(!AssetDatabase.IsValidFolder("Assets/Prefabs/Held"), "Assets/Prefabs/Held 폴더 완전 삭제 확인");

        // 2. ItemData 확인
        string[] itemDataNames = { "SampleGunItemData", "SampleMedkitItemData", "SampleChargedWeaponItemData", "PickableBoxItemData" };
        foreach (var name in itemDataNames)
        {
            var data = Resources.Load<ItemData>($"ItemData/{name}");
            Assert(data != null, $"Resources ItemData 로드 확인: {name}");
            if (data != null)
            {
                Assert(data.HeldMesh != null, $"{name}: HeldMesh 할당 확인");
                Assert(data.HeldMaterial != null, $"{name}: HeldMaterial 할당 확인");
                Assert(data.WorldPrefab != null, $"{name}: WorldPrefab 할당 확인");
                Assert(data.HoldPrefab == null, $"{name}: HoldPrefab null 확인 (소켓 방식 대체)");
            }
        }

        // 3. 월드 프리팹 확인
        string[] prefabs = { "Assets/Prefabs/SampleGun.prefab", "Assets/Prefabs/SampleMedkit.prefab", "Assets/Prefabs/SampleChargedWeapon.prefab", "Assets/Prefabs/PickableBox.prefab" };
        foreach (var path in prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert(prefab != null, $"월드 프리팹 로드 확인: {path}");
            if (prefab != null)
            {
                var pickable = prefab.GetComponent<PickableItem>();
                Assert(pickable != null, $"{path}: PickableItem 컴포넌트 부착 확인");
                Assert(pickable != null && pickable.ItemData != null, $"{path}: PickableItem.ItemData 연결 확인");
                Assert(prefab.GetComponent<SampleGunItem>() == null, $"{path}: SampleGunItem 컴포넌트 없음 확인");
                Assert(prefab.GetComponent<SampleMedkitItem>() == null, $"{path}: SampleMedkitItem 컴포넌트 없음 확인");
                Assert(prefab.GetComponent<SampleChargedWeaponItem>() == null, $"{path}: SampleChargedWeaponItem 컴포넌트 없음 확인");
            }
        }

        // 4. GameScene 인스턴스 확인
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
        }
        var scenePickables = Object.FindObjectsByType<PickableItem>(FindObjectsSortMode.None);
        int unlinkedCount = 0;
        foreach (var sp in scenePickables)
        {
            if (sp.ItemData == null) unlinkedCount++;
        }
        Assert(unlinkedCount == 0, $"GameScene 내 ItemData 미할당 PickableItem 0개 확인 (미할당: {unlinkedCount}개)");

        if (allPass)
        {
            Debug.Log("<color=green>==== [리팩토링 사후 검증 모든 테스트 통과!] ====</color>");
        }
        else
        {
            Debug.LogError("<color=red>==== [리팩토링 사후 검증 일부 실패] ====</color>");
        }

        return allPass;
    }
}
