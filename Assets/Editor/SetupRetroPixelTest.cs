using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 로우폴리 + 픽셀아트 텍스처 스타일을 즉시 테스트할 수 있는 전용 씬과 에셋을 자동 구성하는 에디터 스크립트입니다.
/// </summary>
public static class SetupRetroPixelTest
{
    private const string SCENE_DIR = "Assets/Scenes";
    private const string SCENE_PATH = "Assets/Scenes/RetroPixelTestScene.unity";
    private const string MAT_DIR = "Assets/Materials/Retro";

    [MenuItem("Tools/Retro Art/Setup Retro Pixel Test Scene")]
    public static void ExecuteSetup()
    {
        EnsureDirectories();

        // 1. 머티리얼 생성 및 세팅
        Material floorMat = GetOrCreateLitMaterial(MAT_DIR + "/Retro_StoneFloorMat.mat", new Color(0.18f, 0.2f, 0.24f));
        Material wallMat = GetOrCreateLitMaterial(MAT_DIR + "/Retro_StoneWallMat.mat", new Color(0.24f, 0.26f, 0.3f));
        Material pillarMat = GetOrCreateLitMaterial(MAT_DIR + "/Retro_PillarMat.mat", new Color(0.32f, 0.24f, 0.18f));
        Material pedestalMat = GetOrCreateLitMaterial(MAT_DIR + "/Retro_PedestalMat.mat", new Color(0.12f, 0.13f, 0.16f));
        Material testPropMat = GetOrCreateLitMaterial(MAT_DIR + "/Retro_TestPropMat.mat", new Color(0.95f, 0.65f, 0.2f));
        Material torchHeadMat = GetOrCreateLitMaterial(MAT_DIR + "/Retro_TorchFlameMat.mat", new Color(1.0f, 0.6f, 0.1f));

        // 2. 새로운 테스트 씬 생성
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 3. 라이팅 & 환경 설정 (어두운 던전 앰비언트 + 은은한 달빛)
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.1f);
        RenderSettings.subtractiveShadowColor = new Color(0.02f, 0.02f, 0.05f);

