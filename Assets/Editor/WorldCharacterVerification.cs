using System;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 및 NPC 캐릭터 시스템의 권한/소유권 분리 모델 및 컴포넌트 구성을 검증하는 에디터 테스트 스크립트입니다.
/// </summary>
public static class WorldCharacterVerification
{
    [MenuItem("Tools/Coop/Verify World Characters")]
    public static void RunVerification()
    {
        Debug.Log("================ [World Character System Verification Start] ================");
        bool allPassed = true;

        // 1. 프리팹 생성 실행
        try
        {
            CreateWorldCharacters.CreateAllPrefabs();
            Debug.Log("[PASS] 프리팹 생성(CreateAllPrefabs) 성공");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FAIL] 프리팹 생성 실패: {ex.Message}");
            allPassed = false;
        }

        // 2. Monster 프리팹 검증
        GameObject monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterPrefab.prefab");
        if (monsterPrefab != null)
        {
            Debug.Log("[PASS] MonsterPrefab 에셋 로드 성공");
            allPassed &= VerifyCharacterComponents(monsterPrefab, typeof(MonsterController), isServerAuthoritative: true);
        }
        else
        {
            Debug.LogError("[FAIL] MonsterPrefab.prefab을 찾을 수 없습니다.");
            allPassed = false;
        }

        // 3. NPC 프리팹 검증
        GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPCPrefab.prefab");
        if (npcPrefab != null)
        {
            Debug.Log("[PASS] NPCPrefab 에셋 로드 성공");
            allPassed &= VerifyCharacterComponents(npcPrefab, typeof(NPCController), isServerAuthoritative: true);

            // IInteractable 구현 확인
            var interactable = npcPrefab.GetComponent<IInteractable>();
            if (interactable != null)
            {
                Debug.Log($"[PASS] NPC IInteractable 인터페이스 구현 확인 (Prompt: '{interactable.GetInteractionPrompt()}')");
            }
            else
            {
                Debug.LogError("[FAIL] NPCPrefab에 IInteractable 인터페이스가 구현되지 않았습니다.");
                allPassed = false;
            }
        }
        else
        {
            Debug.LogError("[FAIL] NPCPrefab.prefab을 찾을 수 없습니다.");
            allPassed = false;
        }

        // 4. Player 프리팹 검증
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab");
        if (playerPrefab != null)
        {
            Debug.Log("[PASS] PlayerPrefab 에셋 로드 성공");
            allPassed &= VerifyPlayerAnimatorComponents(playerPrefab);
        }
        else
        {
            Debug.LogError("[FAIL] PlayerPrefab.prefab을 찾을 수 없습니다.");
            allPassed = false;
        }

        // 5. 소유권(Ownership) 및 권한(Authority) 분리 논리 검증 (인스턴스 생성 후 프로퍼티 검증)
        GameObject testMonster = new GameObject("TestMonsterVerification");
        try
        {
            var controller = testMonster.AddComponent<MonsterController>();

            // HasPlayerOwner 확인 (호스트든 누구든 플레이어 소유가 아님)
            if (!controller.HasPlayerOwner)
            {
                Debug.Log("[PASS] MonsterController.HasPlayerOwner == false (소유권 격리 확인)");
            }
            else
            {
                Debug.LogError("[FAIL] MonsterController.HasPlayerOwner가 true를 반환합니다!");
                allPassed = false;
            }

            // IsPlayerControlled 확인 (플레이어 입력/제어 차단)
            if (!controller.IsPlayerControlled)
            {
                Debug.Log("[PASS] MonsterController.IsPlayerControlled == false (제어권 격리 확인)");
            }
            else
            {
                Debug.LogError("[FAIL] MonsterController.IsPlayerControlled가 true를 반환합니다!");
                allPassed = false;
            }

            // IsServerAuthoritative 확인
            if (controller.IsServerAuthoritative)
            {
                Debug.Log("[PASS] MonsterController.IsServerAuthoritative == true (서버 권한 확인)");
            }
            else
            {
                Debug.LogError("[FAIL] MonsterController.IsServerAuthoritative가 false를 반환합니다!");
                allPassed = false;
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testMonster);
        }

