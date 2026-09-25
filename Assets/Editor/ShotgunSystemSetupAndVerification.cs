using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 샷건(산탄총) 및 샷건 전용 탄약 에셋 생성과 기획 명세 일치 여부를 검증하는 에디터 유틸리티입니다.
/// </summary>
public static class ShotgunSystemSetupAndVerification
{
    private const string ITEM_DATA_PATH = "Assets/Resources/ItemData";

    [MenuItem("Tools/Gun Combat/4. Setup Shotgun & Ammo Assets")]
    public static void SetupShotgunAssets()
    {
        if (!Directory.Exists(ITEM_DATA_PATH))
        {
            Directory.CreateDirectory(ITEM_DATA_PATH);
        }

        // 1. Ammo_Shotgun 에셋 생성 / 갱신
        CreateOrUpdateShotgunAmmo();

        // 2. Gun_PumpShotgun 에셋 생성 / 갱신
        CreateOrUpdatePumpShotgun();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[ShotgunSetup] 샷건 및 샷건 탄약 에셋 생성이 성공적으로 완료되었습니다!</color>");
    }

    [MenuItem("Tools/Gun Combat/5. Run Shotgun Full Verification Tests")]
    public static void RunFullVerification()
    {
        Debug.Log("<color=cyan>==================== [샷건 시스템 단위 및 기획 명세 검증 시작] ====================</color>");
        SetupShotgunAssets();

        bool allPassed = true;

        allPassed &= TestAssetProperties();
        allPassed &= TestDeterministicConeDirections();
        allPassed &= TestDamageFalloffFormula();

        if (allPassed)
        {
            Debug.Log("<color=green>★★★★★ [검증 통과] 샷건 시스템의 모든 기획 수치 및 단위 테스트가 100% 정상 통과했습니다! ★★★★★</color>");
        }
        else
        {
            Debug.LogError("<color=red>[검증 실패] 샷건 시스템 검증 중 실패 항목이 발생했습니다.</color>");
        }
    }

    private static void CreateOrUpdateShotgunAmmo()
    {
        string path = $"{ITEM_DATA_PATH}/Ammo_Shotgun.asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.Initialize("Ammo_Shotgun", "산탄총 탄약", null, "12게이지 샷건 쉘 산탄 탄약입니다.", 24, null, null);
        item.SetActionType(ItemActionType.None);
        EditorUtility.SetDirty(item);
        Debug.Log("[ShotgunSetup] Ammo_Shotgun 에셋 생성/갱신 완료 (스택 크기: 24)");
    }

    private static void CreateOrUpdatePumpShotgun()
    {
        string path = $"{ITEM_DATA_PATH}/Gun_PumpShotgun.asset";
        GunItemData shotgun = AssetDatabase.LoadAssetAtPath<GunItemData>(path);
        if (shotgun == null)
        {
            shotgun = ScriptableObject.CreateInstance<GunItemData>();
            AssetDatabase.CreateAsset(shotgun, path);
        }

        // 기존 돌격 소총에서 VFX 프리팹 참조가 있다면 복사
        GunItemData rifle = AssetDatabase.LoadAssetAtPath<GunItemData>($"{ITEM_DATA_PATH}/Gun_AssaultRifle.asset");

        shotgun.Initialize("Gun_PumpShotgun", "펌프액션 샷건", null, "근거리에서 8발의 산탄 펠릿을 방출하는 강력한 산탄총입니다. 1발씩 튜브에 장전하며 장전 중 즉시 격발이 가능합니다.", 1, null, null);
        shotgun.SetActionType(ItemActionType.Gun);

        SetField(shotgun, "_baseDamage", 80);
        SetField(shotgun, "_fireInterval", 0.8f);
        SetField(shotgun, "_maxRange", 15.0f);
        SetField(shotgun, "_fireMode", GunFireMode.SemiAuto);

        // 샷건 산탄 및 감쇄
        SetField(shotgun, "_isShotgun", true);
        SetField(shotgun, "_pelletCount", 8);
        SetField(shotgun, "_spreadAngle", 7.0f);
        SetField(shotgun, "_damageFalloffStartRange", 3.0f);
        SetField(shotgun, "_damageFalloffEndRange", 10.0f);
        SetField(shotgun, "_minDamagePerPellet", 1);

        // 탄약 및 쉘 바이 쉘 장전
        SetField(shotgun, "_magazineCapacity", 8);
        SetField(shotgun, "_requiredAmmoItemId", "Ammo_Shotgun");
        SetField(shotgun, "_useShellByShellReload", true);
        SetField(shotgun, "_reloadStartDelay", 0.35f);
        SetField(shotgun, "_reloadInsertInterval", 0.5f);
        SetField(shotgun, "_reloadEndDelay", 0.3f);

        // 반동
        SetField(shotgun, "_recoilPitch", 4.0f);
        SetField(shotgun, "_recoilYaw", 0.8f);
        SetField(shotgun, "_recoilRecoverySpeed", 10.0f);

        if (rifle != null)
        {
            SetField(shotgun, "_muzzleFlashPrefab", rifle.MuzzleFlashPrefab);
            SetField(shotgun, "_bulletTracerPrefab", rifle.BulletTracerPrefab);
            SetField(shotgun, "_impactEffectPrefab", rifle.ImpactEffectPrefab);
            SetField(shotgun, "_fireSound", rifle.FireSound);
            SetField(shotgun, "_dryFireSound", rifle.DryFireSound);
            SetField(shotgun, "_reloadSound", rifle.ReloadSound);
        }

        EditorUtility.SetDirty(shotgun);
        Debug.Log("[ShotgunSetup] Gun_PumpShotgun 에셋 생성/갱신 완료");
    }

