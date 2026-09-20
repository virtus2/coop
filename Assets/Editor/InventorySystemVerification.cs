using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 인벤토리 및 툴바 시스템의 에셋, 프리팹, 씬 배치 및 코어 데이터 로직을 자동 검증하는 도구입니다.
/// </summary>
public static class InventorySystemVerification
{
    private const string ITEM_GUN_PATH = "Assets/Resources/ItemData/SampleGunItemData.asset";
    private const string ITEM_MEDKIT_PATH = "Assets/Resources/ItemData/SampleMedkitItemData.asset";
    private const string ITEM_CHARGED_PATH = "Assets/Resources/ItemData/SampleChargedWeaponItemData.asset";
    private const string ITEM_BOX_PATH = "Assets/Resources/ItemData/PickableBoxItemData.asset";
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Verify Inventory System")]
    public static void RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        Debug.Log("<color=cyan>================ [인벤토리 시스템 자동 검증 시작] ================</color>");

        // 1. ItemData 에셋 검증
        if (VerifyItemData()) passed++; else failed++;

        // 2. ItemDatabase 조회 검증
        if (VerifyItemDatabase()) passed++; else failed++;

        // 3. PlayerPrefab 컴포넌트 검증
        if (VerifyPlayerPrefab()) passed++; else failed++;

        // 4. 인벤토리 데이터 로직 단위 테스트 (스왑, 스택, 툴바 선택 유지, 드롭 시 제거)
        if (VerifyInventoryLogic()) passed++; else failed++;

        // 5. GameScene UI 배치 검증
        if (VerifyGameScene()) passed++; else failed++;

        // 6. 물리 및 렌더링 최적화 검증 (기법 1, 2, 6)
        if (VerifyPhysicsOptimization()) passed++; else failed++;

        Debug.Log($"<color=cyan>================ [검증 결과: 성공 {passed}건, 실패 {failed}건] ================</color>");

