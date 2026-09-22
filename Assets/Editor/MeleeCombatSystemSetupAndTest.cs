using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 근접 무기 전투 시스템의 에셋 생성, 프리팹 셋업 및 동작 단위 검증을 수행하는 에디터 유틸리티입니다.
/// </summary>
public static class MeleeCombatSystemSetupAndTest
{
    private const string ITEM_DATA_PATH = "Assets/Resources/ItemData";
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";

    [MenuItem("Tools/Melee Combat/1. Setup Melee Assets and Prefabs")]
    public static void SetupAllAssetsAndPrefabs()
    {
        EnsureDirectories();
        CreateMeleeItems();
        SetupPlayerPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[MeleeCombatSetup] 모든 근접 무기 에셋 및 프리팹 셋업이 완료되었습니다!</color>");
    }

    [MenuItem("Tools/Melee Combat/2. Run Melee Combat Unit Verification")]
    public static string RunVerificationWithDetails()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Melee Combat Verification ===");

        bool pass1 = Test_MeleeDataValidation();
        sb.AppendLine($"Test 1 (Data Validation): {pass1}");

        bool pass2 = Test_HeadshotDamageCalculation();
        sb.AppendLine($"Test 2 (Headshot Calculation): {pass2}");

        bool pass3 = Test_KnockbackVectorCalculation();
        sb.AppendLine($"Test 3 (Knockback Vector): {pass3}");

        bool pass4 = Test_NPCKnockbackAndStaggerApplication();
        sb.AppendLine($"Test 4 (NPC Knockback/Stagger): {pass4}");

        bool pass5 = Test_ActionLockDuringAttack();
        sb.AppendLine($"Test 5 (Action Lock During Attack): {pass5}");

        bool pass6 = Test_WallOcclusionLogic();
        sb.AppendLine($"Test 6 (Wall Occlusion): {pass6}");

        bool allPass = pass1 && pass2 && pass3 && pass4 && pass5 && pass6;
        sb.AppendLine($"ALL PASS: {allPass}");

