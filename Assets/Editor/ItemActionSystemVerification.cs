using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 아이템 발사(IFireable) 및 홀드 사용(IUsable) 메커니즘을 자동 검증하는 에디터 테스트 도구입니다.
/// </summary>
public static class ItemActionSystemVerification
{
    [MenuItem("Tools/Verify Item Fire and Hold Use System")]
    public static void RunAllTests()
    {
        Debug.Log("==== [아이템 발사 & 홀드 사용 시스템 검증 시작] ====");
        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Debug.Log($"<color=green>[PASS]</color> {testName}");
                passed++;
            }
            else
            {
                Debug.LogError($"<color=red>[FAIL]</color> {testName}");
                failed++;
            }
        }

        try
        {
            // 1. 인터페이스 정의 검증
            Type fireableType = typeof(IFireable);
            Assert(fireableType.IsInterface, "IFireable이 인터페이스로 정의됨");
            Assert(fireableType.GetMethod("CanFire") != null, "IFireable.CanFire() 선언 확인");
            Assert(fireableType.GetMethod("Fire") != null, "IFireable.Fire() 선언 확인");

            Type usableType = typeof(IUsable);
            Assert(usableType.IsInterface, "IUsable이 인터페이스로 정의됨");
            Assert(usableType.GetMethod("CanUse") != null, "IUsable.CanUse() 선언 확인");
            Assert(usableType.GetMethod("OnUseStart") != null, "IUsable.OnUseStart() 선언 확인");
            Assert(usableType.GetMethod("OnUseUpdate") != null, "IUsable.OnUseUpdate() 선언 확인");
            Assert(usableType.GetMethod("OnUseEnd") != null, "IUsable.OnUseEnd() 선언 확인");
            Assert(usableType.GetProperty("RequiredHoldDuration") != null, "IUsable.RequiredHoldDuration 프로퍼티 확인");

            // 2. PlayerInteraction 프로퍼티 검증
            Type playerInteractionType = typeof(PlayerInteraction);
            Assert(playerInteractionType.GetProperty("FireableItem") != null, "PlayerInteraction.FireableItem 프로퍼티 존재 확인");
            Assert(playerInteractionType.GetProperty("UsableItem") != null, "PlayerInteraction.UsableItem 프로퍼티 존재 확인");
            Assert(playerInteractionType.GetProperty("IsUsingItem") != null, "PlayerInteraction.IsUsingItem 프로퍼티 존재 확인");

            // 3. 인메모리 상호작용 및 아이템 획득 시 인터페이스 캐싱 검증
            GameObject playerGO = new GameObject("TestPlayer");
            var interactor = playerGO.AddComponent<PlayerInteraction>();

            GameObject gunGO = new GameObject("TestGun");
            gunGO.AddComponent<Rigidbody>();
            var gunItem = gunGO.AddComponent<SampleGunItem>();

            GameObject medkitGO = new GameObject("TestMedkit");
            medkitGO.AddComponent<Rigidbody>();
            var medkitItem = medkitGO.AddComponent<SampleMedkitItem>();

            GameObject chargedGO = new GameObject("TestChargedWeapon");
            chargedGO.AddComponent<Rigidbody>();
            var chargedItem = chargedGO.AddComponent<SampleChargedWeaponItem>();

            GameObject normalBoxGO = new GameObject("TestNormalBox");
            normalBoxGO.AddComponent<Rigidbody>();
            var normalItem = normalBoxGO.AddComponent<PickableItem>();

            try
            {
                // A. 권총 줍기 검증 (IFireable 단독)
                gunItem.Interact(interactor);
                Assert(interactor.IsHoldingItem, "권총 획득 후 IsHoldingItem은 true여야 함");
                Assert(interactor.FireableItem != null, "권총 획득 후 FireableItem이 캐싱되어야 함");
                Assert(interactor.UsableItem == null, "권총 획득 후 UsableItem은 null이어야 함");
                Assert(interactor.FireableItem.CanFire(interactor), "권총 CanFire는 true여야 함");
                interactor.FireableItem.Fire(interactor); // 디버그 로그 정상 호출 확인

                // 내려놓기
                interactor.DropHeldItem();
                Assert(!interactor.IsHoldingItem, "내려놓은 후 IsHoldingItem은 false여야 함");
                Assert(interactor.FireableItem == null, "내려놓은 후 FireableItem은 null이어야 함");

                // B. 구급키트 줍기 검증 (IUsable 단독)
                medkitItem.Interact(interactor);
                Assert(interactor.IsHoldingItem, "구급키트 획득 후 IsHoldingItem은 true여야 함");
                Assert(interactor.FireableItem == null, "구급키트 획득 후 FireableItem은 null이어야 함");
                Assert(interactor.UsableItem != null, "구급키트 획득 후 UsableItem이 캐싱되어야 함");
                Assert(interactor.UsableItem.RequiredHoldDuration > 0f, "구급키트 RequiredHoldDuration > 0 확인");
                Assert(interactor.UsableItem.CanUse(interactor), "구급키트 CanUse는 true여야 함");

                // 홀드 사용 라이프사이클 호출 검증
                interactor.UsableItem.OnUseStart(interactor);
                interactor.UsableItem.OnUseUpdate(interactor, 0.5f);
                interactor.UsableItem.OnUseEnd(interactor, true);

                interactor.DropHeldItem();
                Assert(interactor.UsableItem == null, "내려놓은 후 UsableItem은 null이어야 함");

                // C. 차지무기 줍기 검증 (IFireable & IUsable 복합)
                chargedItem.Interact(interactor);
                Assert(interactor.FireableItem != null, "차지무기 획득 후 FireableItem 캐싱 확인");
                Assert(interactor.UsableItem != null, "차지무기 획득 후 UsableItem 캐싱 확인");
                interactor.FireableItem.Fire(interactor);
                interactor.UsableItem.OnUseStart(interactor);
                interactor.UsableItem.OnUseUpdate(interactor, 1.0f);
                interactor.UsableItem.OnUseEnd(interactor, true);
                interactor.DropHeldItem();

                // D. 일반 상자 (발사/사용 없음) 검증
                normalItem.Interact(interactor);
                Assert(interactor.FireableItem == null, "일반 상자는 FireableItem이 null이어야 함");
                Assert(interactor.UsableItem == null, "일반 상자는 UsableItem이 null이어야 함");
                interactor.DropHeldItem();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGO);
                UnityEngine.Object.DestroyImmediate(gunGO);
                UnityEngine.Object.DestroyImmediate(medkitGO);
                UnityEngine.Object.DestroyImmediate(chargedGO);
                UnityEngine.Object.DestroyImmediate(normalBoxGO);
            }

            Debug.Log($"==== [검증 완료] 통과: {passed}개, 실패: {failed}개 ====");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[검증 중 예외 발생] {ex}");
            failed++;
        }
    }

    [MenuItem("Tools/Simulate Pick Up and Fire Gun in Play Mode")]
    public static void SimulatePickUpAndFireGun()
    {
        var player = GameObject.Find("Player (Direct Play)");
        var gun = GameObject.Find("SampleGun_Instance");
        if (player != null && gun != null)
        {
            var interactor = player.GetComponent<PlayerInteraction>();
            var pickable = gun.GetComponent<PickableItem>();
            if (interactor != null && pickable != null)
            {
                if (interactor.IsHoldingItem) interactor.DropHeldItem();
                pickable.Interact(interactor);
                Debug.Log("[Simulate] 권총 획득 완료!");
                if (interactor.FireableItem != null && interactor.FireableItem.CanFire(interactor))
                {
                    interactor.FireableItem.Fire(interactor);
                }
            }
        }
    }

    [MenuItem("Tools/Simulate Pick Up and Use Medkit in Play Mode")]
    public static void SimulatePickUpAndUseMedkit()
    {
        var player = GameObject.Find("Player (Direct Play)");
        var medkit = GameObject.Find("SampleMedkit_Instance");
        if (player != null && medkit != null)
        {
            var interactor = player.GetComponent<PlayerInteraction>();
            var pickable = medkit.GetComponent<PickableItem>();
            if (interactor != null && pickable != null)
            {
                if (interactor.IsHoldingItem) interactor.DropHeldItem();
                pickable.Interact(interactor);
                Debug.Log("[Simulate] 구급키트 획득 완료!");
                if (interactor.UsableItem != null && interactor.UsableItem.CanUse(interactor))
                {
                    interactor.UsableItem.OnUseStart(interactor);
                    interactor.UsableItem.OnUseUpdate(interactor, 0.5f);
                    interactor.UsableItem.OnUseUpdate(interactor, 1.5f);
                    interactor.UsableItem.OnUseEnd(interactor, true);
                }
            }
        }
    }

    [MenuItem("Tools/Simulate Pick Up and Action Charged Weapon in Play Mode")]
    public static void SimulatePickUpAndActionCharged()
    {
        var player = GameObject.Find("Player (Direct Play)");
        var charged = GameObject.Find("SampleChargedWeapon_Instance");
        if (player != null && charged != null)
        {
            var interactor = player.GetComponent<PlayerInteraction>();
            var pickable = charged.GetComponent<PickableItem>();
            if (interactor != null && pickable != null)
            {
                if (interactor.IsHoldingItem) interactor.DropHeldItem();
                pickable.Interact(interactor);
                Debug.Log("[Simulate] 차지 라이플 획득 완료!");
                if (interactor.FireableItem != null)
                {
                    Debug.Log("[Simulate] 단발 탭 발사 시뮬레이션:");
                    interactor.FireableItem.Fire(interactor);
                }
                if (interactor.UsableItem != null)
                {
                    Debug.Log("[Simulate] 홀드 차지 사용 시뮬레이션:");
                    interactor.UsableItem.OnUseStart(interactor);
                    interactor.UsableItem.OnUseUpdate(interactor, 1.0f);
                    interactor.UsableItem.OnUseUpdate(interactor, 2.0f);
                    interactor.UsableItem.OnUseEnd(interactor, true);
                }
            }
        }
    }
}
