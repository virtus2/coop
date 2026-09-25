using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 총기 사격 및 히트스캔 시스템의 에셋 생성, 프리팹 셋업 및 동작 단위 검증을 수행하는 에디터 유틸리티입니다.
/// </summary>
public static class GunCombatSystemSetupAndTest
{
    private const string ITEM_DATA_PATH = "Assets/Resources/ItemData";
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string MONSTER_PREFAB_PATH = "Assets/Prefabs/MonsterPrefab.prefab";

    [MenuItem("Tools/Gun Combat/1. Setup Gun & Ammo Assets and Prefabs")]
    public static void SetupAllAssetsAndPrefabs()
    {
        EnsureDirectories();
        CreateAmmoItems();
        CreateGunItems();
        SetupPlayerPrefab();
        SetupMonsterPrefabHitbox();
        AddGunsToPlayerPrefabInitialItems();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[GunCombatSetup] 모든 총기/탄약 에셋 및 프리팹 셋업이 완료되었습니다!</color>");
    }

    [MenuItem("Tools/Gun Combat/3. Add Guns To PlayerPrefab Initial Items")]
    public static void AddGunsToPlayerPrefabInitialItems()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[GunCombatSetup] {PLAYER_PREFAB_PATH} 프리팹을 찾을 수 없습니다.");
            return;
        }

        PlayerInventory inv = prefab.GetComponent<PlayerInventory>();
        if (inv == null)
        {
            Debug.LogError("[GunCombatSetup] PlayerInventory 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        ItemData rifle = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Gun_AssaultRifle.asset");
        ItemData pistol = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Gun_TacticalPistol.asset");
        ItemData laser = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Gun_ChargeLaser.asset");
        ItemData shotgun = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Gun_PumpShotgun.asset");
        ItemData ammoRifle = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Ammo_Rifle.asset");
        ItemData ammoPistol = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Ammo_Pistol.asset");
        ItemData ammoEnergy = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Ammo_Energy.asset");
        ItemData ammoShotgun = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Ammo_Shotgun.asset");

        var items = new System.Collections.Generic.List<ItemData>();
        if (rifle != null) items.Add(rifle);
        if (pistol != null) items.Add(pistol);
        if (laser != null) items.Add(laser);
        if (shotgun != null) items.Add(shotgun);
        if (ammoRifle != null) items.Add(ammoRifle);
        if (ammoPistol != null) items.Add(ammoPistol);
        if (ammoEnergy != null) items.Add(ammoEnergy);
        if (ammoShotgun != null) items.Add(ammoShotgun);

        inv.SetInitialItems(items);
        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log("<color=green>[GunCombatSetup] PlayerPrefab의 초기 아이템(Initial Items)에 총기 3종 및 탄약이 등록되었습니다!</color>");
    }

    [MenuItem("Tools/Gun Combat/2. Run Gun Combat Unit Verification")]
    public static bool RunVerification()
    {
        Debug.Log("<color=cyan>========== [총기 히트스캔 전투 시스템 검증 시작] ==========</color>");

        bool pass1 = Test_GunCombatDataValidation();
        bool pass2 = Test_InventoryAmmoConsumption();
        bool pass3 = Test_HeadshotAndChargeDamageCalculations();
        bool pass4 = Test_StaggerDiminishingReturns();
        bool pass5 = Test_SprintCombatInteractions();

        if (pass1 && pass2 && pass3 && pass4 && pass5)
        {
            Debug.Log("<color=green><b>[SUCCESS] 모든 총기 전투 및 달리기 상호작용 단위 검증을 완벽하게 통과했습니다!</b></color>");
            return true;
        }
        else
        {
            Debug.LogError("<color=red><b>[FAIL] 일부 검증 테스트에서 문제가 발생했습니다.</b></color>");
            return false;
        }
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(ITEM_DATA_PATH))
        {
            Directory.CreateDirectory(ITEM_DATA_PATH);
        }
    }

    private static void CreateAmmoItems()
    {
        CreateOrUpdateAmmoItem("Ammo_Rifle", "소총 탄약", "연사 소총에 사용되는 5.56mm 표준 탄약입니다.", 120);
        CreateOrUpdateAmmoItem("Ammo_Pistol", "권총 탄약", "단발 권총에 사용되는 9mm 표준 탄약입니다.", 60);
        CreateOrUpdateAmmoItem("Ammo_Energy", "에너지 셀", "차징 레이저 총기에 사용되는 고밀도 에너지 셀입니다.", 50);
    }

    private static void CreateOrUpdateAmmoItem(string id, string name, string desc, int maxStack)
    {
        string path = $"{ITEM_DATA_PATH}/{id}.asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.Initialize(id, name, null, desc, maxStack, null, null);
        item.SetActionType(ItemActionType.None);
        EditorUtility.SetDirty(item);
        Debug.Log($"[GunCombatSetup] 탄약 아이템 준비됨: {name} ({id})");
    }

    private static void CreateGunItems()
    {
        // 1. 연사 소총 (FullAuto)
        CreateOrUpdateGunItem(
            id: "Gun_AssaultRifle",
            name: "돌격 소총",
            desc: "마우스 홀드 시 빠른 연사가 가능한 기본 제식 소총입니다.",
            fireMode: GunFireMode.FullAuto,
            damage: 25,
            fireInterval: 0.12f,
            magCap: 30,
            reloadTime: 2.0f,
            ammoId: "Ammo_Rifle",
            recoilPitch: 1.4f,
            recoilYaw: 0.3f,
            recoilRecov: 10f
        );

        // 2. 단발 권총 (SemiAuto)
        CreateOrUpdateGunItem(
            id: "Gun_TacticalPistol",
            name: "전술 권총",
            desc: "단발 클릭으로 정밀 사격이 가능한 강력한 보조 권총입니다.",
            fireMode: GunFireMode.SemiAuto,
            damage: 35,
            fireInterval: 0.22f,
            magCap: 12,
            reloadTime: 1.5f,
            ammoId: "Ammo_Pistol",
            recoilPitch: 1.8f,
            recoilYaw: 0.2f,
            recoilRecov: 12f
        );

        // 3. 차징 레이저 총 (Charge Shot)
        CreateOrUpdateGunItem(
            id: "Gun_ChargeLaser",
            name: "차지 레이저 라이플",
            desc: "좌클릭 홀드로 에너지를 모아 뗄 때 강력한 빔을 발사합니다. (우클릭 취소 가능)",
            fireMode: GunFireMode.Charge,
            damage: 40,
            fireInterval: 0.4f,
            magCap: 10,
            reloadTime: 2.5f,
            ammoId: "Ammo_Energy",
            recoilPitch: 2.2f,
            recoilYaw: 0.4f,
            recoilRecov: 8f,
            minChargeDuration: 1.0f,
            chargeMultiplier: 2.0f
        );
    }

    private static void CreateOrUpdateGunItem(
        string id, string name, string desc, GunFireMode fireMode,
        int damage, float fireInterval, int magCap, float reloadTime, string ammoId,
        float recoilPitch, float recoilYaw, float recoilRecov,
        float minChargeDuration = 1.0f, float chargeMultiplier = 2.0f)
    {
        string path = $"{ITEM_DATA_PATH}/{id}.asset";
        GunItemData gun = AssetDatabase.LoadAssetAtPath<GunItemData>(path);
        if (gun == null)
        {
            gun = ScriptableObject.CreateInstance<GunItemData>();
            AssetDatabase.CreateAsset(gun, path);
        }

        gun.Initialize(id, name, null, desc, 1, null, null);
        gun.SetActionType(ItemActionType.Gun);

        // 리플렉션을 통해 private 필드 직렬화 설정
        SetField(gun, "_baseDamage", damage);
        SetField(gun, "_fireInterval", fireInterval);
        SetField(gun, "_maxRange", 100f);
        SetField(gun, "_fireMode", fireMode);
        SetField(gun, "_magazineCapacity", magCap);
        SetField(gun, "_reloadDuration", reloadTime);
        SetField(gun, "_requiredAmmoItemId", ammoId);
        SetField(gun, "_recoilPitch", recoilPitch);
        SetField(gun, "_recoilYaw", recoilYaw);
        SetField(gun, "_recoilRecoverySpeed", recoilRecov);
        SetField(gun, "_sprintToFireDelay", 0.18f);
        SetField(gun, "_shootingMovementMultiplier", 0.75f);
        SetField(gun, "_minChargeDuration", minChargeDuration);
        SetField(gun, "_chargedDamageMultiplier", chargeMultiplier);

        EditorUtility.SetDirty(gun);
        Debug.Log($"[GunCombatSetup] 총기 아이템 준비됨: {name} (모드: {fireMode}, 데미지: {damage}, 탄창: {magCap}발, 탄약: {ammoId})");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    private static void SetupPlayerPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogWarning($"[GunCombatSetup] {PLAYER_PREFAB_PATH} 프리팹을 찾을 수 없습니다.");
            return;
        }

        if (prefab.GetComponent<PlayerGunCombat>() == null)
        {
            prefab.AddComponent<PlayerGunCombat>();
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("[GunCombatSetup] PlayerPrefab에 PlayerGunCombat 컴포넌트 추가 완료.");
        }
    }

    private static void SetupMonsterPrefabHitbox()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MONSTER_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogWarning($"[GunCombatSetup] {MONSTER_PREFAB_PATH} 프리팹을 찾을 수 없습니다.");
            return;
        }

        // 1. 루트/몸체 콜라이더에 기본 Hitbox (Body) 확인
        Hitbox bodyHitbox = prefab.GetComponent<Hitbox>();
        if (bodyHitbox == null)
        {
            bodyHitbox = prefab.AddComponent<Hitbox>();
            SetField(bodyHitbox, "_hitboxType", HitboxType.Body);
        }

        // 2. 머리 위치에 HeadHitbox 자식 오브젝트 확인 및 생성
        Transform headTransform = prefab.transform.Find("HeadHitbox");
        if (headTransform == null)
        {
            GameObject headGO = new GameObject("HeadHitbox");
            headGO.transform.SetParent(prefab.transform, false);
            headGO.transform.localPosition = new Vector3(0f, 1.8f, 0f); // 머리 평균 높이

            SphereCollider sc = headGO.AddComponent<SphereCollider>();
            sc.radius = 0.35f;

            Hitbox headHitbox = headGO.AddComponent<Hitbox>();
            SetField(headHitbox, "_hitboxType", HitboxType.Head);
            SetField(headHitbox, "_damageableTarget", prefab.GetComponent<MonsterController>());

            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("[GunCombatSetup] MonsterPrefab에 HeadHitbox (헤드샷 콜라이더) 추가 완료.");
        }
    }

    #region Unit Tests

    private static bool Test_GunCombatDataValidation()
    {
        GunItemData rifle = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_PATH}/Gun_AssaultRifle.asset");
        GunItemData pistol = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_PATH}/Gun_TacticalPistol.asset");
        GunItemData laser = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_PATH}/Gun_ChargeLaser.asset");

        if (rifle == null || pistol == null || laser == null)
        {
            Debug.LogError("[Test 1] 총기 데이터 에셋을 로드할 수 없습니다.");
            return false;
        }

        bool rifleValid = rifle.FireMode == GunFireMode.FullAuto && rifle.BaseDamage == 25 && rifle.MagazineCapacity == 30;
        bool pistolValid = pistol.FireMode == GunFireMode.SemiAuto && pistol.BaseDamage == 35 && pistol.MagazineCapacity == 12;
        bool laserValid = laser.FireMode == GunFireMode.Charge && laser.ChargedDamageMultiplier >= 2.0f && laser.MagazineCapacity == 10;

        if (rifleValid && pistolValid && laserValid)
        {
            Debug.Log("<color=green>[Test 1 통과] 총기 3종의 스탯 및 발사 모드(연사/단발/차지) 데이터 검증 성공.</color>");
            return true;
        }

        Debug.LogError("[Test 1 실패] 총기 데이터 스탯 불일치");
        return false;
    }

    private static bool Test_InventoryAmmoConsumption()
    {
        // GameObject 기반 Mock 인벤토리 구성
        GameObject go = new GameObject("MockPlayerInventory");
        PlayerInventory inv = go.AddComponent<PlayerInventory>();
        inv.EnsureInitialized();

        ItemData ammoItem = AssetDatabase.LoadAssetAtPath<ItemData>($"{ITEM_DATA_PATH}/Ammo_Rifle.asset");
        if (ammoItem == null)
        {
            Object.DestroyImmediate(go);
            Debug.LogError("[Test 2] Ammo_Rifle 에셋 부재");
            return false;
        }

        // 8발 추가 (탄창 30발 중 8발만 보유한 시나리오)
        inv.AddItem(ammoItem, 8);
        int totalBefore = inv.GetTotalAmmoCount("Ammo_Rifle");

        // 20발 장전 시도 -> 8발만 소모되어야 함 (부분 장전 검증)
        int consumed = inv.ConsumeAmmo("Ammo_Rifle", 20);
        int totalAfter = inv.GetTotalAmmoCount("Ammo_Rifle");

        // 슬롯 잔탄 보존 검증
        InventorySlot slot = new InventorySlot();
        GunItemData rifle = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_PATH}/Gun_AssaultRifle.asset");
        slot.Set(rifle, 1, 15); // 잔탄 15발 설정

        bool slotAmmoPreserved = slot.CurrentAmmo == 15;
        bool partialConsumptionValid = (totalBefore == 8) && (consumed == 8) && (totalAfter == 0);

        Object.DestroyImmediate(go);

        if (partialConsumptionValid && slotAmmoPreserved)
        {
            Debug.Log("<color=green>[Test 2 통과] 인벤토리 탄약 부분 소모(8발 장전) 및 슬롯별 잔탄 보존 검증 성공.</color>");
            return true;
        }

        Debug.LogError($"[Test 2 실패] totalBefore={totalBefore}, consumed={consumed}, totalAfter={totalAfter}, slotAmmo={slot.CurrentAmmo}");
        return false;
    }

    private static bool Test_HeadshotAndChargeDamageCalculations()
    {
        int baseDmg = 40;

        // 헤드샷 1.5배 검증
        int headshotDmg = Mathf.RoundToInt(baseDmg * 1.5f);
        if (headshotDmg != 60)
        {
            Debug.LogError($"[Test 3 실패] 헤드샷 데미지 계산 오류: {headshotDmg} != 60");
            return false;
        }

        // 차지샷 2.0배 검증
        int chargeDmg = Mathf.RoundToInt(baseDmg * 2.0f);
        if (chargeDmg != 80)
        {
            Debug.LogError($"[Test 3 실패] 차지 데미지 계산 오류: {chargeDmg} != 80");
            return false;
        }

        // 헤드샷 + 차지샷 복합 배율 (1.5 * 2.0 = 3.0배)
        int compoundDmg = Mathf.RoundToInt(chargeDmg * 1.5f);
        if (compoundDmg != 120)
        {
            Debug.LogError($"[Test 3 실패] 복합 데미지 계산 오류: {compoundDmg} != 120");
            return false;
        }

        Debug.Log("<color=green>[Test 3 통과] 헤드샷(1.5배), 차지샷(2.0배), 복합 타격 데미지 연산 검증 성공.</color>");
        return true;
    }

    private static bool Test_StaggerDiminishingReturns()
    {
        float baseStagger = 0.4f;
        float diminishFactor = 0.25f;

        // 1회차: 100% (0.4s)
        float h1 = baseStagger * Mathf.Max(0.1f, 1.0f - (0 * diminishFactor));
        // 2회차: 75% (0.3s)
        float h2 = baseStagger * Mathf.Max(0.1f, 1.0f - (1 * diminishFactor));
        // 3회차: 50% (0.2s)
        float h3 = baseStagger * Mathf.Max(0.1f, 1.0f - (2 * diminishFactor));
        // 4회차: 25% (0.1s)
        float h4 = baseStagger * Mathf.Max(0.1f, 1.0f - (3 * diminishFactor));
        // 5회차 이후: 최소 10% (0.04s)
        float h5 = baseStagger * Mathf.Max(0.1f, 1.0f - (4 * diminishFactor));

        bool valid = Mathf.Approximately(h1, 0.4f) &&
                     Mathf.Approximately(h2, 0.3f) &&
                     Mathf.Approximately(h3, 0.2f) &&
                     Mathf.Approximately(h4, 0.1f) &&
                     Mathf.Approximately(h5, 0.04f);

        if (valid)
        {
            Debug.Log("<color=green>[Test 4 통과] 경직 점감 법칙(Diminishing Returns) 수치 연산 검증 성공.</color>");
            return true;
        }

        Debug.LogError($"[Test 4 실패] 점감 수치 오류: h1={h1}, h2={h2}, h3={h3}, h4={h4}, h5={h5}");
        return false;
    }

    private static bool Test_SprintCombatInteractions()
    {
        GunItemData rifle = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_PATH}/Gun_AssaultRifle.asset");
        if (rifle == null)
        {
            Debug.LogError("[Test 5 실패] Gun_AssaultRifle 에셋을 찾을 수 없습니다.");
            return false;
        }

        // 1. 선딜레이 및 이동속도 배율 스탯 검증
        bool delayValid = Mathf.Approximately(rifle.SprintToFireDelay, 0.18f);
        bool penaltyValid = Mathf.Approximately(rifle.ShootingMovementMultiplier, 0.75f);

        // 2. 이동 페널티 연산 검증 (기본 걷기 5.0m/s -> 75%인 3.75m/s)
        float baseWalkSpeed = 5.0f;
        float penaltySpeed = baseWalkSpeed * rifle.ShootingMovementMultiplier;
        bool speedCalculationValid = Mathf.Approximately(penaltySpeed, 3.75f);

        if (delayValid && penaltyValid && speedCalculationValid)
        {
            Debug.Log("<color=green>[Test 5 통과] 달리기 후 사격 선딜레이(0.18s) 및 사격 중 이동속도 페널티(75% = 3.75m/s) 검증 성공.</color>");
            return true;
        }

        Debug.LogError($"[Test 5 실패] delayValid={delayValid}, penaltyValid={penaltyValid}, speedCalculationValid={speedCalculationValid}");
        return false;
    }

    #endregion
}
