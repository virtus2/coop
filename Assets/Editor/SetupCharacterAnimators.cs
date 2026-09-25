using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이어, 몬스터, NPC 프리팹에 Animator 및 NetworkAnimator 컴포넌트를 구성하고 설정하는 유틸리티입니다.
/// - 플레이어: Client-Authoritative (ClientNetworkAnimator)
/// - 몬스터 및 NPC: Server-Authoritative (NetworkAnimator)
/// </summary>
[InitializeOnLoad]
public static class SetupCharacterAnimators
{
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string DUMMY_PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerDummyPrefab.prefab";
    private const string MONSTER_PREFAB_PATH = "Assets/Prefabs/MonsterPrefab.prefab";
    private const string NPC_PREFAB_PATH = "Assets/Prefabs/NPCPrefab.prefab";

    static SetupCharacterAnimators()
    {
        EditorApplication.delayCall += AutoSetupOnCompile;
    }

    private static void AutoSetupOnCompile()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (playerPrefab != null && playerPrefab.GetComponent<ClientNetworkAnimator>() == null)
        {
            SetupAllCharacterAnimators();
        }
    }

    [MenuItem("Tools/Coop/Setup Animators On All Character Prefabs", false, 13)]
    public static void SetupAllCharacterAnimators()
    {
        // 1. Monster & NPC 프리팹 재생성 (CreateWorldCharacters에 Animator 및 NetworkAnimator 포함)
        CreateWorldCharacters.CreateAllPrefabs();

        // 2. PlayerPrefab 및 PlayerDummyPrefab 설정
        SetupPlayerAnimatorPrefab(PLAYER_PREFAB_PATH);
        SetupPlayerAnimatorPrefab(DUMMY_PLAYER_PREFAB_PATH);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[SetupCharacterAnimators]</color> 모든 캐릭터(Player, Monster, NPC)에 Animator 및 멀티플레이어 동기화 컴포넌트 설정이 완료되었습니다!");
    }

    private static void SetupPlayerAnimatorPrefab(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"[SetupCharacterAnimators] 플레이어 프리팹을 찾을 수 없습니다: {prefabPath}");
            return;
        }

        bool modified = false;

        // Animator 컴포넌트 확인 및 추가
        Animator animator = prefabRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = prefabRoot.AddComponent<Animator>();
            modified = true;
            Debug.Log("[SetupCharacterAnimators] PlayerPrefab에 Animator 컴포넌트를 추가했습니다.");
        }

        // ClientNetworkAnimator 컴포넌트 확인 및 추가
        ClientNetworkAnimator clientNetAnimator = prefabRoot.GetComponent<ClientNetworkAnimator>();
        if (clientNetAnimator == null)
        {
            clientNetAnimator = prefabRoot.AddComponent<ClientNetworkAnimator>();
            modified = true;
            Debug.Log("[SetupCharacterAnimators] PlayerPrefab에 ClientNetworkAnimator 컴포넌트를 추가했습니다.");
        }

        // Animator 프로퍼티 바인딩
        if (clientNetAnimator.Animator != animator)
        {
            clientNetAnimator.Animator = animator;
            modified = true;
            Debug.Log("[SetupCharacterAnimators] ClientNetworkAnimator.Animator를 바인딩했습니다.");
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PLAYER_PREFAB_PATH);
        }

        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }
}