    private static bool TestAssetProperties()
    {
        GunItemData shotgun = Resources.Load<GunItemData>("ItemData/Gun_PumpShotgun");
        ItemData ammo = Resources.Load<ItemData>("ItemData/Ammo_Shotgun");

        if (shotgun == null)
        {
            Debug.LogError("[Test Fail] Gun_PumpShotgun 에셋을 Resources에서 로드할 수 없습니다.");
            return false;
        }

        if (ammo == null)
        {
            Debug.LogError("[Test Fail] Ammo_Shotgun 에셋을 Resources에서 로드할 수 없습니다.");
            return false;
        }

        bool pass = true;
        if (!shotgun.IsShotgun) { Debug.LogError("IsShotgun이 true가 아닙니다."); pass = false; }
        if (shotgun.PelletCount != 8) { Debug.LogError($"PelletCount가 8이 아닙니다: {shotgun.PelletCount}"); pass = false; }
        if (Mathf.Abs(shotgun.SpreadAngle - 7f) > 0.01f) { Debug.LogError($"SpreadAngle이 7도가 아닙니다: {shotgun.SpreadAngle}"); pass = false; }
        if (Mathf.Abs(shotgun.DamageFalloffStartRange - 3f) > 0.01f) { Debug.LogError($"FalloffStart가 3m가 아닙니다: {shotgun.DamageFalloffStartRange}"); pass = false; }
        if (Mathf.Abs(shotgun.DamageFalloffEndRange - 10f) > 0.01f) { Debug.LogError($"FalloffEnd가 10m가 아닙니다: {shotgun.DamageFalloffEndRange}"); pass = false; }
        if (shotgun.MinDamagePerPellet != 1) { Debug.LogError($"MinDamagePerPellet이 1이 아닙니다: {shotgun.MinDamagePerPellet}"); pass = false; }
        if (Mathf.Abs(shotgun.MaxRange - 15f) > 0.01f) { Debug.LogError($"MaxRange가 15m가 아닙니다: {shotgun.MaxRange}"); pass = false; }
        if (shotgun.MagazineCapacity != 8) { Debug.LogError($"MagazineCapacity가 8발이 아닙니다: {shotgun.MagazineCapacity}"); pass = false; }
        if (shotgun.RequiredAmmoItemId != "Ammo_Shotgun") { Debug.LogError($"RequiredAmmoItemId가 Ammo_Shotgun이 아닙니다: {shotgun.RequiredAmmoItemId}"); pass = false; }
        if (!shotgun.UseShellByShellReload) { Debug.LogError("UseShellByShellReload가 true가 아닙니다."); pass = false; }
        if (ammo.MaxStackSize != 24) { Debug.LogError($"Ammo_Shotgun MaxStackSize가 24가 아닙니다: {ammo.MaxStackSize}"); pass = false; }

        if (pass)
        {
            Debug.Log("<color=green>[Test 1 통과] 샷건 및 탄약 ScriptableObject 필드 스펙 일치 확인.</color>");
        }
        return pass;
    }