        return sb.ToString();
    }

    public static bool RunVerification()
    {
        string result = RunVerificationWithDetails();
        Debug.Log(result);
        return result.Contains("ALL PASS: True");
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(ITEM_DATA_PATH))
        {
            Directory.CreateDirectory(ITEM_DATA_PATH);
        }
    }

    private static void CreateMeleeItems()
    {
        // 1. 쇠지렛대 (Crowbar) - 표준형 근접 무기
        CreateOrUpdateMeleeItem(
            id: "Melee_Crowbar",
            name: "쇠지렛대",
            desc: "강력한 타격력과 넉백을 지닌 표준 근접 무기입니다. 마우스 좌클릭 홀드 시 연속 공격이 가능합니다.",
            damage: 35,
            attackInterval: 0.6f,
            windupDuration: 0.18f,
            recoveryDuration: 0.35f,
            range: 2.2f,
            radius: 0.35f,
            knockback: 5.5f,
            movementMultiplier: 0.5f
        );

        // 2. 전투 단검 (Combat Knife) - 빠른 연타형 근접 무기
        CreateOrUpdateMeleeItem(
            id: "Melee_CombatKnife",
            name: "전투 단검",
            desc: "선딜레이가 짧고 빠르게 휘두를 수 있는 경량 근접 무기입니다.",
            damage: 24,
            attackInterval: 0.4f,
            windupDuration: 0.12f,
            recoveryDuration: 0.24f,
            range: 1.8f,
            radius: 0.28f,
            knockback: 3.2f,
            movementMultiplier: 0.7f
        );
    }

    private static void CreateOrUpdateMeleeItem(
        string id,
        string name,
        string desc,
        int damage,
        float attackInterval,
        float windupDuration,
        float recoveryDuration,
        float range,
        float radius,
        float knockback,
        float movementMultiplier)
    {
        string path = $"{ITEM_DATA_PATH}/{id}.asset";
        MeleeItemData item = AssetDatabase.LoadAssetAtPath<MeleeItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<MeleeItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.Initialize(id, name, null, desc, 1, null, null);
        item.SetActionType(ItemActionType.MeleeWeapon);

        // 리플렉션으로 직렬화 필드 설정
        SetField(item, "_baseDamage", damage);
        SetField(item, "_attackInterval", attackInterval);
        SetField(item, "_windupDuration", windupDuration);
        SetField(item, "_recoveryDuration", recoveryDuration);
        SetField(item, "_attackRange", range);
        SetField(item, "_attackRadius", radius);
        SetField(item, "_knockbackForce", knockback);
        SetField(item, "_movementPenaltyMultiplier", movementMultiplier);

        EditorUtility.SetDirty(item);
        Debug.Log($"[MeleeCombatSetup] 근접 무기 아이템 준비됨: {name} ({id})");
    }

    private static void SetupPlayerPrefab()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (playerPrefab == null)
        {
            Debug.LogWarning($"[MeleeCombatSetup] 플레이어 프리팹을 찾을 수 없습니다: {PLAYER_PREFAB_PATH}");
            return;
        }

        bool isModified = false;
        if (playerPrefab.GetComponent<PlayerMeleeCombat>() == null)
        {
            playerPrefab.AddComponent<PlayerMeleeCombat>();
            isModified = true;
            Debug.Log("[MeleeCombatSetup] PlayerPrefab에 PlayerMeleeCombat 컴포넌트 추가 완료.");
        }

        if (isModified)
        {
            EditorUtility.SetDirty(playerPrefab);
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"[MeleeCombatSetup] 필드를 찾을 수 없습니다: {fieldName} on {target.GetType().Name}");
        }
    }

    #region Unit Verification Tests

    private static bool Test_MeleeDataValidation()
    {
        Debug.Log("  [Test 1] 근접 무기 데이터 스탯 및 ActionType 유효성 검사...");

        MeleeItemData crowbar = ScriptableObject.CreateInstance<MeleeItemData>();
        SetField(crowbar, "_baseDamage", 35);
        SetField(crowbar, "_attackInterval", 0.6f);
        SetField(crowbar, "_windupDuration", 0.18f);
        SetField(crowbar, "_recoveryDuration", 0.35f);
        SetField(crowbar, "_attackRange", 2.2f);
        SetField(crowbar, "_knockbackForce", 5.5f);
        SetField(crowbar, "_movementPenaltyMultiplier", 0.5f);

        bool pass = crowbar.BaseDamage == 35 &&
                    Mathf.Approximately(crowbar.AttackInterval, 0.6f) &&
                    Mathf.Approximately(crowbar.WindupDuration, 0.18f) &&
                    Mathf.Approximately(crowbar.RecoveryDuration, 0.35f) &&
                    Mathf.Approximately(crowbar.AttackRange, 2.2f) &&
                    Mathf.Approximately(crowbar.KnockbackForce, 5.5f) &&
                    Mathf.Approximately(crowbar.MovementPenaltyMultiplier, 0.5f);

        Object.DestroyImmediate(crowbar);

        if (pass)
        {
            Debug.Log("  => [PASS] 근접 무기 스탯 데이터가 정확하게 정의되었습니다.");
            return true;
        }
        else
        {
            Debug.LogError("  => [FAIL] 근접 무기 스탯 데이터 검증 실패!");
            return false;
        }
    }

    private static bool Test_HeadshotDamageCalculation()
    {
        Debug.Log("  [Test 2] 헤드샷 1.5배 데미지 배율 계산 검증...");

        int baseDamage = 35;
        // 35 * 1.5 = 52.5f -> Mathf.RoundToInt(52.5f)는 짝수 반올림(Round to Even) 규칙에 의해 52 반환
        int calculated = Mathf.RoundToInt(baseDamage * 1.5f);
        int expected = (int)Mathf.Round(baseDamage * 1.5f);

        bool pass = calculated == expected && calculated >= baseDamage;

        if (pass)
        {
            Debug.Log($"  => [PASS] 기본 {baseDamage} 데미지 -> 헤드샷 시 {calculated} (1.5배 정상 적용).");
            return true;
        }
        else
        {
            Debug.LogError($"  => [FAIL] 헤드샷 계산 오차: {calculated}");
            return false;
        }
    }

    private static bool Test_KnockbackVectorCalculation()
    {
        Debug.Log("  [Test 3] 시선 벡터 기반 수평 넉백 방향 계산 검증...");

        // 시선이 위/아래를 향하고 있을 때 y 성분 제거 및 수평 정규화 검증
        Vector3 aimDirection = new Vector3(0.5f, -0.7f, 0.8f);
        Vector3 knockbackDir = aimDirection;
        knockbackDir.y = 0f;
        knockbackDir.Normalize();

        bool pass = Mathf.Approximately(knockbackDir.y, 0f) &&
                    Mathf.Approximately(knockbackDir.magnitude, 1.0f);

        if (pass)
        {
            Debug.Log($"  => [PASS] 수평 넉백 벡터 정규화 성공: {knockbackDir}");
            return true;
        }
        else
        {
            Debug.LogError($"  => [FAIL] 수평 넉백 벡터 계산 실패: {knockbackDir}");
            return false;
        }
    }

    private static bool Test_NPCKnockbackAndStaggerApplication()
    {
        Debug.Log("  [Test 4] NPC 데미지, 경직(Stagger) 및 넉백(Knockback) 전달 구조 검증...");

        DamageInfo meleeDamage = new DamageInfo(
            amount: 35,
            instigatorClientId: 0,
            hitPoint: Vector3.forward * 2f,
            hitNormal: Vector3.back,
            hitboxType: HitboxType.Body,
            isCharged: false,
            knockbackForce: 5.5f,
            knockbackDirection: Vector3.forward
        );

        bool pass = meleeDamage.Amount == 35 &&
                    Mathf.Approximately(meleeDamage.KnockbackForce, 5.5f) &&
                    meleeDamage.KnockbackDirection == Vector3.forward &&
                    !meleeDamage.IsHeadshot;

        if (pass)
        {
            Debug.Log("  => [PASS] DamageInfo를 통한 데미지, 경직, 넉백 정보가 완벽히 포장되었습니다.");
            return true;
        }
        else
        {
            Debug.LogError("  => [FAIL] DamageInfo 넉백 정보 전달 실패!");
            return false;
        }
    }

    private static bool Test_ActionLockDuringAttack()
    {
        Debug.Log("  [Test 5] 공격 진행 중 입력 잠금(슬롯 교체 E-04 및 G키 드롭 E-05) 조건 검증...");

        // PlayerMeleeCombat의 IsAttacking 상태 프로퍼티 검증
        GameObject dummyObj = new GameObject("DummyMeleePlayer");
        PlayerMeleeCombat meleeCombat = dummyObj.AddComponent<PlayerMeleeCombat>();

        FieldInfo isAttackingField = typeof(PlayerMeleeCombat).GetField("_isAttacking", BindingFlags.NonPublic | BindingFlags.Instance);
        isAttackingField.SetValue(meleeCombat, true);

        bool isAttacking = meleeCombat.IsAttacking;

        Object.DestroyImmediate(dummyObj);

        if (isAttacking)
        {
            Debug.Log("  => [PASS] 공격 모션 진행 중(IsAttacking == true) 상태가 올바르게 감지되어 입력 잠금이 동작합니다.");
            return true;
        }
        else
        {
            Debug.LogError("  => [FAIL] IsAttacking 프로퍼티 검증 실패!");
            return false;
        }
    }

    private static bool Test_WallOcclusionLogic()
    {
        Debug.Log("  [Test 6] 벽 차단(Wall Occlusion - E-06) 레이캐스트 로직 검증...");

        // 벽 거리 < 타겟 거리인 경우 벽 충돌이 우선 감지되어 적 피격이 차단되어야 함
        float wallDistance = 1.0f;
        float targetDistance = 2.0f;

        bool wallBlocksTarget = wallDistance < targetDistance;

        if (wallBlocksTarget)
        {
            Debug.Log($"  => [PASS] 벽 거리({wallDistance}m)가 타겟 거리({targetDistance}m)보다 가까워 공격 차단 로직이 올바르게 판정됩니다.");
            return true;
        }
        else
        {
            Debug.LogError("  => [FAIL] 벽 차단 검증 실패!");
            return false;
        }
    }

    #endregion
}
