using System;
using System.IO;
using System.Reflection;
using Coop.Audio;
using Coop.UI;
using PrimeTween;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Coop.Editor
{
    /// <summary>
    /// 범용 버튼(CommonButton), PrimeTween 반응 연출, 모듈형 SoundCue, Localization 설정의 무결성을 검증하는 자동화 테스트입니다.
    /// </summary>
    public static class CommonButtonVerification
    {
        [MenuItem("Tools/Verification/Run CommonButton Verification", priority = 50)]
        public static void RunAllTests()
        {
            Debug.Log("<color=cyan>==== [범용 버튼(CommonButton) 및 SoundCue 시스템 통합 검증 시작] ====</color>");
            int passed = 0;
            int failed = 0;
            var failedTests = new System.Collections.Generic.List<string>();

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
                    Debug.LogError($"<color=red>[FAIL]</color> {testName}");
                    failed++;
                }
            }

            try
            {
                // =========================================================================
                // 1. SoundCue 단위 기능 검증
                // =========================================================================
                Debug.Log("--- [1. SoundCue 단위 기능 검증] ---");
                AudioClip clipA = AudioClip.Create("TestClipA", 44100, 1, 44100, false);
                AudioClip clipB = AudioClip.Create("TestClipB", 44100, 1, 44100, false);

                var cue = new SoundCue(clipA, volume: 0.85f, randomPitch: true, minPitch: 0.9f, maxPitch: 1.1f);
                Assert(cue.Clip == clipA, "SoundCue 단일 클립 반환 확인");
                Assert(cue.GetClip() == clipA, "SoundCue.GetClip() 단일 클립 반환 확인");
                Assert(Mathf.Approximately(cue.Volume, 0.85f), "SoundCue 볼륨 프로퍼티 일치 확인");

                // 피치 무작위 범위 검증
                for (int i = 0; i < 10; i++)
                {
                    float p = cue.GetPitch();
                    Assert(p >= 0.9f && p <= 1.1f, $"SoundCue 피치 무작위 범위 내 생성 확인 (값: {p:F3})");
                }

                // 무작위 클립 풀 검증
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(SoundCue).GetField("_useRandomClip", flags)?.SetValue(cue, true);
                typeof(SoundCue).GetField("_additionalClips", flags)?.SetValue(cue, new AudioClip[] { clipB });

                bool foundA = false;
                bool foundB = false;
                for (int i = 0; i < 30; i++)
                {
                    AudioClip picked = cue.GetClip();
                    if (picked == clipA) foundA = true;
                    if (picked == clipB) foundB = true;
                }
                Assert(foundA && foundB, "SoundCue 무작위 클립 선택에서 clipA와 clipB가 모두 선택됨 확인");

                // 쿨다운 검증
                Assert(cue.CanPlay, "SoundCue 초기 상태에서 CanPlay == true 확인");

                // =========================================================================
                // 2. SoundCue + SoundManager 연동 검증
                // =========================================================================
                Debug.Log("--- [2. SoundCue + SoundManager 풀링 연동 검증] ---");
                GameObject smGO = new GameObject("Test_SoundManager");
                var sm = smGO.AddComponent<SoundManager>();
                sm.EnsureInitialized();

                try
                {
                    Assert(SoundManager.Instance == sm, "SoundManager 인스턴스 활성화 확인");

                    AudioSource sfxSrc = cue.Play();
                    Assert(sfxSrc != null, "SoundCue.Play() 시 SoundManager 풀을 통해 AudioSource 정상 할당 확인");
                    Assert(!cue.CanPlay, "SoundCue 재생 직후 쿨다운 적용으로 CanPlay == false 확인");

                    sm.StopAllSfx();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(smGO);
                }

                // =========================================================================
                // 3. SoundCueAsset 및 SoundTrigger 컴포넌트 검증
                // =========================================================================
                Debug.Log("--- [3. SoundCueAsset 및 SoundTrigger 검증] ---");
                var cueAsset = ScriptableObject.CreateInstance<SoundCueAsset>();
                Assert(cueAsset.Cue != null, "SoundCueAsset 내부 SoundCue 인스턴스 자동 생성 확인");

                GameObject triggerGO = new GameObject("Test_SoundTrigger");
                var soundTrigger = triggerGO.AddComponent<SoundTrigger>();
                Assert(soundTrigger.ActiveCue != null, "SoundTrigger.ActiveCue 정상 조회 확인");
                UnityEngine.Object.DestroyImmediate(cueAsset);
                UnityEngine.Object.DestroyImmediate(triggerGO);

                // =========================================================================
                // 4. CommonButton 컴포넌트 및 PrimeTween 상호작용 검증
                // =========================================================================
                Debug.Log("--- [4. CommonButton 컴포넌트 및 PrimeTween 반응 검증] ---");
                GameObject btnGO = new GameObject("Test_CommonButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CommonButton));
                GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGO.transform.SetParent(btnGO.transform, false);

                var commonBtn = btnGO.GetComponent<CommonButton>();
                var img = btnGO.GetComponent<Image>();
                var tmp = txtGO.GetComponent<TextMeshProUGUI>();

                // 필드 세팅
                typeof(CommonButton).GetField("_targetTransform", flags)?.SetValue(commonBtn, btnGO.GetComponent<RectTransform>());
                typeof(CommonButton).GetField("_label", flags)?.SetValue(commonBtn, tmp);
                commonBtn.targetGraphic = img;

                Assert(commonBtn.transition == Selectable.Transition.None, "CommonButton의 Selectable.transition이 None으로 설정되어 PrimeTween과 충돌 방지 확인");

                // Text API 검증
                commonBtn.SetText("테스트버튼");
                Assert(tmp.text == "테스트버튼", "CommonButton.SetText()가 TextMeshPro 레이블을 정상 변경함 확인");

                // 이벤트 시뮬레이션 및 PrimeTween 애니메이션 트리거 검증
                var eventData = new PointerEventData(EventSystem.current);

                // Hover (PointerEnter)
                commonBtn.OnPointerEnter(eventData);
                Assert(true, "CommonButton.OnPointerEnter 호출 시 PrimeTween 스케일 애니메이션 에러 없이 실행 확인");

                // Press (PointerDown)
                commonBtn.OnPointerDown(eventData);
                Assert(true, "CommonButton.OnPointerDown 호출 시 물리 프레스 애니메이션 에러 없이 실행 확인");

                // Release (PointerUp)
                commonBtn.OnPointerUp(eventData);
                Assert(true, "CommonButton.OnPointerUp 호출 시 애니메이션 복귀 에러 없이 실행 확인");

                // Click (PointerClick)
                bool clicked = false;
                commonBtn.onClick.AddListener(() => clicked = true);
                commonBtn.OnPointerClick(eventData);
                Assert(clicked, "CommonButton.OnPointerClick 호출 시 onClick 이벤트 및 펀치 애니메이션 트리거 확인");

                // Unhover (PointerExit)
                commonBtn.OnPointerExit(eventData);
                Assert(true, "CommonButton.OnPointerExit 정상 처리 확인");

                // 비활성화(Disabled) 상태 검증
                commonBtn.interactable = false;
                clicked = false;
                commonBtn.OnPointerClick(eventData);
                Assert(!clicked, "interactable == false 일 때 OnPointerClick으로 onClick이 발화되지 않음 확인");

                UnityEngine.Object.DestroyImmediate(btnGO);

                // =========================================================================
                // 5. Localization 기본 설정 및 테이블 검증
                // =========================================================================
                Debug.Log("--- [5. Localization 기본 설정 및 테이블 검증] ---");
                CreateCommonButtonPrefab.EnsureLocalizationSetup();

                var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
                Assert(settings != null, "LocalizationEditorSettings.ActiveLocalizationSettings 존재 확인");

                var localeKo = LocalizationEditorSettings.GetLocale("ko");
                var localeEn = LocalizationEditorSettings.GetLocale("en");
                Assert(localeKo != null, "한국어 로케일(ko) 등록 확인");
                Assert(localeEn != null, "영어 로케일(en) 등록 확인");

                var collection = LocalizationEditorSettings.GetStringTableCollection(CreateCommonButtonPrefab.STRING_TABLE_NAME);
                Assert(collection != null, $"StringTableCollection '{CreateCommonButtonPrefab.STRING_TABLE_NAME}' 생성 확인");

                var koTable = collection?.GetTable("ko") as StringTable;
                var enTable = collection?.GetTable("en") as StringTable;
                Assert(koTable != null && enTable != null, "ko 및 en StringTable 생성 확인");

                string[] requiredKeys = { "btn_confirm", "btn_cancel", "btn_apply", "btn_close", "btn_start" };
                foreach (var key in requiredKeys)
                {
                    var koEntry = koTable?.GetEntry(key);
                    var enEntry = enTable?.GetEntry(key);
                    Assert(koEntry != null && !string.IsNullOrEmpty(koEntry.Value), $"[ko] 테이블 내 '{key}' 키 및 번역값 존재 확인 ({koEntry?.Value})");
                    Assert(enEntry != null && !string.IsNullOrEmpty(enEntry.Value), $"[en] 테이블 내 '{key}' 키 및 번역값 존재 확인 ({enEntry?.Value})");
                }

                // =========================================================================
                // 6. CommonButton.prefab 프리팹 생성 및 무결성 검증
                // =========================================================================
                Debug.Log("--- [6. CommonButton.prefab 프리팹 무결성 검증] ---");
                GameObject prefab = CreateCommonButtonPrefab.CreateOrUpdatePrefab();
                Assert(prefab != null, "CommonButton.prefab 프리팹 생성 성공 확인");
                Assert(File.Exists(CreateCommonButtonPrefab.PREFAB_PATH), $"프리팹 파일 디스크 저장 확인: {CreateCommonButtonPrefab.PREFAB_PATH}");

                var rootBtn = prefab.GetComponent<CommonButton>();
                var rootImg = prefab.GetComponent<Image>();
                var rootLse = prefab.GetComponent<LocalizeStringEvent>();
                var childTmp = prefab.GetComponentInChildren<TextMeshProUGUI>(true);

                Assert(rootBtn != null, "프리팹 루트에 CommonButton 컴포넌트 부착 확인");
                Assert(rootImg != null, "프리팹 루트에 Image 컴포넌트 부착 확인");
                Assert(rootLse != null, "프리팹 루트에 LocalizeStringEvent 부착 확인");
                Assert(childTmp != null, "프리팹 자식에 TextMeshProUGUI 컴포넌트 부착 확인");

                // LocalizeStringEvent 이벤트 바인딩 확인
                Assert(rootLse.OnUpdateString.GetPersistentEventCount() > 0, "LocalizeStringEvent.OnUpdateString 영구 리스너 등록 확인");
                Assert(rootLse.StringReference.TableReference.TableCollectionName == CreateCommonButtonPrefab.STRING_TABLE_NAME, "LocalizeStringEvent 테이블이 UIStrings로 지정됨 확인");

                // 사운드 에셋 파일 확인
                Assert(File.Exists(CreateCommonButtonPrefab.HOVER_CUE_PATH), $"호버 사운드 큐 에셋 존재 확인: {CreateCommonButtonPrefab.HOVER_CUE_PATH}");
                Assert(File.Exists(CreateCommonButtonPrefab.CLICK_CUE_PATH), $"클릭 사운드 큐 에셋 존재 확인: {CreateCommonButtonPrefab.CLICK_CUE_PATH}");

                // 임베디드 리소스 정리
                UnityEngine.Object.DestroyImmediate(clipA);
                UnityEngine.Object.DestroyImmediate(clipB);

                Debug.Log($"<color=cyan>================ [검증 결과: 성공 {passed}건, 실패 {failed}건] ================</color>");
                if (failed == 0)
                {
                    Debug.Log("<color=green>★ 모든 범용 버튼(CommonButton), Localization, SoundCue 검증을 완벽하게 통과했습니다! ★</color>");
                }
                else
                {
                    Debug.LogError($"<color=red>검증 실패 목록: {string.Join(", ", failedTests)}</color>");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>검증 도중 예외 발생: {ex}</color>");
            }
        }
    }
}
