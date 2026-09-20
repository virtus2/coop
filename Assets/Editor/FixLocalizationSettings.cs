using System;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Coop.Editor
{
    public static class FixLocalizationSettings
    {
        [MenuItem("Tools/Localization/Fix Localization Settings", priority = 40)]
        [InitializeOnLoadMethod]
        public static void ExecuteFix()
        {
            try
            {
                string settingsPath = "Assets/Localization/LocalizationSettings.asset";
                
                // 1. 기존 에셋 로드 및 null 체크
                var existingSettings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
                
                // 2. 만약 기존 에셋의 StartupSelectors에 null이 있거나 역직렬화가 깨져 있다면 새로 생성
                bool needsRecreate = false;
                if (existingSettings == null)
                {
                    needsRecreate = true;
                }
                else
                {
                    var selectors = existingSettings.GetStartupLocaleSelectors();
                    if (selectors == null || selectors.Count == 0)
                    {
                        needsRecreate = true;
                    }
                    else
                    {
                        foreach (var s in selectors)
                        {
                            if (s == null)
                            {
                                needsRecreate = true;
                                break;
                            }
                        }
                    }
                    if (existingSettings.GetAvailableLocales() == null)
                    {
                        needsRecreate = true;
                    }
                }

                if (needsRecreate)
                {
                    Debug.Log("<color=yellow>[FixLocalizationSettings] LocalizationSettings.asset이 손상되었거나 null 셀렉터를 포함하여 Unity 네이티브로 재생성합니다.</color>");

                    if (existingSettings != null)
                    {
                        AssetDatabase.DeleteAsset(settingsPath);
                    }

                    // Unity C# 생성자를 통해 유효한 초기값(CommandLine, System, Specific 셀렉터 및 LocalesProvider)을 가진 인스턴스 생성
                    var newSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                    newSettings.name = "LocalizationSettings";

                    // AssetDatabase를 통해 Unity 네이티브 직렬화로 저장
                    AssetDatabase.CreateAsset(newSettings, settingsPath);
                    LocalizationEditorSettings.ActiveLocalizationSettings = newSettings;

                    // 로케일 연결
                    var localeKo = LocalizationEditorSettings.GetLocale("ko");
                    var localeEn = LocalizationEditorSettings.GetLocale("en");

                    if (localeKo != null)
                    {
                        LocalizationEditorSettings.AddLocale(localeKo);
                    }
                    if (localeEn != null)
                    {
                        LocalizationEditorSettings.AddLocale(localeEn);
                    }

                    LocalizationSettings.ProjectLocale = localeKo != null ? localeKo : localeEn;

                    EditorUtility.SetDirty(newSettings);
                    AssetDatabase.SaveAssets();

                    Debug.Log("<color=green>[FixLocalizationSettings] LocalizationSettings.asset이 성공적으로 정상 복구되었습니다!</color>");
                }
                else
                {
                    Debug.Log("<color=green>[FixLocalizationSettings] LocalizationSettings.asset이 이미 정상 상태입니다.</color>");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FixLocalizationSettings] 오류 발생: {ex}");
            }
        }
    }
}
