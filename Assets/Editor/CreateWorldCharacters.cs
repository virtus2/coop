using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 에디터에서 서버 권한(Server-Authoritative) 몬스터 및 NPC 프리팹과 오브젝트를 생성하는 유틸리티입니다.
/// URP 파이프라인과 완벽히 호환되는 기본 머티리얼 및 필수 네트워크 컴포넌트를 구성합니다.
/// </summary>
public static class CreateWorldCharacters
{
    private const string PREFABS_FOLDER = "Assets/Prefabs";
    private const string MONSTER_PREFAB_PATH = "Assets/Prefabs/MonsterPrefab.prefab";
    private const string NPC_PREFAB_PATH = "Assets/Prefabs/NPCPrefab.prefab";

    [MenuItem("Tools/Coop/Create Monster & NPC Prefabs", false, 10)]
    public static void CreateAllPrefabs()
    {
        EnsureFolderExists(PREFABS_FOLDER);

        GameObject monsterObj = BuildMonsterGameObject();
        PrefabUtility.SaveAsPrefabAssetAndConnect(monsterObj, MONSTER_PREFAB_PATH, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(monsterObj);

        GameObject npcObj = BuildNPCGameObject();
        PrefabUtility.SaveAsPrefabAssetAndConnect(npcObj, NPC_PREFAB_PATH, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(npcObj);

        GameObject monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MONSTER_PREFAB_PATH);
        GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NPC_PREFAB_PATH);

        RegisterNetworkPrefab(monsterPrefab);
        RegisterNetworkPrefab(npcPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateWorldCharacters] 프리팹 생성 및 NetworkPrefabs 등록 완료: \n1. {MONSTER_PREFAB_PATH}\n2. {NPC_PREFAB_PATH}");
    }

    private static void RegisterNetworkPrefab(GameObject prefab)
    {
        if (prefab == null) return;
        var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (networkPrefabs != null)
        {
            if (!networkPrefabs.Contains(prefab))
            {
                networkPrefabs.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(networkPrefabs);
                Debug.Log($"[CreateWorldCharacters] DefaultNetworkPrefabs에 {prefab.name} 등록 완료.");
            }
        }
    }

    [MenuItem("GameObject/Coop/Create Monster in Scene", false, 20)]
    public static void CreateMonsterInScene(MenuCommand menuCommand)
    {
        GameObject go = BuildMonsterGameObject();
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Monster");
        Selection.activeObject = go;
    }

    [MenuItem("GameObject/Coop/Create NPC in Scene", false, 21)]
    public static void CreateNPCInScene(MenuCommand menuCommand)
    {
        GameObject go = BuildNPCGameObject();
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create NPC");
        Selection.activeObject = go;
    }

    [MenuItem("Tools/Coop/Setup World Characters in GameScene", false, 11)]
    public static void SetupGameSceneCharacters()
    {
        CreateAllPrefabs();

        string scenePath = "Assets/Scenes/GameScene.unity";
        Scene activeScene = SceneManager.GetActiveScene();
        bool openedScene = false;

        if (activeScene.path != scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                openedScene = true;
            }
            else
            {
                return;
            }
        }

        GameObject spawnerGO = GameObject.Find("WorldCharacterSpawner");
        if (spawnerGO == null)
        {
            spawnerGO = new GameObject("WorldCharacterSpawner");
            Undo.RegisterCreatedObjectUndo(spawnerGO, "Create WorldCharacterSpawner");
        }

        var spawner = EnsureComponent<WorldCharacterSpawner>(spawnerGO);

