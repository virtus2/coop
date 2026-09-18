using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이어, 몬스터, NPC 프리팹에 CharacterFootsteps 컴포넌트를 안전하게 추가 및 설정하는 에디터 유틸리티입니다.
/// </summary>
[InitializeOnLoad]
public static class SetupFootstepSystem
{
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string MONSTER_PREFAB_PATH = "Assets/Prefabs/MonsterPrefab.prefab";
    private const string NPC_PREFAB_PATH = "Assets/Prefabs/NPCPrefab.prefab";

    static SetupFootstepSystem()
    {
        EditorApplication.delayCall += AutoSetupOnCompile;
    }

    private static void AutoSetupOnCompile()
    {
        // 씬이나 프리팹에 컴포넌트가 아직 누락되어 있는 경우에만 1회 자동 적용
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (playerPrefab != null && playerPrefab.GetComponent<CharacterFootsteps>() == null)
        {
            SetupAllCharacterPrefabs();
        }
    }

    [MenuItem("Tools/Coop/Setup Footsteps On All Character Prefabs", false, 12)]
    public static void SetupAllCharacterPrefabs()
    {
        int updatedCount = 0;

        // 1. Monster & NPC 프리팹 재생성 (CreateWorldCharacters에 CharacterFootsteps 포함됨)
        CreateWorldCharacters.CreateAllPrefabs();

        // 2. PlayerPrefab 확인 및 CharacterFootsteps 추가
        updatedCount += EnsureFootstepsOnPrefab(PLAYER_PREFAB_PATH, isPlayer: true);
        updatedCount += EnsureFootstepsOnPrefab(MONSTER_PREFAB_PATH, isPlayer: false);
        updatedCount += EnsureFootstepsOnPrefab(NPC_PREFAB_PATH, isPlayer: false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SetupFootstepSystem] 모든 캐릭터 프리팹에 CharacterFootsteps 컴포넌트 설정 완료! (총 {updatedCount}개 갱신)");
    }

    private static int EnsureFootstepsOnPrefab(string prefabPath, bool isPlayer)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"[SetupFootstepSystem] 프리팹을 로드할 수 없습니다: {prefabPath}");
            return 0;
        }

        bool modified = false;
        var footsteps = prefabRoot.GetComponent<CharacterFootsteps>();
        if (footsteps == null)
        {
            footsteps = prefabRoot.AddComponent<CharacterFootsteps>();
            modified = true;
            Debug.Log($"[SetupFootstepSystem] '{prefabRoot.name}' 프리팹에 CharacterFootsteps 컴포넌트를 추가했습니다.");
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }

        PrefabUtility.UnloadPrefabContents(prefabRoot);
        return modified ? 1 : 0;
    }
}
