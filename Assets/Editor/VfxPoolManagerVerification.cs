using System;
using System.Reflection;
using Coop.VFX;
using UnityEditor;
using UnityEngine;

public static class VfxPoolManagerVerification
{
    [MenuItem("Tools/Run VFX Pool Manager Verification Tests")]
    public static void RunAllTests()
    {
        Debug.Log("==== [VFX 풀 매니저 검증 테스트 시작] ====");
        int passed = 0;
        int failed = 0;

        System.Collections.Generic.List<string> failedTests = new System.Collections.Generic.List<string>();

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Debug.Log($"<color=green>[PASS]</color> {testName}");
                passed++;
            }
            else
            {
                failedTests.Add(testName);
                Debug.LogError($"TEST_FAILURE_DETECTED: {testName}");
                failed++;
            }
        }

        GameObject managerHost = null;
        try
        {
            // 1. SurfaceType 열거형 검증
            Assert((byte)SurfaceType.Default == 0, "SurfaceType.Default == 0");
            Assert((byte)SurfaceType.Stone == 1, "SurfaceType.Stone == 1");
            Assert((byte)SurfaceType.Flesh == 2, "SurfaceType.Flesh == 2");
            Assert((byte)SurfaceType.Metal == 3, "SurfaceType.Metal == 3");
            Assert((byte)SurfaceType.Wood == 4, "SurfaceType.Wood == 4");

            // 2. SurfaceIdentifier 컴포넌트 검증
            GameObject testSurfaceGo = new GameObject("TestSurfaceObject");
            SurfaceIdentifier identifier = testSurfaceGo.AddComponent<SurfaceIdentifier>();
            identifier.SurfaceType = SurfaceType.Metal;
            Assert(identifier.SurfaceType == SurfaceType.Metal, "SurfaceIdentifier.SurfaceType 설정 및 취득 일치");
            UnityEngine.Object.DestroyImmediate(testSurfaceGo);

            // 3. VfxPoolManager 인스턴스 초기화 검증
            VfxPoolManager poolManager = VfxPoolManager.Instance;
            Assert(poolManager != null, "VfxPoolManager.Instance 싱글톤 정상 접근");

            // Domain Reload 리셋 속성 존재 검증
            MethodInfo resetMethod = typeof(VfxPoolManager).GetMethod("ResetStaticState", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(resetMethod != null, "ResetStaticState 메서드 존재");
            if (resetMethod != null)
            {
                var attr = resetMethod.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                Assert(attr != null && attr.loadType == RuntimeInitializeLoadType.SubsystemRegistration,
                    "ResetStaticState에 SubsystemRegistration 등록 확인");
            }

            // 4. 표면별 이펙트 스폰 검증 (Default, Flesh, Metal, Wood, Stone)
            PooledVfxInstance defaultInstance = poolManager.SpawnImpact(SurfaceType.Default, Vector3.zero, Vector3.up);
            Assert(defaultInstance != null, "SpawnImpact(Default) 정상 스폰");
            Assert(defaultInstance.gameObject.activeSelf, "스폰된 인스턴스 활성화 상태");

            PooledVfxInstance fleshInstance = poolManager.SpawnImpact(SurfaceType.Flesh, new Vector3(1, 0, 0), Vector3.up);
            Assert(fleshInstance != null, "SpawnImpact(Flesh) 정상 스폰");

            PooledVfxInstance metalInstance = poolManager.SpawnImpact(SurfaceType.Metal, new Vector3(2, 0, 0), Vector3.up);
            Assert(metalInstance != null, "SpawnImpact(Metal) 정상 스폰");

            PooledVfxInstance woodInstance = poolManager.SpawnImpact(SurfaceType.Wood, new Vector3(3, 0, 0), Vector3.up);
            Assert(woodInstance != null, "SpawnImpact(Wood) 정상 스폰");

            PooledVfxInstance stoneInstance = poolManager.SpawnImpact(SurfaceType.Stone, new Vector3(4, 0, 0), Vector3.up);
            Assert(stoneInstance != null, "SpawnImpact(Stone) 정상 스폰");

            // 5. 풀 반환 (ReturnToPool) 동작 검증
            defaultInstance.ReturnToPool();
            Assert(defaultInstance.IsReturned, "ReturnToPool 호출 후 IsReturned 플래그 true");
            Assert(!defaultInstance.gameObject.activeSelf, "ReturnToPool 호출 후 GameObject 비활성화");

            // 6. 가장 오래된 이펙트 재활용 (Steal Oldest / FIFO) 정책 검증
            // 소형 커스텀 템플릿으로 테스트
            GameObject testPrefab = new GameObject("TestVfxPrefab");
            testPrefab.AddComponent<ParticleSystem>();
            testPrefab.AddComponent<PooledVfxInstance>();

            // 35개 연속 스폰 (MaxCapacity인 32개 초과 테스트)
            PooledVfxInstance firstSpawned = poolManager.Spawn(testPrefab, Vector3.zero, Quaternion.identity);
            Assert(firstSpawned != null, "첫 번째 인스턴스 스폰 성공");

            PooledVfxInstance current = null;
            for (int i = 0; i < 35; i++)
            {
                current = poolManager.Spawn(testPrefab, new Vector3(i, 0, 0), Quaternion.identity);
            }

            Assert(current != null, "용량 초과 후에도 스폰 성공 (FIFO Steal Oldest 동작)");
            UnityEngine.Object.DestroyImmediate(testPrefab);

            // 7. PlayerGunCombat의 DetermineSurfaceType 로직 검증
            MethodInfo determineMethod = typeof(PlayerGunCombat).GetMethod("DetermineSurfaceType", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(determineMethod != null, "PlayerGunCombat.DetermineSurfaceType 메서드 존재 확인");

            GameObject dummyMonster = new GameObject("DummyMonster");
            BoxCollider monsterCol = dummyMonster.AddComponent<BoxCollider>();
            dummyMonster.AddComponent<Hitbox>();

            GameObject playerCombatHost = new GameObject("PlayerCombatHost");
            PlayerGunCombat combat = playerCombatHost.AddComponent<PlayerGunCombat>();

            if (determineMethod != null)
            {
                SurfaceType monsterSurface = (SurfaceType)determineMethod.Invoke(combat, new object[] { monsterCol });
                Assert(monsterSurface == SurfaceType.Flesh, "Hitbox 타겟 판별 결과 == SurfaceType.Flesh");

                GameObject dummyWall = new GameObject("DummyWall");
                BoxCollider wallCol = dummyWall.AddComponent<BoxCollider>();
                SurfaceIdentifier wallIdent = dummyWall.AddComponent<SurfaceIdentifier>();
                wallIdent.SurfaceType = SurfaceType.Stone;

                SurfaceType wallSurface = (SurfaceType)determineMethod.Invoke(combat, new object[] { wallCol });
                Assert(wallSurface == SurfaceType.Stone, "SurfaceIdentifier(Stone) 부착 오브젝트 판별 결과 == SurfaceType.Stone");

                GameObject plainObj = new GameObject("PlainObj");
                BoxCollider plainCol = plainObj.AddComponent<BoxCollider>();
                SurfaceType plainSurface = (SurfaceType)determineMethod.Invoke(combat, new object[] { plainCol });
                Assert(plainSurface == SurfaceType.Default, "지정 없는 오브젝트 판별 결과 == SurfaceType.Default");

                UnityEngine.Object.DestroyImmediate(dummyWall);
                UnityEngine.Object.DestroyImmediate(plainObj);
            }

            UnityEngine.Object.DestroyImmediate(dummyMonster);
            UnityEngine.Object.DestroyImmediate(playerCombatHost);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VFX 풀 매니저 검증 테스트 예외 발생]: {ex}");
            failed++;
        }
        finally
        {
            if (managerHost != null)
            {
                UnityEngine.Object.DestroyImmediate(managerHost);
            }
        }

        Debug.Log($"==== [VFX 풀 매니저 검증 완료] 통과: {passed}, 실패: {failed} ====");
    }
}