        if (failed == 0)
        {
            Debug.Log("<color=green>★ 모든 인벤토리 시스템 검증을 완벽하게 통과했습니다! ★</color>");
        }
        else
        {
            Debug.LogError($"<color=red>일부 검증 항목에서 문제가 발견되었습니다. (실패 {failed}건)</color>");
        }
    }

    private static bool VerifyItemData()
    {
        string[] paths = { ITEM_GUN_PATH, ITEM_MEDKIT_PATH, ITEM_CHARGED_PATH, ITEM_BOX_PATH };
        bool allOk = true;

        foreach (var path in paths)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
            {
                Debug.LogError($"[Verification] ItemData 에셋 누락: {path}");
                allOk = false;
            }
            else
            {
                if (item.HoldPrefab == null)
                {
                    Debug.LogWarning($"[Verification] ItemData의 HoldPrefab 누락: {path}");
                }
            }
        }

        if (allOk)
        {
            Debug.Log("<color=green>[PASS]</color> ItemData 4종 에셋 무결성 확인 완료.");
        }
        return allOk;
    }

    private static bool VerifyItemDatabase()
    {
        ItemDatabase.EnsureLoaded();

        string[] requiredIds = { "item_gun", "item_medkit", "item_charged_weapon", "item_box" };
        foreach (var id in requiredIds)
        {
            var item = ItemDatabase.GetItem(id);
            if (item == null)
            {
                Debug.LogError($"[Verification] ItemDatabase에서 아이템 조회 실패: {id}");
                return false;
            }
        }

        Debug.Log("<color=green>[PASS]</color> ItemDatabase 4종 아이템 조회 확인 완료.");
        return true;
    }

    private static bool VerifyPlayerPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[Verification] {PLAYER_PREFAB_PATH}을 찾을 수 없습니다.");
            return false;
        }

        var inventory = prefab.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("[Verification] PlayerPrefab에 PlayerInventory 컴포넌트가 없습니다.");
            return false;
        }

        var holder = prefab.GetComponent<PlayerItemHolder>();
        if (holder == null)
        {
            Debug.LogError("[Verification] PlayerPrefab에 PlayerItemHolder 컴포넌트가 없습니다.");
            return false;
        }

        Debug.Log("<color=green>[PASS]</color> PlayerPrefab의 PlayerInventory 및 PlayerItemHolder 부착 확인 완료.");
        return true;
    }

    private static bool VerifyInventoryLogic()
    {
        var testGO = new GameObject("TestInventoryGO");
        try
        {
            var inv = testGO.AddComponent<PlayerInventory>();
            inv.EnsureInitialized();

            // 1. 슬롯 크기 검증
            if (inv.ToolbarSlots.Count != 10)
            {
                Debug.LogError($"[Verification] 툴바 슬롯 크기 불일치: {inv.ToolbarSlots.Count} != 10");
                return false;
            }

            if (inv.GridTotalSize != 20)
            {
                Debug.LogError($"[Verification] 그리드 슬롯 크기 불일치: {inv.GridTotalSize} != 20");
                return false;
            }

            // 2. 가상의 ItemData 생성 후 아이템 추가 테스트
            var testItemA = ScriptableObject.CreateInstance<ItemData>();
            testItemA.Initialize("test_a", "Test Item A", null, "Desc A", 10, null, null);

            var testItemB = ScriptableObject.CreateInstance<ItemData>();
            testItemB.Initialize("test_b", "Test Item B", null, "Desc B", 1, null, null);

            // 툴바 0번에 추가
            inv.GetSlot(new SlotLocation(SlotType.Toolbar, 0)).Set(testItemA, 3);
            if (inv.GetSlot(new SlotLocation(SlotType.Toolbar, 0)).Quantity != 3)
            {
                Debug.LogError("[Verification] 툴바 슬롯 아이템 설정 실패");
                return false;
            }

            // 3. 툴바 0번 -> 그리드 5번 이동 (Move)
            var fromLoc = new SlotLocation(SlotType.Toolbar, 0);
            var toLoc = new SlotLocation(SlotType.Grid, 5);
            bool moveSuccess = inv.MoveOrSwap(fromLoc, toLoc);

            if (!moveSuccess || !inv.GetSlot(fromLoc).IsEmpty || inv.GetSlot(toLoc).Quantity != 3)
            {
                Debug.LogError("[Verification] 툴바 -> 그리드 이동 실패");
                return false;
            }

            // 4. 그리드 5번에 testItemB를 툴바 0번에 넣고 맞교환(Swap) 테스트
            inv.GetSlot(fromLoc).Set(testItemB, 1);
            bool swapSuccess = inv.MoveOrSwap(fromLoc, toLoc);

            if (!swapSuccess || inv.GetSlot(fromLoc).Item != testItemA || inv.GetSlot(toLoc).Item != testItemB)
            {
                Debug.LogError("[Verification] 슬롯 맞교환(Swap) 실패");
                return false;
            }

            // 5. 툴바 슬롯 선택 테스트: 3번 선택 -> 3번 활성화, 3번 재선택 -> -1로 토글 해제 (손 비우기 및 하이라이트 취소)
            inv.SelectToolbarSlot(3);
            if (inv.SelectedToolbarIndex != 3)
            {
                Debug.LogError($"[Verification] 툴바 슬롯 3번 선택 실패: {inv.SelectedToolbarIndex}");
                return false;
            }

            // 같은 번호 다시 선택 시 손에 있던 아이템을 비우고 하이라이트 취소 (-1)
            inv.SelectToolbarSlot(3);
            if (inv.SelectedToolbarIndex != -1)
            {
                Debug.LogError($"[Verification] 툴바 슬롯 재선택 시 토글 해제(-1) 실패: {inv.SelectedToolbarIndex} != -1");
                return false;
            }

            // 6. 드롭 시 툴바 아이템 차감/제거 테스트 (요구사항 3)
            inv.SelectToolbarSlot(0); // 툴바 0번에 testItemA (수량 3)
            inv.RemoveCurrentHeldItem(1);
            if (inv.GetSlot(new SlotLocation(SlotType.Toolbar, 0)).Quantity != 2)
            {
                Debug.LogError($"[Verification] 툴바 아이템 1개 차감 실패: {inv.GetSlot(new SlotLocation(SlotType.Toolbar, 0)).Quantity}");
                return false;
            }

            inv.RemoveCurrentHeldItem(2);
            if (!inv.GetSlot(new SlotLocation(SlotType.Toolbar, 0)).IsEmpty)
            {
                Debug.LogError("[Verification] 툴바 아이템 완전 소진 시 슬롯 비우기 실패");
                return false;
            }

            // 7. 바닥 아이템 툴바 슬롯 삽입 테스트 (PutItemIntoToolbarSlot)
            // 툴바 4번(빈 슬롯)에 testItemB 삽입
            bool putSuccess = inv.PutItemIntoToolbarSlot(4, testItemB, 1);
            if (!putSuccess || inv.GetSlot(new SlotLocation(SlotType.Toolbar, 4)).Item != testItemB)
            {
                Debug.LogError("[Verification] 빈 툴바 슬롯에 아이템 삽입 실패");
                return false;
            }

            // 툴바 4번에 이미 testItemB가 있을 때 testItemA 삽입 -> 기존 testItemB는 그리드로 이동하고 툴바 4번은 testItemA가 됨
            bool swapPutSuccess = inv.PutItemIntoToolbarSlot(4, testItemA, 1);
            if (!swapPutSuccess || inv.GetSlot(new SlotLocation(SlotType.Toolbar, 4)).Item != testItemA)
            {
                Debug.LogError("[Verification] 기존 아이템이 있는 툴바 슬롯에 새 아이템 삽입 실패");
                return false;
            }

            // 기존 testItemB가 그리드 슬롯으로 이동했는지 확인
            bool foundInGrid = false;
            foreach (var gridSlot in inv.GridSlots)
            {
                if (gridSlot.Item == testItemB)
                {
                    foundInGrid = true;
                    break;
                }
            }

            if (!foundInGrid)
            {
                Debug.LogError("[Verification] 툴바 밀려난 기존 아이템의 그리드 이동 실패");
                return false;
            }

            Object.DestroyImmediate(testItemA);
            Object.DestroyImmediate(testItemB);

            Debug.Log("<color=green>[PASS]</color> 인벤토리 슬롯 크기, 이동/스왑, 툴바 선택 유지, 드롭 차감 및 툴바 삽입 로직 단위 테스트 성공.");
            return true;
        }
        finally
        {
            Object.DestroyImmediate(testGO);
        }
    }

    private static bool VerifyGameScene()
    {
        var currentScene = SceneManager.GetActiveScene();
        if (currentScene.path != GAME_SCENE_PATH)
        {
            currentScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
        }

        var canvasGO = GameObject.Find("InventoryCanvas");
        if (canvasGO == null)
        {
            Debug.LogError("[Verification] GameScene에 InventoryCanvas가 존재하지 않습니다.");
            return false;
        }

        var controller = canvasGO.GetComponent<InventoryUIController>();
        if (controller == null)
        {
            Debug.LogError("[Verification] InventoryCanvas에 InventoryUIController 컴포넌트가 없습니다.");
            return false;
        }

        Debug.Log("<color=green>[PASS]</color> GameScene의 InventoryCanvas 및 InventoryUIController 확인 완료.");
        return true;
    }

    private static bool VerifyPhysicsOptimization()
    {
        // 1. PickableItem 레이어 등록 확인
        int pickableLayer = LayerMask.NameToLayer(PhysicsLayerSetup.PICKABLE_LAYER_NAME);
        if (pickableLayer < 0)
        {
            Debug.LogError($"[Verification] '{PhysicsLayerSetup.PICKABLE_LAYER_NAME}' 레이어가 TagManager에 등록되어 있지 않습니다.");
            return false;
        }

        // 2. 레이어 충돌 무시(Item vs Item) 설정 확인
        PhysicsLayerSetup.InitializePhysicsLayers();
        if (!Physics.GetIgnoreLayerCollision(pickableLayer, pickableLayer))
        {
            Debug.LogError("[Verification] PickableItem 레이어 간 충돌 무시 설정 실패.");
            return false;
        }

        // 3. 머티리얼 4종의 GPU Instancing 활성화 확인
        string[] matPaths = {
            "Assets/Materials/SampleGunMat.mat",
            "Assets/Materials/SampleMedkitMat.mat",
            "Assets/Materials/SampleChargedWeaponMat.mat",
            "Assets/Materials/PickableBoxMat.mat"
        };

        foreach (var path in matPaths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError($"[Verification] 머티리얼을 찾을 수 없습니다: {path}");
                return false;
            }

            if (!mat.enableInstancing)
            {
                Debug.LogError($"[Verification] 머티리얼의 GPU Instancing이 꺼져 있습니다: {path}");
                return false;
            }
        }

        // 4. PickableItem SettlePhysics 동작 단위 테스트
        var testGO = new GameObject("TestPickableGO");
        try
        {
            var rb = testGO.AddComponent<Rigidbody>();
            var pickable = testGO.AddComponent<PickableItem>();
            pickable.InternalDrop(Vector3.zero, Quaternion.identity);

            if (rb.isKinematic)
            {
                Debug.LogError("[Verification] InternalDrop 직후 isKinematic이 false가 아닙니다.");
                return false;
            }

            // 물리 안정화(Settle) 호출
            pickable.SettlePhysics();

            if (!pickable.IsSettled || !rb.isKinematic)
            {
                Debug.LogError("[Verification] SettlePhysics 호출 후 IsSettled 또는 isKinematic 전환 실패.");
                return false;
            }

            Debug.Log("<color=green>[PASS]</color> 물리 레이어 충돌 격리, GPU Instancing 4종 및 PickableItem Settle & Freeze 검증 완료.");
            return true;
        }
        finally
        {
            Object.DestroyImmediate(testGO);
        }
    }

    [MenuItem("Tools/Spawn 100 Test Items (Stress Test)")]
    public static void Spawn100TestItems()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PickableBox.prefab");
        if (prefab == null)
        {
            Debug.LogError("Assets/Prefabs/PickableBox.prefab을 찾을 수 없습니다.");
            return;
        }

        var player = GameObject.FindWithTag("Player");
        Vector3 basePos = player != null ? player.transform.position + player.transform.forward * 3f + Vector3.up * 2f : new Vector3(0, 3, 0);

        var parentGO = new GameObject("TestSpawned100Items");
        Undo.RegisterCreatedObjectUndo(parentGO, "Spawn 100 Test Items");

        for (int i = 0; i < 100; i++)
        {
            Vector3 spawnPos = basePos + new Vector3(
                Random.Range(-2f, 2f),
                Random.Range(0f, 3f),
                Random.Range(-2f, 2f)
            );
            Quaternion spawnRot = Random.rotation;

            var item = Object.Instantiate(prefab, spawnPos, spawnRot, parentGO.transform);
            Undo.RegisterCreatedObjectUndo(item, "Spawn Test Item");
        }

        Debug.Log($"<color=cyan>[Stress Test]</color> 테스트용 물리 아이템 100개를 성공적으로 스폰했습니다! 위치: {basePos}");
    }
}
