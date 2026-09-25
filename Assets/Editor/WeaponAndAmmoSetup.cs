using System;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GunItemData 기반 신규 무기 및 탄약 프리팹을 생성하고,
/// ItemData의 WorldPrefab 필드를 연결하며, DefaultNetworkPrefabs 및 GameScene에 배치하고,
/// 기존 레거시 테스트 데이터/프리팹을 정돈하는 통합 에디터 스크립트입니다.
/// </summary>
public static class WeaponAndAmmoSetup
{
    private const string ITEM_DATA_DIR = "Assets/Resources/ItemData";
    private const string PREFABS_DIR = "Assets/Prefabs";
    private const string WEAPON_MAT_DIR = "Assets/Materials/Weapons";
    private const string AMMO_MAT_DIR = "Assets/Materials/Ammo";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";
    private const string DEFAULT_NETWORK_PREFABS_PATH = "Assets/DefaultNetworkPrefabs.asset";

    [MenuItem("Tools/Gun Combat/Execute Weapon and Ammo Migration & Scene Setup")]
    public static void ExecuteAll()
    {
        EnsureDirectories();

        // 1. 머티리얼 준비
        var pistolMat = GetOrCreateMaterial($"{WEAPON_MAT_DIR}/PistolMat.mat", new Color(0.22f, 0.22f, 0.25f, 1f));
        var rifleMat = GetOrCreateMaterial($"{WEAPON_MAT_DIR}/AssaultRifleMat.mat", new Color(0.20f, 0.26f, 0.22f, 1f));
        var shotgunMat = GetOrCreateMaterial($"{WEAPON_MAT_DIR}/PumpShotgunMat.mat", new Color(0.16f, 0.16f, 0.18f, 1f));
        var laserMat = GetOrCreateMaterial($"{WEAPON_MAT_DIR}/ChargeLaserMat.mat", new Color(0.12f, 0.35f, 0.45f, 1f));

        var ammoPistolMat = GetOrCreateMaterial($"{AMMO_MAT_DIR}/AmmoPistolMat.mat", new Color(0.65f, 0.52f, 0.25f, 1f));
        var ammoRifleMat = GetOrCreateMaterial($"{AMMO_MAT_DIR}/AmmoRifleMat.mat", new Color(0.25f, 0.38f, 0.22f, 1f));
        var ammoShotgunMat = GetOrCreateMaterial($"{AMMO_MAT_DIR}/AmmoShotgunMat.mat", new Color(0.78f, 0.15f, 0.12f, 1f));
        var ammoEnergyMat = GetOrCreateMaterial($"{AMMO_MAT_DIR}/AmmoEnergyMat.mat", new Color(0.15f, 0.70f, 0.90f, 1f));

        // 2. ItemData 로드
        var pistolData = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_DIR}/Gun_TacticalPistol.asset");
        var rifleData = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_DIR}/Gun_AssaultRifle.asset");
        var shotgunData = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_DIR}/Gun_PumpShotgun.asset");
        var laserData = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_DIR}/Gun_ChargeLaser.asset");

        var ammoPistolData = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_DIR}/Ammo_Pistol.asset");
        var ammoRifleData = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_DIR}/Ammo_Rifle.asset");
        var ammoShotgunData = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_DIR}/Ammo_Shotgun.asset");
        var ammoEnergyData = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_DIR}/Ammo_Energy.asset");

        // 3. 프리팹 생성
        var pistolPrefab = CreateWorldPrefab("Gun_TacticalPistol", new Vector3(0.18f, 0.22f, 0.45f), pistolMat, pistolData, "전술 권총 들기", 1.5f, 0.15f);
        var riflePrefab = CreateWorldPrefab("Gun_AssaultRifle", new Vector3(0.15f, 0.22f, 0.95f), rifleMat, rifleData, "돌격 소총 들기", 2.5f, 0.15f);
        var shotgunPrefab = CreateWorldPrefab("Gun_PumpShotgun", new Vector3(0.18f, 0.22f, 1.05f), shotgunMat, shotgunData, "산탄총 들기", 2.8f, 0.15f);
        var laserPrefab = CreateWorldPrefab("Gun_ChargeLaser", new Vector3(0.20f, 0.25f, 0.85f), laserMat, laserData, "차지 레이저 건 들기", 2.2f, 0.15f);

        var ammoPistolPrefab = CreateWorldPrefab("Ammo_Pistol", new Vector3(0.25f, 0.18f, 0.25f), ammoPistolMat, ammoPistolData, "권총 탄약 상자 줍기", 1.0f, 0.10f);
        var ammoRiflePrefab = CreateWorldPrefab("Ammo_Rifle", new Vector3(0.30f, 0.20f, 0.35f), ammoRifleMat, ammoRifleData, "소총 탄약 상자 줍기", 1.2f, 0.10f);
        var ammoShotgunPrefab = CreateWorldPrefab("Ammo_Shotgun", new Vector3(0.28f, 0.22f, 0.28f), ammoShotgunMat, ammoShotgunData, "산탄총 탄약 상자 줍기", 1.2f, 0.10f);
        var ammoEnergyPrefab = CreateWorldPrefab("Ammo_Energy", new Vector3(0.22f, 0.25f, 0.22f), ammoEnergyMat, ammoEnergyData, "에너지 탄약 셀 줍기", 1.0f, 0.10f);

        // 4. ItemData 에셋에 WorldPrefab 및 HeldMaterial 연결
        LinkItemData(pistolData, pistolPrefab, pistolMat);
        LinkItemData(rifleData, riflePrefab, rifleMat);
        LinkItemData(shotgunData, shotgunPrefab, shotgunMat);
        LinkItemData(laserData, laserPrefab, laserMat);

        LinkItemData(ammoPistolData, ammoPistolPrefab, ammoPistolMat);
        LinkItemData(ammoRifleData, ammoRiflePrefab, ammoRifleMat);
        LinkItemData(ammoShotgunData, ammoShotgunPrefab, ammoShotgunMat);
        LinkItemData(ammoEnergyData, ammoEnergyPrefab, ammoEnergyMat);

        // 5. DefaultNetworkPrefabs 등록 및 레거시 제거
        GameObject[] newPrefabs = {
            pistolPrefab, riflePrefab, shotgunPrefab, laserPrefab,
            ammoPistolPrefab, ammoRiflePrefab, ammoShotgunPrefab, ammoEnergyPrefab
        };
        UpdateNetworkPrefabsList(newPrefabs);

        // 6. GameScene 배치 및 구형 오브젝트 정리
        SetupGameScene(newPrefabs);

        // 7. 구형 에셋 정리
        CleanupLegacyAssets();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>★ [WeaponAndAmmoSetup] 신규 무기 및 탄약 프리팹 생성, WorldPrefab 연결, 네트워크 등록, GameScene 배치 및 구형 에셋 정리 완료! ★</color>");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Weapons"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            AssetDatabase.CreateFolder("Assets/Materials", "Weapons");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Ammo"))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Ammo");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.color = color;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static GameObject CreateWorldPrefab(string name, Vector3 scale, Material mat, ItemData itemData, string prompt, float mass, float dropOffset)
    {
        string prefabPath = $"{PREFABS_DIR}/{name}.prefab";

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.localScale = scale;

        // 레이어 설정
        int pickableLayer = LayerMask.NameToLayer("PickableItem");
        if (pickableLayer >= 0) go.layer = pickableLayer;

        // 머티리얼 적용
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }

        // Rigidbody 설정
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Network 컴포넌트 추가
        var netObj = go.AddComponent<NetworkObject>();
        var netTransform = go.AddComponent<NetworkTransform>();
        var netRb = go.AddComponent<NetworkRigidbody>();

        // PickableItem 추가 및 필드 설정
        var pickable = go.AddComponent<PickableItem>();
        pickable.ItemData = itemData;

        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(PickableItem).GetField("_promptText", flags)?.SetValue(pickable, prompt);
        typeof(PickableItem).GetField("_dropVerticalOffset", flags)?.SetValue(pickable, dropOffset);
        typeof(PickableItem).GetField("_itemData", flags)?.SetValue(pickable, itemData);

        // 프리팹 저장
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);

        Debug.Log($"[WeaponAndAmmoSetup] 프리팹 생성/갱신: {prefabPath}");
        return savedPrefab;
    }

    private static void LinkItemData(ItemData itemData, GameObject worldPrefab, Material mat)
    {
        if (itemData == null) return;

        SerializedObject so = new SerializedObject(itemData);
        var worldPrefabProp = so.FindProperty("_worldPrefab");
        if (worldPrefabProp != null)
        {
            worldPrefabProp.objectReferenceValue = worldPrefab;
        }

        var heldMatProp = so.FindProperty("_heldMaterial");
        if (heldMatProp != null && heldMatProp.objectReferenceValue == null && mat != null)
        {
            heldMatProp.objectReferenceValue = mat;
        }

        var heldMeshProp = so.FindProperty("_heldMesh");
        if (heldMeshProp != null && heldMeshProp.objectReferenceValue == null)
        {
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(tempCube);
            heldMeshProp.objectReferenceValue = mesh;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(itemData);
    }

    private static void UpdateNetworkPrefabsList(GameObject[] prefabsToAdd)
    {
        var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DEFAULT_NETWORK_PREFABS_PATH);
        if (networkPrefabs == null) return;

        SerializedObject so = new SerializedObject(networkPrefabs);
        var listProp = so.FindProperty("List");
        if (listProp == null) return;

        // 1. 구형/null 프리팹 제거
        for (int i = listProp.arraySize - 1; i >= 0; i--)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            var prefabProp = element.FindPropertyRelative("Prefab");
            if (prefabProp != null)
            {
                var target = prefabProp.objectReferenceValue as GameObject;
                if (target == null ||
                    target.name.StartsWith("SampleGun") ||
                    target.name.StartsWith("SampleChargedWeapon") ||
                    target.name == "SampleShotgun")
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
            }
        }

        // 2. 신규 프리팹 추가 (중복 방지)
        foreach (var newPrefab in prefabsToAdd)
        {
            if (newPrefab == null) continue;
            bool alreadyExists = false;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                var prefabProp = element.FindPropertyRelative("Prefab");
                if (prefabProp != null && prefabProp.objectReferenceValue == newPrefab)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                int newIdx = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(newIdx);
                var newElement = listProp.GetArrayElementAtIndex(newIdx);
                var newPrefabProp = newElement.FindPropertyRelative("Prefab");
                if (newPrefabProp != null)
                {
                    newPrefabProp.objectReferenceValue = newPrefab;
                }
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(networkPrefabs);
        Debug.Log("[WeaponAndAmmoSetup] DefaultNetworkPrefabs 업데이트 완료");
    }

    private static void SetupGameScene(GameObject[] prefabs)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool openedScene = false;

        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            openedScene = true;
        }

        // 구형 인스턴스 제거
        string[] oldNames = { "SampleGun_Instance", "SampleChargedWeapon_Instance" };
        foreach (var oldName in oldNames)
        {
            var oldObj = GameObject.Find(oldName);
            if (oldObj != null)
            {
                Undo.DestroyObjectImmediate(oldObj);
                Debug.Log($"[WeaponAndAmmoSetup] GameScene에서 레거시 오브젝트 제거: {oldName}");
            }
        }

        // 총기 4종 및 탄약 4종 배치 좌표 매핑
        var placements = new (string name, string prefabName, Vector3 pos)[]
        {
            ("Gun_TacticalPistol_Instance", "Gun_TacticalPistol", new Vector3(1.5f, 0.5f, 2.0f)),
            ("Ammo_Pistol_Instance", "Ammo_Pistol", new Vector3(1.5f, 0.5f, 2.7f)),

            ("Gun_AssaultRifle_Instance", "Gun_AssaultRifle", new Vector3(0.5f, 0.5f, 2.0f)),
            ("Ammo_Rifle_Instance", "Ammo_Rifle", new Vector3(0.5f, 0.5f, 2.7f)),

            ("Gun_PumpShotgun_Instance", "Gun_PumpShotgun", new Vector3(-0.5f, 0.5f, 2.0f)),
            ("Ammo_Shotgun_Instance", "Ammo_Shotgun", new Vector3(-0.5f, 0.5f, 2.7f)),

            ("Gun_ChargeLaser_Instance", "Gun_ChargeLaser", new Vector3(-1.5f, 0.5f, 2.0f)),
            ("Ammo_Energy_Instance", "Ammo_Energy", new Vector3(-1.5f, 0.5f, 2.7f)),
        };

        foreach (var placement in placements)
        {
            var existing = GameObject.Find(placement.name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject targetPrefab = null;
            foreach (var p in prefabs)
            {
                if (p != null && p.name == placement.prefabName)
                {
                    targetPrefab = p;
                    break;
                }
            }

            if (targetPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab);
                instance.name = placement.name;
                instance.transform.position = placement.pos;
                instance.transform.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(instance, $"Create {placement.name}");
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        if (openedScene)
        {
            Debug.Log($"[WeaponAndAmmoSetup] {GAME_SCENE_PATH}에 총기 4종 및 탄약 4종 배치 완료.");
        }
    }

    private static void CleanupLegacyAssets()
    {
        string[] assetsToDelete = {
            "Assets/ItemData",
            "Assets/Resources/ItemData/SampleGunItemData.asset",
            "Assets/Resources/ItemData/SampleChargedWeaponItemData.asset",
            "Assets/Prefabs/SampleGun.prefab",
            "Assets/Prefabs/SampleChargedWeapon.prefab",
            "Assets/Prefabs/SampleShotgun.prefab",
            "Assets/Scripts/SampleGunItem.cs",
            "Assets/Scripts/SampleChargedWeaponItem.cs"
        };

        foreach (var path in assetsToDelete)
        {
            if (AssetDatabase.IsValidFolder(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[WeaponAndAmmoSetup] 레거시 에셋 삭제: {path}");
            }
        }
    }
}