        GameObject monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MONSTER_PREFAB_PATH);
        GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NPC_PREFAB_PATH);

        // SerializedObject를 사용하여 프리팹 목록 구성
        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty listProp = so.FindProperty("_characterSpawnList");
        listProp.ClearArray();

        // 몬스터 엔트리 추가 (스폰 위치 (6, 1, 6))
        listProp.InsertArrayElementAtIndex(0);
        SerializedProperty monsterEntry = listProp.GetArrayElementAtIndex(0);
        monsterEntry.FindPropertyRelative("EntryName").stringValue = "FieldMonster_1";
        monsterEntry.FindPropertyRelative("Prefab").objectReferenceValue = monsterPrefab;
        monsterEntry.FindPropertyRelative("DefaultPosition").vector3Value = new Vector3(6f, 1f, 6f);
        monsterEntry.FindPropertyRelative("DefaultRotation").quaternionValue = Quaternion.identity;

        // NPC 엔트리 추가 (스폰 위치 (-4, 1, 4))
        listProp.InsertArrayElementAtIndex(1);
        SerializedProperty npcEntry = listProp.GetArrayElementAtIndex(1);
        npcEntry.FindPropertyRelative("EntryName").stringValue = "VillageNPC_1";
        npcEntry.FindPropertyRelative("Prefab").objectReferenceValue = npcPrefab;
        npcEntry.FindPropertyRelative("DefaultPosition").vector3Value = new Vector3(-4f, 1f, 4f);
        npcEntry.FindPropertyRelative("DefaultRotation").quaternionValue = Quaternion.Euler(0f, 135f, 0f);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawnerGO);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("[CreateWorldCharacters] GameScene에 WorldCharacterSpawner 설정 완료 (Monster 1, NPC 1 등록됨)");
    }

    private static GameObject BuildMonsterGameObject()
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        root.name = "Monster";

        // URP Lit 셰이더 적용 및 붉은색 계열 컬러 설정
        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = CreateUrpMaterial(new Color(0.85f, 0.25f, 0.25f, 1f));
            renderer.sharedMaterial = mat;
        }

        // 필수 네트워크 컴포넌트 추가
        EnsureComponent<NetworkObject>(root);
        
        // [중요] 몬스터는 서버 권한(Server-Authoritative)이므로 ClientNetworkTransform이 아닌 Unity 기본 NetworkTransform 사용
        EnsureComponent<NetworkTransform>(root);

        // 물리 및 내비게이션
        NavMeshAgent agent = EnsureComponent<NavMeshAgent>(root);
        agent.height = 2f;
        agent.radius = 0.5f;
        agent.speed = 3.5f;

        // 몬스터 컨트롤러
        EnsureComponent<MonsterController>(root);

        // 애니메이터 및 서버 권한 네트워크 애니메이터 컴포넌트
        Animator animator = EnsureComponent<Animator>(root);
        NetworkAnimator netAnimator = EnsureComponent<NetworkAnimator>(root);
        netAnimator.Animator = animator;

        // 발걸음 사운드 컴포넌트
        EnsureComponent<CharacterFootsteps>(root);

        return root;
    }

    private static GameObject BuildNPCGameObject()
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        root.name = "NPC";

        // URP Lit 셰이더 적용 및 푸른색/녹색 계열 컬러 설정
        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = CreateUrpMaterial(new Color(0.25f, 0.65f, 0.85f, 1f));
            renderer.sharedMaterial = mat;
        }

        // 필수 네트워크 컴포넌트 추가
        EnsureComponent<NetworkObject>(root);

        // 서버 권한 NetworkTransform
        EnsureComponent<NetworkTransform>(root);

        // 내비게이션
        NavMeshAgent agent = EnsureComponent<NavMeshAgent>(root);
        agent.height = 2f;
        agent.radius = 0.5f;
        agent.speed = 2.5f;

        // NPC 컨트롤러 (IInteractable 구현)
        EnsureComponent<NPCController>(root);

        // 애니메이터 및 서버 권한 네트워크 애니메이터 컴포넌트
        Animator animator = EnsureComponent<Animator>(root);
        NetworkAnimator netAnimator = EnsureComponent<NetworkAnimator>(root);
        netAnimator.Animator = animator;

        // 발걸음 사운드 컴포넌트
        EnsureComponent<CharacterFootsteps>(root);

        return root;
    }

    private static Material CreateUrpMaterial(Color color)
    {
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            urpLitShader = Shader.Find("Standard");
        }

        Material mat = new Material(urpLitShader);
        mat.color = color;
        return mat;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
        {
            comp = go.AddComponent<T>();
        }
        return comp;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
