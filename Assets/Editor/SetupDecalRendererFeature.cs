using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Coop.Editor
{
    /// <summary>
    /// URP Renderer 에셋(PC_Renderer, Mobile_Renderer)에 DecalRendererFeature가 누락되지 않도록
    /// 자동으로 검사하고 등록하는 에디터 유틸리티입니다.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupDecalRendererFeature
    {
        private static readonly string[] RendererAssetPaths = new string[]
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        static SetupDecalRendererFeature()
        {
            EditorApplication.delayCall += EnsureFeatureSetup;
        }

        [MenuItem("Tools/Coop/Setup Decal Renderer Feature")]
        public static void EnsureFeatureSetup()
        {
            bool modifiedAny = false;

            for (int i = 0; i < RendererAssetPaths.Length; i++)
            {
                string path = RendererAssetPaths[i];
                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData == null)
                {
                    continue;
                }

                bool alreadyExists = false;
                for (int f = 0; f < rendererData.rendererFeatures.Count; f++)
                {
                    if (rendererData.rendererFeatures[f] is DecalRendererFeature)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                    feature.name = "DecalRendererFeature";
                    AssetDatabase.AddObjectToAsset(feature, rendererData);
                    rendererData.rendererFeatures.Add(feature);

                    MethodInfo updateMap = typeof(ScriptableRendererData).GetMethod("UpdateMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    updateMap?.Invoke(rendererData, null);

                    EditorUtility.SetDirty(rendererData);
                    modifiedAny = true;
                    Debug.Log($"<color=cyan>[SetupDecalRendererFeature]</color> {path}에 DecalRendererFeature 추가 완료!");
                }
            }

            if (modifiedAny)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
