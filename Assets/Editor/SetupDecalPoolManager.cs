using System.IO;
using Coop.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.EditorScripts
{
    /// <summary>
    /// DecalPoolManager 프리팹을 생성하고 MainScene에 안전하게 배치하는 에디터 유틸리티입니다.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupDecalPoolManager
    {
        public const string PREFAB_PATH = "Assets/Prefabs/DecalPoolManager.prefab";
        public const string MAIN_SCENE_PATH = "Assets/Scenes/MainScene.unity";
        public const string BULLET_HOLE_MAT_PATH = "Assets/FreeSurfaceDecals5Pack/Materials/URP Projector/M_DecalProjector_BulletHole.mat";

        static SetupDecalPoolManager()
        {
            EditorApplication.delayCall += AutoSetupOnCompile;
        }

        private static void AutoSetupOnCompile()
        {
            if (!File.Exists(PREFAB_PATH))
            {
                ExecuteSetup();
            }
            else
            {
                EnsurePlacedInMainScene();
            }
        }

        [MenuItem("Tools/Coop/Setup DecalPoolManager in MainScene")]
        public static void ExecuteSetup()
        {
            GameObject prefab = CreateOrUpdatePrefab();
            if (prefab == null)
            {
                Debug.LogError("[SetupDecalPoolManager] DecalPoolManager 프리팹 생성 실패!");
                return;
            }

            PlaceInMainScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[SetupDecalPoolManager] DecalPoolManager 프리팹 생성 및 MainScene 배치 완료!</color>");
        }

        public static GameObject CreateOrUpdatePrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            GameObject go = new GameObject("[DecalPoolManager]");
            DecalPoolManager manager = go.AddComponent<DecalPoolManager>();

            Material bulletMat = AssetDatabase.LoadAssetAtPath<Material>(BULLET_HOLE_MAT_PATH);
            if (bulletMat != null)
            {
                manager.SetDefaultMaterial(bulletMat);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PREFAB_PATH);
            Object.DestroyImmediate(go);
            return prefab;
        }

        public static void EnsurePlacedInMainScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                prefab = CreateOrUpdatePrefab();
            }

            PlaceInMainScene(prefab);
        }

        public static void PlaceInMainScene(GameObject prefab)
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            bool needToClose = false;

            if (activeScene.path != MAIN_SCENE_PATH)
            {
                activeScene = EditorSceneManager.OpenScene(MAIN_SCENE_PATH, OpenSceneMode.Single);
                needToClose = true;
            }

            GameObject existing = GameObject.Find("[DecalPoolManager]");
            if (existing == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeScene);
                instance.name = "[DecalPoolManager]";
                Undo.RegisterCreatedObjectUndo(instance, "Create DecalPoolManager in MainScene");

                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[SetupDecalPoolManager] {MAIN_SCENE_PATH}에 [DecalPoolManager] 배치 및 저장 완료.");
            }
        }
    }
}
