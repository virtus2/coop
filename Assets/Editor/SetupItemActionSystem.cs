using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameScene에 발사 및 홀드 사용 테스트용 샘플 아이템 프리팹을 생성하고 배치하는 에디터 도구입니다.
/// </summary>
public static class SetupItemActionSystem
{
    private const string GUN_PREFAB_PATH = "Assets/Prefabs/SampleGun.prefab";
    private const string MEDKIT_PREFAB_PATH = "Assets/Prefabs/SampleMedkit.prefab";
    private const string CHARGED_PREFAB_PATH = "Assets/Prefabs/SampleChargedWeapon.prefab";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Setup Sample Items in GameScene")]
    public static void ExecuteCompleteSetup()
    {
        EnsureMaterialsFolder();

        var gunPrefab = CreateOrUpdateGunPrefab();
        var medkitPrefab = CreateOrUpdateMedkitPrefab();
        var chargedPrefab = CreateOrUpdateChargedWeaponPrefab();

        SetupSampleItemsInScene(gunPrefab, medkitPrefab, chargedPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[SetupItemActionSystem]</color> 발사(Gun), 홀드 사용(Medkit), 복합(ChargedWeapon) 샘플 아이템 프리팹 및 GameScene 배치 완료!");
    }

    private static void EnsureMaterialsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                urpLit = Shader.Find("Standard");
            }
            mat = new Material(urpLit);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    public static GameObject CreateOrUpdateGunPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "SampleGun";
        go.transform.localScale = new Vector3(0.2f, 0.2f, 0.45f);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var mat = GetOrCreateMaterial("Assets/Materials/SampleGunMat.mat", new Color(0.9f, 0.25f, 0.15f, 1f));
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.AddComponent<NetworkObject>();
        var pickable = go.AddComponent<PickableItem>();
        go.AddComponent<SampleGunItem>();

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(PickableItem).GetField("_promptText", flags)?.SetValue(pickable, "샘플 권총 들기");
        typeof(PickableItem).GetField("_dropVerticalOffset", flags)?.SetValue(pickable, 0.12f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, GUN_PREFAB_PATH);
        Object.DestroyImmediate(go);

        RegisterNetworkPrefab(prefab);
        return prefab;
    }

    public static GameObject CreateOrUpdateMedkitPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "SampleMedkit";
        go.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 2f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var mat = GetOrCreateMaterial("Assets/Materials/SampleMedkitMat.mat", new Color(0.2f, 0.8f, 0.3f, 1f));
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.AddComponent<NetworkObject>();
        var pickable = go.AddComponent<PickableItem>();
        go.AddComponent<SampleMedkitItem>();

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(PickableItem).GetField("_promptText", flags)?.SetValue(pickable, "구급키트 들기");
        typeof(PickableItem).GetField("_dropVerticalOffset", flags)?.SetValue(pickable, 0.15f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, MEDKIT_PREFAB_PATH);
        Object.DestroyImmediate(go);

        RegisterNetworkPrefab(prefab);
        return prefab;
    }

    public static GameObject CreateOrUpdateChargedWeaponPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "SampleChargedWeapon";
        go.transform.localScale = new Vector3(0.25f, 0.25f, 0.6f);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var mat = GetOrCreateMaterial("Assets/Materials/SampleChargedWeaponMat.mat", new Color(0.7f, 0.2f, 0.85f, 1f));
        go.GetComponent<Renderer>().sharedMaterial = mat;

        go.AddComponent<NetworkObject>();
        var pickable = go.AddComponent<PickableItem>();
        go.AddComponent<SampleChargedWeaponItem>();

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(PickableItem).GetField("_promptText", flags)?.SetValue(pickable, "차지 라이플 들기");
        typeof(PickableItem).GetField("_dropVerticalOffset", flags)?.SetValue(pickable, 0.15f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, CHARGED_PREFAB_PATH);
        Object.DestroyImmediate(go);

        RegisterNetworkPrefab(prefab);
        return prefab;
    }

    private static void SetupSampleItemsInScene(GameObject gunPrefab, GameObject medkitPrefab, GameObject chargedPrefab)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool openedScene = false;

        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            openedScene = true;
        }

        // 기존 샘플 오브젝트 확인 또는 생성
        EnsureSceneItem("SampleGun_Instance", gunPrefab, new Vector3(1.2f, 0.5f, 2.0f));
        EnsureSceneItem("SampleMedkit_Instance", medkitPrefab, new Vector3(0.0f, 0.5f, 2.5f));
        EnsureSceneItem("SampleChargedWeapon_Instance", chargedPrefab, new Vector3(-1.2f, 0.5f, 2.0f));

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        if (openedScene)
        {
            Debug.Log($"[SetupItemActionSystem] {GAME_SCENE_PATH}에 샘플 아이템 배치 완료.");
        }
    }

    private static void EnsureSceneItem(string name, GameObject prefab, Vector3 position)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(instance, $"Create {name}");
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
