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
    private const string ITEM_GUN_PATH = "Assets/ItemData/SampleGunItemData.asset";
    private const string ITEM_MEDKIT_PATH = "Assets/ItemData/SampleMedkitItemData.asset";
    private const string ITEM_CHARGED_PATH = "Assets/ItemData/SampleChargedWeaponItemData.asset";
    private const string ITEM_BOX_PATH = "Assets/ItemData/PickableBoxItemData.asset";
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

        // 2. PlayerPrefab 컴포넌트 검증
        if (VerifyPlayerPrefab()) passed++; else failed++;

        // 3. 인벤토리 데이터 로직 단위 테스트 (스왑, 스택, 툴바 선택)
        if (VerifyInventoryLogic()) passed++; else failed++;

        // 4. GameScene UI 배치 검증
        if (VerifyGameScene()) passed++; else failed++;

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
        // GameObject 기반 임시 인벤토리 생성 후 단위 테스트 실행
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

            // 5. 툴바 슬롯 선택 테스트 (숫자키 시뮬레이션)
            inv.SelectToolbarSlot(3);
            if (inv.SelectedToolbarIndex != 3)
            {
                Debug.LogError($"[Verification] 툴바 슬롯 3번 선택 실패: {inv.SelectedToolbarIndex}");
                return false;
            }

            // 토글 테스트 (같은 번호 다시 선택 시 -1)
            inv.SelectToolbarSlot(3);
            if (inv.SelectedToolbarIndex != -1)
            {
                Debug.LogError($"[Verification] 툴바 슬롯 토글 해제 실패: {inv.SelectedToolbarIndex}");
                return false;
            }

            Object.DestroyImmediate(testItemA);
            Object.DestroyImmediate(testItemB);

            Debug.Log("<color=green>[PASS]</color> 인벤토리 슬롯 크기, 이동/스왑, 툴바 선택 로직 단위 테스트 성공.");
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
        bool opened = false;
        if (currentScene.path != GAME_SCENE_PATH)
        {
            currentScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            opened = true;
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
}