        if (allPassed)
        {
            Debug.Log("<color=green>================ [ALL TESTS PASSED: World Character System & Animators Verified] ================</color>");
        }
        else
        {
            Debug.LogError("================ [SOME TESTS FAILED: Please check logs above] ================");
        }
    }

    private static bool VerifyCharacterComponents(GameObject prefab, Type controllerType, bool isServerAuthoritative)
    {
        bool ok = true;

        // 1. NetworkObject
        var netObj = prefab.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"[PASS] {prefab.name}: NetworkObject 확인됨");
        }
        else
        {
            Debug.LogError($"[FAIL] {prefab.name}: NetworkObject 없음");
            ok = false;
        }

        // 2. NetworkTransform (서버 권한)
        var netTrans = prefab.GetComponent<NetworkTransform>();
        if (netTrans != null)
        {
            // ClientNetworkTransform이 아닌 기본 Unity NetworkTransform인지 확인
            if (netTrans.GetType() == typeof(NetworkTransform))
            {
                Debug.Log($"[PASS] {prefab.name}: Server-Authoritative NetworkTransform 확인됨");
            }
            else
            {
                Debug.LogWarning($"[WARN] {prefab.name}: NetworkTransform 파생 클래스({netTrans.GetType().Name}) 사용 중");
            }
        }
        else
        {
            Debug.LogError($"[FAIL] {prefab.name}: NetworkTransform 없음");
            ok = false;
        }

        // 3. Controller
        var ctrl = prefab.GetComponent(controllerType);
        if (ctrl != null)
        {
            Debug.Log($"[PASS] {prefab.name}: {controllerType.Name} 컴포넌트 확인됨");
        }
        else
        {
            Debug.LogError($"[FAIL] {prefab.name}: {controllerType.Name} 컴포넌트 없음");
            ok = false;
        }

        // 4. Collider
        var col = prefab.GetComponent<Collider>();
        if (col != null)
        {
            Debug.Log($"[PASS] {prefab.name}: Collider 확인됨 ({col.GetType().Name})");
        }
        else
        {
            Debug.LogError($"[FAIL] {prefab.name}: Collider 없음");
            ok = false;
        }

        // 5. NavMeshAgent
        var agent = prefab.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            Debug.Log($"[PASS] {prefab.name}: NavMeshAgent 확인됨");
        }
        else
        {
            Debug.LogWarning($"[WARN] {prefab.name}: NavMeshAgent 없음 (선택 사항)");
        }

        // 6. Animator
        var animator = prefab.GetComponent<Animator>();
        if (animator != null)
        {
            Debug.Log($"[PASS] {prefab.name}: Animator 컴포넌트 확인됨");
        }
        else
        {
            Debug.LogError($"[FAIL] {prefab.name}: Animator 컴포넌트 없음");
            ok = false;
        }

        // 7. NetworkAnimator
        var netAnim = prefab.GetComponent<NetworkAnimator>();
        if (netAnim != null)
        {
            if (isServerAuthoritative && netAnim.GetType() == typeof(NetworkAnimator))
            {
                Debug.Log($"[PASS] {prefab.name}: Server-Authoritative NetworkAnimator 확인됨");
            }
            else if (!isServerAuthoritative && netAnim is ClientNetworkAnimator)
            {
                Debug.Log($"[PASS] {prefab.name}: Client-Authoritative ClientNetworkAnimator 확인됨");
            }
            else
            {
                Debug.LogWarning($"[WARN] {prefab.name}: 예상과 다른 NetworkAnimator 타입({netAnim.GetType().Name})");
            }

            if (netAnim.Animator == animator)
            {
                Debug.Log($"[PASS] {prefab.name}: NetworkAnimator.Animator 바인딩 정상");
            }
            else
            {
                Debug.LogError($"[FAIL] {prefab.name}: NetworkAnimator.Animator가 바인딩되지 않았습니다.");
                ok = false;
            }
        }
        else
        {
            Debug.LogError($"[FAIL] {prefab.name}: NetworkAnimator 컴포넌트 없음");
            ok = false;
        }

        return ok;
    }

    private static bool VerifyPlayerAnimatorComponents(GameObject playerPrefab)
    {
        bool ok = true;

        var animator = playerPrefab.GetComponent<Animator>();
        if (animator != null)
        {
            Debug.Log("[PASS] PlayerPrefab: Animator 컴포넌트 확인됨");
        }
        else
        {
            Debug.LogError("[FAIL] PlayerPrefab: Animator 컴포넌트 없음");
            ok = false;
        }

        var clientNetAnim = playerPrefab.GetComponent<ClientNetworkAnimator>();
        if (clientNetAnim != null)
        {
            Debug.Log("[PASS] PlayerPrefab: ClientNetworkAnimator (Client-Authoritative) 확인됨");
            if (clientNetAnim.Animator == animator)
            {
                Debug.Log("[PASS] PlayerPrefab: ClientNetworkAnimator.Animator 바인딩 정상");
            }
            else
            {
                Debug.LogError("[FAIL] PlayerPrefab: ClientNetworkAnimator.Animator가 바인딩되지 않았습니다.");
                ok = false;
            }
        }
        else
        {
            Debug.LogError("[FAIL] PlayerPrefab: ClientNetworkAnimator 컴포넌트 없음");
            ok = false;
        }

        return ok;
    }
}
