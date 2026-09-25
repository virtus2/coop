#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// GunItemData / ItemData의 손 부착 오프셋(Held Position, Rotation, Scale)을
/// 시각적으로 편집할 수 있는 전용 씬(Assets/Scenes/Editor/HeldPositionEditScene.unity)을 생성하고 여는 에디터 툴입니다.
/// </summary>
public static class HeldPositionEditSceneSetup
{
    public const string SCENE_DIR = "Assets/Scenes/Editor";
    public const string SCENE_PATH = "Assets/Scenes/Editor/HeldPositionEditScene.unity";

    [MenuItem("Tools/Item Hold Offset/Open Held Position Edit Scene", priority = 0)]
    public static void OpenOrCreateEditScene()
    {
        if (File.Exists(SCENE_PATH))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(SCENE_PATH);
                FocusOnTweaker();
            }
        }
        else
        {
            CreateHeldPositionEditScene();
        }
    }

    public static string lastError = "None";

    [MenuItem("Tools/Item Hold Offset/Recreate Held Position Edit Scene", priority = 1)]
    public static string CreateAndReport()
    {
        lastError = "None";
        CreateHeldPositionEditScene();
        return File.Exists(SCENE_PATH) ? "Success: " + SCENE_PATH : "Failed: " + lastError;
    }

    public static void CreateHeldPositionEditScene()
    {
        try
        {
            if (!Directory.Exists(SCENE_DIR))
            {
                Directory.CreateDirectory(SCENE_DIR);
            }

            Debug.Log("[HeldPositionEditSceneSetup] 1. 대상 씬 결정");
            var activeScene = EditorSceneManager.GetActiveScene();
            UnityEngine.SceneManagement.Scene targetScene;

            if (string.IsNullOrEmpty(activeScene.path))
            {
                // 현재 씬이 Untitled (빈 씬) 상태인 경우 현재 씬을 그대로 활용
                targetScene = activeScene;
                // 기존 루트 오브젝트가 있다면 정리
                foreach (var rootGo in targetScene.GetRootGameObjects())
                {
                    Object.DestroyImmediate(rootGo);
                }
            }
            else
            {
                targetScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }

            Debug.Log("[HeldPositionEditSceneSetup] 2. Directional Light 생성");
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, targetScene);

            Debug.Log("[HeldPositionEditSceneSetup] 3. Floor 생성");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, targetScene);

            Debug.Log("[HeldPositionEditSceneSetup] 4. Player 인스턴스화");
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerDummyPrefab.prefab");
            GameObject playerInstance = null;
            if (playerPrefab != null)
            {
                playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, targetScene);
                playerInstance.name = "Player_HeldRig";
                playerInstance.transform.position = Vector3.zero;
                playerInstance.transform.rotation = Quaternion.identity;
            }
            else
            {
                playerInstance = new GameObject("Player_HeldRig");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerInstance, targetScene);
            }

            Debug.Log("[HeldPositionEditSceneSetup] 5. HoldPoint 탐색");
            Transform holdPoint = FindHoldPoint(playerInstance.transform);
            if (holdPoint == null)
            {
                GameObject hpGo = new GameObject("HoldPoint");
                hpGo.transform.SetParent(playerInstance.transform);
                hpGo.transform.localPosition = new Vector3(0.32f, -0.25f, 0.65f);
                holdPoint = hpGo.transform;
            }

            Debug.Log("[HeldPositionEditSceneSetup] 6. 카메라 생성");
            Transform cameraTarget = playerInstance.transform.Find("CameraTarget");
            GameObject camGo = new GameObject("FirstPersonCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();

            if (camGo.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                camGo.AddComponent<UniversalAdditionalCameraData>();
            }

            if (cameraTarget != null)
            {
                camGo.transform.SetParent(cameraTarget, false);
                camGo.transform.localPosition = Vector3.zero;
                camGo.transform.localRotation = Quaternion.identity;
            }
            else
            {
                camGo.transform.SetParent(playerInstance.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camGo.transform.localRotation = Quaternion.identity;
            }

            Debug.Log("[HeldPositionEditSceneSetup] 7. Tweaker 부착");
            ItemHoldOffsetTweaker tweaker = playerInstance.AddComponent<ItemHoldOffsetTweaker>();
            tweaker.HoldPoint = holdPoint;
            tweaker.PreviewCamera = cam;

            ItemData sampleGun = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/ItemData/Gun_TacticalPistol.asset");
            if (sampleGun != null)
            {
                tweaker.TargetItemData = sampleGun;
                tweaker.RefreshPreview(true);
            }

            Debug.Log("[HeldPositionEditSceneSetup] 8. 씬 저장");
            bool saved = EditorSceneManager.SaveScene(targetScene, SCENE_PATH);
            AssetDatabase.Refresh();

            Debug.Log($"<color=#4CAF50><b>[HeldPositionEditSceneSetup] 전용 편집 씬 저장 결과({saved}): {SCENE_PATH}</b></color>");

            FocusOnTweaker();
        }
        catch (System.Exception ex)
        {
            lastError = ex.ToString();
            Debug.LogError($"[HeldPositionEditSceneSetup] 씬 생성 실패: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void FocusOnTweaker()
    {
        var tweaker = Object.FindFirstObjectByType<ItemHoldOffsetTweaker>();
        if (tweaker != null)
        {
            Selection.activeGameObject = tweaker.gameObject;
            if (tweaker.PreviewInstance != null)
            {
                Selection.activeGameObject = tweaker.PreviewInstance;
            }

            try
            {
                if (SceneView.lastActiveSceneView != null && tweaker.PreviewCamera != null)
                {
                    SceneView.lastActiveSceneView.AlignViewToObject(tweaker.PreviewCamera.transform);
                }
            }
            catch
            {
                // Headless 환경이나 SceneView 비활성화 상태에서는 무시
            }
        }
    }

    private static Transform FindHoldPoint(Transform current)
    {
        if (current == null) return null;
        if (current.name == "HoldPoint") return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindHoldPoint(current.GetChild(i));
            if (found != null) return found;
        }

        return null;
    }
}
#endif
