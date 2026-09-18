using System;
using System.Reflection;
using Coop.Audio;
using UnityEditor;
using UnityEngine;

public static class SoundManagerVerification
{
    [MenuItem("Tools/Run SoundManager Verification Tests")]
    public static void RunAllTests()
    {
        Debug.Log("==== [사운드매니저 검증 테스트 시작] ====");
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

        try
        {
            // 1. SoundPriority 열거형 검증
            Assert((int)SoundPriority.Low == 0, "SoundPriority.Low == 0");
            Assert((int)SoundPriority.Normal == 10, "SoundPriority.Normal == 10");
            Assert((int)SoundPriority.High == 20, "SoundPriority.High == 20");
            Assert((int)SoundPriority.Critical == 30, "SoundPriority.Critical == 30");

            // 2. LinearToDecibel 변환 검증
            float dbMax = SoundManager.LinearToDecibel(1.0f);
            float dbMin = SoundManager.LinearToDecibel(0.0f);
            float dbMute = SoundManager.LinearToDecibel(0.00001f);
            float dbTenth = SoundManager.LinearToDecibel(0.1f);

            Assert(Mathf.Approximately(dbMax, 0f), $"1.0 linear는 0dB여야 함 (실제: {dbMax})");
            Assert(Mathf.Approximately(dbMin, -80f), $"0.0 linear는 -80dB여야 함 (실제: {dbMin})");
            Assert(Mathf.Approximately(dbMute, -80f), $"0.00001 linear는 -80dB여야 함 (실제: {dbMute})");
            Assert(Mathf.Abs(dbTenth - (-20f)) < 0.01f, $"0.1 linear는 약 -20dB여야 함 (실제: {dbTenth})");

            // 3. SettingsManager 볼륨 프로퍼티 및 이벤트 검증
            float originalMaster = SettingsManager.MasterVolume;
            float originalBgm = SettingsManager.BgmVolume;
            float originalSfx = SettingsManager.SfxVolume;

            bool masterEventInvoked = false;
            Action<float> masterHandler = val => masterEventInvoked = true;
            SettingsManager.OnMasterVolumeChanged += masterHandler;

            SettingsManager.MasterVolume = 0.65f;
            Assert(masterEventInvoked, "SettingsManager.MasterVolume 변경 시 OnMasterVolumeChanged 이벤트 호출 확인");
            Assert(Mathf.Approximately(SettingsManager.MasterVolume, 0.65f), "SettingsManager.MasterVolume 값 저장 및 조회 확인");

            // Clamping 검증
            SettingsManager.MasterVolume = 1.5f;
            Assert(Mathf.Approximately(SettingsManager.MasterVolume, 1.0f), "SettingsManager.MasterVolume 상한 Clamping(1.0) 확인");
            SettingsManager.MasterVolume = -0.5f;
            Assert(Mathf.Approximately(SettingsManager.MasterVolume, 0.0f), "SettingsManager.MasterVolume 하한 Clamping(0.0) 확인");

            SettingsManager.OnMasterVolumeChanged -= masterHandler;

            // 원복
            SettingsManager.MasterVolume = originalMaster;
            SettingsManager.BgmVolume = originalBgm;
            SettingsManager.SfxVolume = originalSfx;

            // 4. SoundManager 인스턴스 생성 및 계층 구조 / 풀링 검증
            GameObject soundManagerObj = new GameObject("Test_SoundManager");
            SoundManager sm = soundManagerObj.AddComponent<SoundManager>();
            sm.EnsureInitialized();

            try
            {
                Assert(SoundManager.Instance == sm, "SoundManager 싱글톤 인스턴스 등록 확인");

                Transform sfxPoolTrans = soundManagerObj.transform.Find("SFX_Pool");
                Transform bgmTrans = soundManagerObj.transform.Find("BGM_Sources");
                Assert(sfxPoolTrans != null, "SFX_Pool 컨테이너 트랜스폼 생성 확인");
                Assert(bgmTrans != null, "BGM_Sources 컨테이너 트랜스폼 생성 확인");

                int sfxCount = sfxPoolTrans.childCount;
                Assert(sfxCount == 16, $"SFX_Pool 자식 AudioSource 개수 확인 (기대: 16, 실제: {sfxCount})");

                int bgmCount = bgmTrans.childCount;
                Assert(bgmCount == 2, $"BGM_Sources 자식 AudioSource 개수 확인 (기대: 2, 실제: {bgmCount})");

                // 5. 더미 오디오 클립 생성 후 재생 및 선점(Preemption) 로직 검증
                AudioClip dummyClipA = AudioClip.Create("DummyClipA", 44100, 1, 44100, false);
                AudioClip dummyClipB = AudioClip.Create("DummyClipB", 44100, 1, 44100, false);
                AudioClip dummyClipHigh = AudioClip.Create("DummyClipHigh", 44100, 1, 44100, false);

                // 16개 슬롯을 모두 Low 우선순위로 채움
                AudioSource[] allocatedSources = new AudioSource[16];
                for (int i = 0; i < 16; i++)
                {
                    allocatedSources[i] = sm.PlaySfx(dummyClipA, 1f, 1f, SoundPriority.Low, true); // loop=true로 유지
                    Assert(allocatedSources[i] != null, $"SFX 슬롯 {i} 할당 성공");
                }

                // 17번째 Low 우선순위 재생 요청 -> 여유 없고 선점 불가하므로 null 반환되어야 함
                AudioSource rejectedSource = sm.PlaySfx(dummyClipB, 1f, 1f, SoundPriority.Low, false);
                Assert(rejectedSource == null, "풀이 꽉 차고 동일/낮은 우선순위일 때 새 SFX 재생 거절(null 반환) 확인");

                // 18번째 High 우선순위 재생 요청 -> 가장 우선순위가 낮은 소스 중 하나를 선점(Preemption)해야 함
                AudioSource preemptedSource = sm.PlaySfx(dummyClipHigh, 1f, 1f, SoundPriority.High, false);
                Assert(preemptedSource != null, "우선순위가 더 높은 High 요청 시 기존 Low 사운드 선점 재생 성공 확인");
                Assert(preemptedSource.clip == dummyClipHigh, "선점된 오디오 소스에 새 High 클립이 바인딩되었는지 확인");

                // StopAllSfx 검증
                sm.StopAllSfx();
                int playingCount = 0;
                for (int i = 0; i < 16; i++)
                {
                    var src = sfxPoolTrans.GetChild(i).GetComponent<AudioSource>();
                    if (src != null && src.isPlaying)
                    {
                        playingCount++;
                    }
                }
                Assert(playingCount == 0, "StopAllSfx 호출 후 모든 SFX AudioSource 정지 확인");

                // 6. BGM 즉각 전환 및 루프 검증
                sm.PlayBgm(dummyClipA, 0f, true, 0.9f);
                Assert(sm.IsBgmPlaying, "BGM 재생 시작 확인");
                Assert(sm.CurrentBgmClip == dummyClipA, "현재 BGM 클립이 dummyClipA인지 확인");

                sm.PlayBgm(dummyClipB, 0f, true, 0.8f);
                Assert(sm.CurrentBgmClip == dummyClipB, "BGM 즉각 교체 후 dummyClipB로 변경 확인");

                sm.PauseBgm();
                sm.ResumeBgm();
                sm.StopBgm(0f);
                Assert(!sm.IsBgmPlaying, "StopBgm 후 BGM 정지 확인");

                // 7. 3D 사운드 위치 기반 재생 검증
                Vector3 testPos = new Vector3(10f, 2f, 5f);
                AudioSource sfx3D = sm.PlaySfxAt(dummyClipA, testPos, 0.8f, 1f, SoundPriority.Normal, 1.0f);
                Assert(sfx3D != null, "PlaySfxAt 3D 효과음 재생 성공 확인");
                Assert(sfx3D.spatialBlend == 1f, "3D 효과음의 spatialBlend가 1.0인지 확인");
                Assert(sfx3D.transform.position == testPos, "3D 효과음 AudioSource의 위치가 지정 위치와 일치하는지 확인");

                sm.StopAllSfx();

                // 8. 무작위 피치(Random Pitch) 재생 검증
                float minPitch = 0.85f;
                float maxPitch = 1.15f;
                AudioSource randomPitchSrc = sm.PlaySfxWithRandomPitch(dummyClipA, 1.0f, minPitch, maxPitch);
                Assert(randomPitchSrc != null, "PlaySfxWithRandomPitch 재생 성공 확인");
                Assert(randomPitchSrc.pitch >= minPitch && randomPitchSrc.pitch <= maxPitch,
                    $"피치 값이 지정 범위({minPitch}~{maxPitch}) 내에 있는지 확인 (실제 피치: {randomPitchSrc.pitch})");

                Vector3 pos3D = new Vector3(5f, 0f, 5f);
                AudioSource randomPitch3DSrc = sm.PlaySfxWithRandomPitchAt(dummyClipA, pos3D, 1.0f, minPitch, maxPitch);
                Assert(randomPitch3DSrc != null, "PlaySfxWithRandomPitchAt 3D 재생 성공 확인");
                Assert(randomPitch3DSrc.pitch >= minPitch && randomPitch3DSrc.pitch <= maxPitch,
                    $"3D 피치 값이 지정 범위 내에 있는지 확인 (실제 피치: {randomPitch3DSrc.pitch})");

                sm.StopAllSfx();

                // 9. 무작위 클립(Random Clip) 및 복합 재생 검증
                Assert(SoundManager.PickRandomClip(null) == null, "null 배열 전달 시 PickRandomClip null 반환 확인");
                Assert(SoundManager.PickRandomClip(new AudioClip[0]) == null, "빈 배열 전달 시 PickRandomClip null 반환 확인");

                AudioClip[] nullPaddedClips = new AudioClip[] { null, dummyClipA, null };
                AudioClip pickedValidClip = SoundManager.PickRandomClip(nullPaddedClips);
                Assert(pickedValidClip == dummyClipA, "null이 포함된 배열에서 유효한 클립(dummyClipA) 선택 확인");

                AudioClip[] clipPool = new AudioClip[] { dummyClipA, dummyClipB, dummyClipHigh };
                AudioSource randomClipSrc = sm.PlayRandomSfx(clipPool, 0.9f);
                Assert(randomClipSrc != null, "PlayRandomSfx 재생 성공 확인");
                Assert(Array.IndexOf(clipPool, randomClipSrc.clip) >= 0, "선택된 클립이 clipPool 목록 중 하나인지 확인");

                AudioSource randomClipAndPitchSrc = sm.PlayRandomSfxWithRandomPitch(clipPool, 0.9f, 0.8f, 1.2f);
                Assert(randomClipAndPitchSrc != null, "PlayRandomSfxWithRandomPitch 재생 성공 확인");
                Assert(Array.IndexOf(clipPool, randomClipAndPitchSrc.clip) >= 0, "복합 재생에서 선택된 클립이 clipPool 목록 중 하나인지 확인");
                Assert(randomClipAndPitchSrc.pitch >= 0.8f && randomClipAndPitchSrc.pitch <= 1.2f, "복합 재생 피치 범위 일치 확인");

                AudioSource randomClipAndPitch3DSrc = sm.PlayRandomSfxWithRandomPitchAt(clipPool, pos3D, 0.9f, 0.8f, 1.2f);
                Assert(randomClipAndPitch3DSrc != null, "PlayRandomSfxWithRandomPitchAt 3D 재생 성공 확인");
                Assert(randomClipAndPitch3DSrc.transform.position == pos3D, "3D 복합 재생 위치 일치 확인");

                sm.StopAllSfx();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(soundManagerObj);
            }

            Assert(SoundManager.Instance == null, "SoundManager 파괴 후 Instance 정적 참조 null 정리 확인");

            string failSummary = failedTests.Count > 0 ? $" (실패목록: {string.Join(", ", failedTests)})" : "";
            Debug.Log($"==== [사운드매니저 검증 테스트 완료] 통과: {passed}개, 실패: {failed}개{failSummary} ====");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[사운드매니저 검증 중 예외 발생] {ex}");
            failed++;
        }
    }
}
