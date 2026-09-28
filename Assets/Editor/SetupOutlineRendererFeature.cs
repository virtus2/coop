using System.Reflection;
using Coop.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Coop.Editor
{
    [InitializeOnLoad]
    public static class SetupOutlineRendererFeature
    {
        private static readonly string[] RendererAssetPaths = new string[]
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        static SetupOutlineRendererFeature()
        {
            EditorApplication.delayCall += EnsureFeatureSetup;
        }

        [MenuItem("Tools/Coop/Setup Outline Renderer Feature")]
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
                    if (rendererData.rendererFeatures[f] is OutlineRenderFeature)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    var feature = ScriptableObject.CreateInstance<OutlineRenderFeature>();
                    feature.name = "OutlineRenderFeature";
                    AssetDatabase.AddObjectToAsset(feature, rendererData);
                    rendererData.rendererFeatures.Add(feature);

                    MethodInfo updateMap = typeof(ScriptableRendererData).GetMethod("UpdateMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    updateMap?.Invoke(rendererData, null);

                    EditorUtility.SetDirty(rendererData);
                    modifiedAny = true;
                    Debug.Log($"<color=cyan>[SetupOutlineRendererFeature]</color> {path}에 OutlineRenderFeature 추가 완료!");
                }
            }

            if (modifiedAny)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}
