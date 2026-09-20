using System;
using System.IO;
using Coop.Audio;
using Coop.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Coop.Editor
{
    public static class CreateCommonButtonPrefab
    {
        public const string PREFAB_DIR = "Assets/Prefabs/UI";
        public const string PREFAB_PATH = "Assets/Prefabs/UI/CommonButton.prefab";
        public const string AUDIO_CUES_DIR = "Assets/Audio/Cues";
        public const string HOVER_CUE_PATH = "Assets/Audio/Cues/Btn_Hover_Cue.asset";
        public const string CLICK_CUE_PATH = "Assets/Audio/Cues/Btn_Click_Cue.asset";
        public const string LOCALIZATION_DIR = "Assets/Localization";
        public const string STRING_TABLE_NAME = "UIStrings";

        [InitializeOnLoadMethod]
        private static void AutoInitialize()
        {
            if (!File.Exists(PREFAB_PATH))
            {
                Debug.Log("[CreateCommonButtonPrefab] CommonButton.prefab이 존재하지 않아 자동으로 생성 및 설정을 진행합니다.");
                CreateOrUpdatePrefab();
            }
        }

        [MenuItem("Tools/UI/Create or Update Common Button Prefab", priority = 10)]
        public static GameObject CreateOrUpdatePrefab()
        {
            // 1. 다국어 설정 및 기본 테이블 보장
            EnsureLocalizationSetup();

            // 2. 기본 SoundCueAsset 생성 및 보장
            var hoverAsset = EnsureSoundCueAsset(HOVER_CUE_PATH, volume: 0.8f, minPitch: 0.95f, maxPitch: 1.05f);
            var clickAsset = EnsureSoundCueAsset(CLICK_CUE_PATH, volume: 1.0f, minPitch: 0.98f, maxPitch: 1.02f);

            // 3. Prefabs/UI 디렉토리 생성
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }

            // 4. 버튼 GameObject 계층 구조 생성
            var buttonGO = new GameObject("CommonButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CommonButton), typeof(LocalizeStringEvent));
            var rootRect = buttonGO.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(240f, 60f);

            // 5. Background Image 설정
            var img = buttonGO.GetComponent<Image>();
            img.color = new Color(0.16f, 0.19f, 0.24f, 1f); // 모던 다크 슬레이트 블루
            img.raycastTarget = true;

            // 6. TextMeshProUGUI 자식 생성
            var textGO = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(buttonGO.transform, false);

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);

            var tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "확인";
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            // 기본 TMP 폰트 설정
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            // 7. LocalizeStringEvent 바인딩
            var localizeEvent = buttonGO.GetComponent<LocalizeStringEvent>();
            localizeEvent.StringReference.SetReference(STRING_TABLE_NAME, "btn_confirm");

            // OnUpdateString 이벤트에 TextMeshProUGUI.set_text 바인딩
            var setMethod = typeof(TMP_Text).GetProperty("text")?.GetSetMethod();
            if (setMethod != null)
            {
                var setTextAction = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), tmp, setMethod);
                UnityEventTools.AddPersistentListener(localizeEvent.OnUpdateString, setTextAction);
                int listenerIndex = localizeEvent.OnUpdateString.GetPersistentEventCount() - 1;
                localizeEvent.OnUpdateString.SetPersistentListenerState(listenerIndex, UnityEventCallState.EditorAndRuntime);
            }

            // 8. CommonButton 컴포넌트 세부 설정
            var commonBtn = buttonGO.GetComponent<CommonButton>();
            commonBtn.transition = Selectable.Transition.None;
            commonBtn.targetGraphic = img;

            // 리플렉션을 통해 직렬화 필드 완벽 바인딩
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(CommonButton).GetField("_targetTransform", flags)?.SetValue(commonBtn, rootRect);
            typeof(CommonButton).GetField("_label", flags)?.SetValue(commonBtn, tmp);
            typeof(CommonButton).GetField("_localizeStringEvent", flags)?.SetValue(commonBtn, localizeEvent);
            typeof(CommonButton).GetField("_hoverSoundAsset", flags)?.SetValue(commonBtn, hoverAsset);
            typeof(CommonButton).GetField("_clickSoundAsset", flags)?.SetValue(commonBtn, clickAsset);

            // 9. 프리팹으로 저장
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(buttonGO, PREFAB_PATH, InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(buttonGO);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[CreateCommonButtonPrefab]</color> 범용 버튼 프리팹이 성공적으로 생성되었습니다: {PREFAB_PATH}");
            return savedPrefab;
        }

        private static SoundCueAsset EnsureSoundCueAsset(string assetPath, float volume, float minPitch, float maxPitch)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
            {
                AssetDatabase.CreateFolder("Assets", "Audio");
            }
            if (!AssetDatabase.IsValidFolder(AUDIO_CUES_DIR))
            {
                AssetDatabase.CreateFolder("Assets/Audio", "Cues");
            }

            var asset = AssetDatabase.LoadAssetAtPath<SoundCueAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SoundCueAsset>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var cue = asset.Cue;
                if (cue != null)
                {
                    typeof(SoundCue).GetField("_volume", flags)?.SetValue(cue, volume);
                    typeof(SoundCue).GetField("_useRandomPitch", flags)?.SetValue(cue, true);
                    typeof(SoundCue).GetField("_minPitch", flags)?.SetValue(cue, minPitch);
                    typeof(SoundCue).GetField("_maxPitch", flags)?.SetValue(cue, maxPitch);
                    typeof(SoundCue).GetField("_cooldown", flags)?.SetValue(cue, 0.05f);
                }
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            return asset;
        }

        [MenuItem("GameObject/UI/Common Button (Juicy)", priority = 20)]
        public static void CreateButtonInHierarchy(MenuCommand menuCommand)
        {
            // 프리팹 로드 (없으면 자동 생성)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                prefab = CreateOrUpdatePrefab();
            }

            // 부모 오브젝트 결정 (선택된 객체 또는 씬 내 Canvas)
            GameObject parentGO = menuCommand.context as GameObject;
            if (parentGO == null || parentGO.GetComponentInParent<Canvas>() == null)
            {
                var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    var c = canvasGO.GetComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvas = c;
                    Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
                }
                parentGO = canvas.gameObject;
            }

            // 인스턴스화
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parentGO.transform);
            instance.name = "CommonButton";
            Undo.RegisterCreatedObjectUndo(instance, "Create Common Button");
            Selection.activeGameObject = instance;
        }

        [MenuItem("Tools/Localization/Setup Basic UIStrings Table", priority = 30)]
        public static void EnsureLocalizationSetup()
        {
            // 1. Localization 폴더 확인
            if (!AssetDatabase.IsValidFolder(LOCALIZATION_DIR))
            {
                AssetDatabase.CreateFolder("Assets", "Localization");
            }

            // 2. LocalizationSettings 에셋 확인 또는 생성
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                string settingsPath = $"{LOCALIZATION_DIR}/LocalizationSettings.asset";
                settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                    AssetDatabase.CreateAsset(settings, settingsPath);
                }
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            // 3. 로케일 등록 (ko, en)
            EnsureLocale(settings, "ko", "Korean (ko)");
            EnsureLocale(settings, "en", "English (en)");

            // 4. UIStrings StringTableCollection 확인 또는 생성
            var collection = LocalizationEditorSettings.GetStringTableCollection(STRING_TABLE_NAME);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(STRING_TABLE_NAME, LOCALIZATION_DIR);
            }

            // 5. 기본 버튼 문자열 데이터 주입
            AddStringEntry(collection, "btn_confirm", "확인", "Confirm");
            AddStringEntry(collection, "btn_cancel", "취소", "Cancel");
            AddStringEntry(collection, "btn_apply", "적용", "Apply");
            AddStringEntry(collection, "btn_close", "닫기", "Close");
            AddStringEntry(collection, "btn_start", "시작", "Start");

            // 6. 저장 및 알림
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            foreach (var table in collection.StringTables)
            {
                EditorUtility.SetDirty(table);
            }
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureLocale(LocalizationSettings settings, string code, string localeName)
        {
            var locale = LocalizationEditorSettings.GetLocale(code);
            if (locale == null)
            {
                string localeFolder = $"{LOCALIZATION_DIR}/Locales";
                if (!AssetDatabase.IsValidFolder(localeFolder))
                {
                    AssetDatabase.CreateFolder(LOCALIZATION_DIR, "Locales");
                }

                string localePath = $"{localeFolder}/{code}.asset";
                locale = AssetDatabase.LoadAssetAtPath<Locale>(localePath);
                if (locale == null)
                {
                    locale = Locale.CreateLocale(System.Globalization.CultureInfo.GetCultureInfo(code));
                    locale.name = localeName;
                    AssetDatabase.CreateAsset(locale, localePath);
                }
                LocalizationEditorSettings.AddLocale(locale);
            }
        }

        private static void AddStringEntry(StringTableCollection collection, string key, string koValue, string enValue)
        {
            var sharedEntry = collection.SharedData.GetEntry(key);
            if (sharedEntry == null)
            {
                collection.SharedData.AddKey(key);
            }

            var koTable = collection.GetTable("ko") as StringTable;
            if (koTable != null)
            {
                var entry = koTable.GetEntry(key);
                if (entry == null)
                {
                    koTable.AddEntry(key, koValue);
                }
                else if (string.IsNullOrEmpty(entry.Value))
                {
                    entry.Value = koValue;
                }
            }

            var enTable = collection.GetTable("en") as StringTable;
            if (enTable != null)
            {
                var entry = enTable.GetEntry(key);
                if (entry == null)
                {
                    enTable.AddEntry(key, enValue);
                }
                else if (string.IsNullOrEmpty(entry.Value))
                {
                    entry.Value = enValue;
                }
            }
        }
    }
}