        // Directional Light (은은한 푸른 달빛 - 횃불과의 대비 극대화)
        GameObject dirLightGo = new GameObject("Directional Light (Moonlight)");
        Light dirLight = dirLightGo.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.6f, 0.7f, 0.9f);
        dirLight.intensity = 0.35f;
        dirLight.shadows = LightShadows.Hard;
        dirLightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        // 4. 테스트 룸 환경 지오메트리 구축
        GameObject envRoot = new GameObject("--- Environment ---");

        // 바닥
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor (14x14)";
        floor.transform.parent = envRoot.transform;
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(14f, 1f, 14f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // 벽면 (뒤/좌/우)
        CreateWall(envRoot.transform, "Wall_Back", new Vector3(0f, 3f, 7f), new Vector3(14f, 6f, 1f), wallMat);
        CreateWall(envRoot.transform, "Wall_Left", new Vector3(-7f, 3f, 0f), new Vector3(1f, 6f, 14f), wallMat);
        CreateWall(envRoot.transform, "Wall_Right", new Vector3(7f, 3f, 0f), new Vector3(1f, 6f, 14f), wallMat);

        // 기둥 4개 및 횃불 조명 (기둥 밖으로 돌출 배치)
        Vector3[] pillarPositions = new Vector3[]
        {
            new Vector3(-4.5f, 2f, 4.5f),
            new Vector3(4.5f, 2f, 4.5f),
            new Vector3(-4.5f, 2f, -4.5f),
            new Vector3(4.5f, 2f, -4.5f)
        };

        for (int i = 0; i < pillarPositions.Length; i++)
        {
            Vector3 pPos = pillarPositions[i];
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"Pillar_{i + 1}";
            pillar.transform.parent = envRoot.transform;
            pillar.transform.position = pPos;
            pillar.transform.localScale = new Vector3(0.8f, 4f, 0.8f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;

            // 기둥 중심에서 방 중앙을 향하는 방향으로 횃불을 기둥 외벽에 배치
            Vector3 dirToCenter = (Vector3.zero - new Vector3(pPos.x, 0f, pPos.z)).normalized;
            Vector3 torchPosition = pPos + dirToCenter * 0.65f + new Vector3(0f, 0.6f, 0f);

            // 횃불 브래킷/시각용 불꽃 큐브
            GameObject torchVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torchVisual.name = $"TorchBracket_{i + 1}";
            torchVisual.transform.parent = pillar.transform;
            torchVisual.transform.position = torchPosition;
            torchVisual.transform.localScale = new Vector3(0.25f, 0.35f, 0.25f);
            torchVisual.GetComponent<MeshRenderer>().sharedMaterial = torchHeadMat;

            // 횃불 포인트 라이트
            GameObject torch = new GameObject($"TorchLight_{i + 1}");
            torch.transform.parent = torchVisual.transform;
            torch.transform.position = torchPosition;

            Light torchLight = torch.AddComponent<Light>();
            torchLight.type = LightType.Point;
            torchLight.color = new Color(1.0f, 0.55f, 0.18f); // 따뜻한 오렌지빛
            torchLight.range = 9.5f;
            torchLight.intensity = 3.2f;
            torchLight.shadows = LightShadows.None;

            torch.AddComponent<RetroTorchFlicker>();
        }

        // 5. 테스트 전시 받침대 (Pedestal) 및 타겟 슬롯
        GameObject testTargetRoot = new GameObject("--- [PUT_YOUR_FBX_HERE] ---");
        testTargetRoot.transform.position = new Vector3(0f, 0f, 0f);

        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = "Pedestal_Base";
        pedestal.transform.parent = testTargetRoot.transform;
        pedestal.transform.position = new Vector3(0f, 0.2f, 0f);
        pedestal.transform.localScale = new Vector3(2.5f, 0.4f, 2.5f);
        pedestal.GetComponent<MeshRenderer>().sharedMaterial = pedestalMat;

        // 사전 테스트용 기본 3D 샘플 프롭들 (큐브, 구체)
        GameObject sampleGroup = new GameObject("Sample_Test_Props");
        sampleGroup.transform.parent = testTargetRoot.transform;
        sampleGroup.transform.position = new Vector3(0f, 0.4f, 0f);

        GameObject sampleCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sampleCube.name = "Sample_Cube";
        sampleCube.transform.parent = sampleGroup.transform;
        sampleCube.transform.position = new Vector3(-0.6f, 0.5f, 0f);
        sampleCube.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
        sampleCube.GetComponent<MeshRenderer>().sharedMaterial = testPropMat;

        GameObject sampleSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sampleSphere.name = "Sample_Sphere";
        sampleSphere.transform.parent = sampleGroup.transform;
        sampleSphere.transform.position = new Vector3(0.6f, 0.5f, 0f);
        sampleSphere.GetComponent<MeshRenderer>().sharedMaterial = testPropMat;

        // 6. 메인 레트로 픽셀 카메라 생성 및 세팅 (순수 URP 네이티브)
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 1.8f, -3.8f);
        camGo.transform.rotation = Quaternion.Euler(14f, 0f, 0f);

        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        camGo.AddComponent<AudioListener>();

        // RetroPixelCamera 컴포넌트 부착
        RetroPixelCamera retroCam = camGo.AddComponent<RetroPixelCamera>();
        SerializedObject so = new SerializedObject(retroCam);
        so.FindProperty("_preset").enumValueIndex = (int)RetroPixelCamera.RetroResolutionPreset.Preset270p_480x270;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 7. 씬 저장 및 에디터 열기
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=cyan>[SetupRetroPixelTest]</color> 레트로 픽셀 테스트 씬 생성 완료! 경로: " + SCENE_PATH);
        EditorSceneManager.OpenScene(SCENE_PATH);
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        if (!AssetDatabase.IsValidFolder(MAT_DIR))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Retro");
        }
    }

    private static Material GetOrCreateLitMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Retro/RetroPixelLit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
