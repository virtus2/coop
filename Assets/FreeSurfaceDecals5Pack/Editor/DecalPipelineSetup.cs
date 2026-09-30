// DecalPipelineSetup - bind this pack's decal materials to the render
// pipeline the project actually uses.
//
// The decals ship as transparent materials for quads, bound to Standard in
// Fade mode, because that is the one decal route every pipeline has. A
// Unity material names exactly one shader, so in a URP project Standard is
// magenta: this rebinds the same assets, in place, to URP Lit in transparent
// mode, and adds a Shader Graphs/Decal material per decal for URP's Decal
// Projector. Paths and GUIDs are kept, so the demo scene keeps working.
//
// Idempotent: a material already on the wanted shader is left alone, and a
// projector material that exists is only refreshed.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Taproot.Decals
{
    public class DecalPipelineImporter : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                           string[] moved, string[] movedFrom)
        {
            if (imported.Any(p => p.EndsWith(".mat")))
                DecalPipelineSetup.Run(false);
        }
    }

    [InitializeOnLoad]
    public static class DecalPipelineSetup
    {
        static DecalPipelineSetup()
        {
            EditorApplication.delayCall += () => Run(false);
        }

        [MenuItem("Tools/Taproot/Rebind Decals To Render Pipeline")]
        static void RunFromMenu() { Run(true); }

        static bool IsUrp()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            return rp != null && rp.GetType().Name.Contains("Universal");
        }

        static string PackRoot()
        {
            foreach (var g in AssetDatabase.FindAssets("DecalPipelineSetup t:MonoScript"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith("/DecalPipelineSetup.cs"))
                    return Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/');
            }
            return null;
        }

        static Texture2D Tex(string root, string stem, string suffix)
        {
            string name = stem.StartsWith("M_Decal_") ? stem.Substring(8) : stem;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(
                string.Format("{0}/Textures/T_Decal_{1}_{2}.png", root, name, suffix));
        }

        public static void Run(bool verbose)
        {
            string root = PackRoot();
            if (string.IsNullOrEmpty(root)) return;
            bool urp = IsUrp();
            string want = urp ? "Universal Render Pipeline/Lit" : "Standard";
            var shader = Shader.Find(want);
            if (shader == null) return;

            var quads = AssetDatabase.FindAssets("t:Material", new[] { root + "/Materials" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).StartsWith("M_Decal_")).ToList();

            int changed = 0, projectors = 0;
            foreach (var p in quads)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat == null) continue;
                string stem = Path.GetFileNameWithoutExtension(p);
                if (mat.shader == null || mat.shader.name != want)
                {
                    mat.shader = shader;
                    BindQuad(mat, urp, root, stem);
                    EditorUtility.SetDirty(mat);
                    changed++;
                }
                if (urp && MakeProjector(root, stem)) projectors++;
            }
            changed += RebindDemo(root, urp);
            if (changed + projectors > 0) AssetDatabase.SaveAssets();
            if (verbose || changed + projectors > 0)
                Debug.Log(string.Format("[Taproot] decals: {0} rebound to {1}, {2} projector materials",
                                        changed, want, projectors));
        }

        public static void BindQuad(Material mat, bool urp, string root, string stem)
        {
            var albedo = Tex(root, stem, "Albedo");
            var normal = Tex(root, stem, "Normal");
            var ms = Tex(root, stem, "MetallicSmoothness");
            if (urp)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_BumpMap", normal);
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_Surface", 1f);       // transparent
                mat.SetFloat("_Blend", 0f);         // alpha
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                // URP keeps a transparent surface's specular by default, which
                // drew a glossy square round every decal where alpha is zero
                mat.SetFloat("_BlendModePreserveSpecular", 0f);
                mat.SetFloat("_AlphaToMask", 0f);
                foreach (var k in new[] { "_ALPHABLEND_ON", "_METALLICGLOSSMAP", "_ALPHAPREMULTIPLY_ON" })
                    mat.DisableKeyword(k);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                // Setting the floats is not enough: the first URP build rendered
                // every quad opaque and mirror-smooth, because URP derives its
                // blend state and keywords in its own editor code. Call that code
                // by reflection, so this file still compiles without URP.
                UrpSetup(mat);
            }
            else
            {
                mat.SetTexture("_MainTex", albedo);
                mat.SetTexture("_BumpMap", normal);
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_Mode", 2f);          // Fade
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_GlossMapScale", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        static void UrpSetup(Material mat)
        {
            foreach (var typeName in new[] {
                "UnityEditor.Rendering.Universal.ShaderGUI.LitGUI, Unity.RenderPipelines.Universal.Editor",
                "UnityEditor.Rendering.Universal.ShaderGUI.BaseShaderGUI, Unity.RenderPipelines.Universal.Editor" })
            {
                var t = Type.GetType(typeName);
                if (t == null) continue;
                foreach (var name in new[] { "UpdateMaterialSurfaceOptions", "SetupMaterialBlendMode", "SetMaterialKeywords" })
                {
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                       .Where(x => x.Name == name))
                    {
                        var ps = m.GetParameters();
                        try
                        {
                            if (ps.Length == 1 && ps[0].ParameterType == typeof(Material))
                                m.Invoke(null, new object[] { mat });
                            else if (ps.Length == 2 && ps[0].ParameterType == typeof(Material) && ps[1].ParameterType == typeof(bool))
                                m.Invoke(null, new object[] { mat, true });
                        }
                        catch (Exception) { }
                    }
                }
            }
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        // The demo's wall and floor ship on Standard too, and were magenta in
        // URP because only the decal materials were being rebound.
        static int RebindDemo(string root, bool urp)
        {
            string want = urp ? "Universal Render Pipeline/Lit" : "Standard";
            var shader = Shader.Find(want);
            if (shader == null || !AssetDatabase.IsValidFolder(root + "/Demo")) return 0;
            int n = 0;
            foreach (var p in AssetDatabase.FindAssets("t:Material", new[] { root + "/Demo" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat == null || (mat.shader != null && mat.shader.name == want)) continue;
                Color c = mat.HasProperty("_Color") ? mat.GetColor("_Color")
                        : mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.grey;
                mat.shader = shader;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.2f);
                EditorUtility.SetDirty(mat);
                n++;
            }
            return n;
        }

        static bool MakeProjector(string root, string stem)
        {
            var shader = Shader.Find("Shader Graphs/Decal");
            if (shader == null) return false;
            string dir = root + "/Materials/URP Projector";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(root + "/Materials", "URP Projector");
            string path = dir + "/" + stem.Replace("M_Decal_", "M_DecalProjector_") + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("Base_Map", Tex(root, stem, "Albedo"));
            mat.SetTexture("Normal_Map", Tex(root, stem, "Normal"));
            mat.SetFloat("Normal_Blend", 0.5f);
            EditorUtility.SetDirty(mat);
            return created;
        }
    }
}