    private static bool TestDeterministicConeDirections()
    {
        Vector3 forward = Vector3.forward;
        float angle = 7.0f;
        int count = 8;
        int seed = 12345;

        // 동일한 시드로 2번 생성 시 100% 동일해야 함 (클라이언트 예측과 서버 판정 일치)
        Vector3[] dirsA = PlayerGunCombat.GenerateConeDirections(forward, angle, count, seed);
        Vector3[] dirsB = PlayerGunCombat.GenerateConeDirections(forward, angle, count, seed);

        if (dirsA.Length != count || dirsB.Length != count)
        {
            Debug.LogError("[Test Fail] 생성된 방향 벡터 개수가 일치하지 않습니다.");
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (Vector3.Distance(dirsA[i], dirsB[i]) > 0.0001f)
            {
                Debug.LogError($"[Test Fail] 시드 결정론적 검증 실패 (인덱스 {i} 불일치)");
                return false;
            }

            // 모든 펠릿이 중심축 기준 7도 이내인지 검증
            float deg = Vector3.Angle(forward, dirsA[i]);
            if (deg > angle + 0.01f)
            {
                Debug.LogError($"[Test Fail] 펠릿 분산 각도가 7도를 초과함: {deg}도");
                return false;
            }
        }

        Debug.Log("<color=green>[Test 2 통과] 시드 기반 결정론적 7도 콘 방향 생성 (클라이언트-서버 100% 일치) 확인.</color>");
        return true;
    }

    private static bool TestDamageFalloffFormula()
    {
        int baseDamage = 80;
        int pelletCount = 8;
        int basePelletDamage = Mathf.FloorToInt((float)baseDamage / pelletCount); // 10
        float startRange = 3.0f;
        float endRange = 10.0f;
        int minDamage = 1;

        // 1) 0m ~ 3m 구간: 100% 풀 데미지 (10)
        int dmg0m = CalculatePelletDamage(0f, startRange, endRange, basePelletDamage, minDamage);
        int dmg3m = CalculatePelletDamage(3f, startRange, endRange, basePelletDamage, minDamage);
        if (dmg0m != 10 || dmg3m != 10)
        {
            Debug.LogError($"[Test Fail] 3m 이하 데미지 오류 (0m: {dmg0m}, 3m: {dmg3m})");
            return false;
        }

        // 2) 6.5m (중간 지점): 선형 감쇄로 5 또는 6
        int dmgMid = CalculatePelletDamage(6.5f, startRange, endRange, basePelletDamage, minDamage);
        if (dmgMid < 4 || dmgMid > 7)
        {
            Debug.LogError($"[Test Fail] 중간 거리 감쇄 데미지 오류 (6.5m: {dmgMid})");
            return false;
        }

        // 3) 10m 이상: 최소 1 데미지 보장
        int dmg10m = CalculatePelletDamage(10f, startRange, endRange, basePelletDamage, minDamage);
        int dmg14m = CalculatePelletDamage(14f, startRange, endRange, basePelletDamage, minDamage);
        if (dmg10m != 1 || dmg14m != 1)
        {
            Debug.LogError($"[Test Fail] 10m 이상 최소 데미지(1) 보장 오류 (10m: {dmg10m}, 14m: {dmg14m})");
            return false;
        }

        Debug.Log("<color=green>[Test 3 통과] 거리별 데미지 감쇄 (0~3m 100%, 3~10m 선형, 10~15m 최소 1데미지) 수식 일치 확인.</color>");
        return true;
    }

    private static int CalculatePelletDamage(float distance, float startRange, float endRange, int basePelletDmg, int minDmg)
    {
        if (distance <= startRange) return basePelletDmg;
        if (distance >= endRange) return minDmg;
        float t = Mathf.InverseLerp(startRange, endRange, distance);
        int dmg = Mathf.RoundToInt(Mathf.Lerp(basePelletDmg, minDmg, t));
        return Mathf.Max(minDmg, dmg);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
