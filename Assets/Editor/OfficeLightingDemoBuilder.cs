using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class OfficeLightingDemoBuilder
{
    [MenuItem("Tools/Build Office Lighting Demo Scene")]
    public static void BuildScene()
    {
        // 1. Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Clear Skybox and Ambient Light (Complete darkness without lights)
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

        // 3. Materials
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        // Floor: Office dark vinyl / carpet tile
        Material floorMat = new Material(litShader) { name = "Mat_Office_Floor" };
        floorMat.SetColor("_BaseColor", new Color(0.24f, 0.26f, 0.28f));
        floorMat.SetFloat("_Smoothness", 0.35f);

        // Wall: 90s institutional off-white / beige
        Material wallMat = new Material(litShader) { name = "Mat_Office_Wall" };
        wallMat.SetColor("_BaseColor", new Color(0.78f, 0.76f, 0.72f));
        wallMat.SetFloat("_Smoothness", 0.15f);

        // Ceiling: Acoustic tile
        Material ceilingMat = new Material(litShader) { name = "Mat_Office_Ceiling" };
        ceilingMat.SetColor("_BaseColor", new Color(0.65f, 0.65f, 0.65f));
        ceilingMat.SetFloat("_Smoothness", 0.05f);

        // Desk Wood Material
        Material deskMat = new Material(litShader) { name = "Mat_Office_Desk" };
        deskMat.SetColor("_BaseColor", new Color(0.52f, 0.38f, 0.26f));
        deskMat.SetFloat("_Smoothness", 0.45f);

        // Metal / Plastic Gray Material (Computers, cabinets)
        Material metalMat = new Material(litShader) { name = "Mat_Office_Metal" };
        metalMat.SetColor("_BaseColor", new Color(0.72f, 0.70f, 0.66f));
        metalMat.SetFloat("_Smoothness", 0.3f);

        // Fluorescent Lamp Tube (Emission HDR)
        Material lampEmissionMat = new Material(litShader) { name = "Mat_Lamp_Emission" };
        lampEmissionMat.EnableKeyword("_EMISSION");
        Color lampHdr = new Color(0.92f, 0.98f, 0.95f) * 4.5f;
        lampEmissionMat.SetColor("_BaseColor", Color.white);
        lampEmissionMat.SetColor("_EmissionColor", lampHdr);

        // Monitor CRT Screen (Green Phosphor Emission HDR)
        Material screenEmissionMat = new Material(litShader) { name = "Mat_Screen_Emission" };
        screenEmissionMat.EnableKeyword("_EMISSION");
        Color screenHdr = new Color(0.15f, 1.0f, 0.45f) * 3.5f;
        screenEmissionMat.SetColor("_BaseColor", new Color(0.05f, 0.2f, 0.1f));
        screenEmissionMat.SetColor("_EmissionColor", screenHdr);

        // Emergency Light Red Glass (Emission HDR)
        Material emergencyMat = new Material(litShader) { name = "Mat_Emergency_Emission" };
        emergencyMat.EnableKeyword("_EMISSION");
        Color emergencyHdr = new Color(1.0f, 0.15f, 0.1f) * 4.5f;
        emergencyMat.SetColor("_BaseColor", Color.red);
        emergencyMat.SetColor("_EmissionColor", emergencyHdr);

        // 4. Room Construction (Enclosed thick box, no light leaks)
        GameObject roomRoot = new GameObject("Office_Room");

        // Dimensions: 8m (X) x 3.2m (Y) x 8m (Z)
        CreateBox("Floor", roomRoot.transform, new Vector3(0, -0.2f, 0), new Vector3(8.8f, 0.4f, 8.8f), floorMat);
        CreateBox("Ceiling", roomRoot.transform, new Vector3(0, 3.4f, 0), new Vector3(8.8f, 0.4f, 8.8f), ceilingMat);
        CreateBox("Wall_Back", roomRoot.transform, new Vector3(0, 1.6f, 4.2f), new Vector3(8.8f, 3.6f, 0.4f), wallMat);
        CreateBox("Wall_Front", roomRoot.transform, new Vector3(0, 1.6f, -4.2f), new Vector3(8.8f, 3.6f, 0.4f), wallMat);
        CreateBox("Wall_Left", roomRoot.transform, new Vector3(-4.2f, 1.6f, 0), new Vector3(0.4f, 3.6f, 8.8f), wallMat);
        CreateBox("Wall_Right", roomRoot.transform, new Vector3(4.2f, 1.6f, 0), new Vector3(0.4f, 3.6f, 8.8f), wallMat);

        // Office Partition Screen (shifted so it frames the scene instead of blocking)
        CreateBox("Office_Partition", roomRoot.transform, new Vector3(1.2f, 0.8f, 0.8f), new Vector3(0.12f, 1.6f, 3.4f), wallMat);

        // 5. Furniture & Desks
        GameObject furnitureRoot = new GameObject("Furniture");
        furnitureRoot.transform.SetParent(roomRoot.transform);

        // Primary Desk Set (Facing toward camera at pleasant 3/4 angle)
        CreateOfficeDesk("Primary_Desk", furnitureRoot.transform, new Vector3(-0.6f, 0, -0.6f), -35f, deskMat, metalMat, screenEmissionMat);

        // Secondary Desk behind partition
        CreateOfficeDesk("Secondary_Desk", furnitureRoot.transform, new Vector3(2.4f, 0, 0.8f), -90f, deskMat, metalMat, screenEmissionMat);

        // Filing Cabinets against back wall
        CreateBox("Cabinet_1", furnitureRoot.transform, new Vector3(-2.8f, 0.9f, 3.7f), new Vector3(1.2f, 1.8f, 0.6f), metalMat);
        CreateBox("Cabinet_2", furnitureRoot.transform, new Vector3(-1.8f, 0.9f, 3.7f), new Vector3(0.7f, 1.8f, 0.6f), metalMat);

        // Whiteboard on left wall
        CreateBox("Whiteboard", furnitureRoot.transform, new Vector3(-3.95f, 1.8f, -0.5f), new Vector3(0.08f, 1.2f, 2.4f), ceilingMat);

        // 6. Lighting Setup
        GameObject lightingRoot = new GameObject("Lighting");
        lightingRoot.transform.SetParent(roomRoot.transform);

        // --- 4 Fluorescent Light Fixtures on Ceiling (2x2 Grid) ---
        Vector3[] lampPositions = new Vector3[]
        {
            new Vector3(-1.6f, 3.18f, 1.6f),
            new Vector3(-1.6f, 3.18f, -1.6f),
            new Vector3(1.8f, 3.18f, 1.6f),
            new Vector3(1.8f, 3.18f, -1.6f)
        };

        for (int i = 0; i < lampPositions.Length; i++)
        {
            Vector3 pos = lampPositions[i];
            // Visual lamp fixture box
            GameObject fixture = CreateBox("LampFixture_" + (i + 1), lightingRoot.transform, pos, new Vector3(0.6f, 0.08f, 1.8f), metalMat);
            // Emissive tube
            GameObject tube = CreateBox("Tube", fixture.transform, pos - new Vector3(0, 0.04f, 0), new Vector3(0.35f, 0.04f, 1.6f), lampEmissionMat);

            // Spot Light pointing down
            GameObject lightGo = new GameObject("Fluorescent_Light_" + (i + 1));
            lightGo.transform.SetParent(lightingRoot.transform);
            lightGo.transform.position = pos - new Vector3(0, 0.1f, 0);
            lightGo.transform.rotation = Quaternion.Euler(90f, 0, 0);

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.93f, 0.98f, 0.95f); // Cool Fluorescent White
            light.intensity = 20f;
            light.range = 7.0f;
            light.spotAngle = 115f;
            light.innerSpotAngle = 65f;
            light.shadows = LightShadows.Soft;
        }

        // --- Room Indirect Bounce / Fill Light (Soft ambient, no shadows) ---
        GameObject fillLightObj = new GameObject("Room_Indirect_FillLight");
        fillLightObj.transform.SetParent(lightingRoot.transform);
        fillLightObj.transform.position = new Vector3(0, 2.0f, 0);
        Light fillLight = fillLightObj.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.color = new Color(0.70f, 0.78f, 0.85f); // Soft cool bounce
        fillLight.intensity = 0.8f;
        fillLight.range = 10.0f;
        fillLight.shadows = LightShadows.None;

        // --- Monitor CRT Phosphor Glow Lights (Right in front of screens) ---
        // Desk 1 Screen Glow
        Vector3 desk1ScreenWorld = new Vector3(-0.6f, 0, -0.6f) + Quaternion.Euler(0, -35f, 0) * new Vector3(0, 0.96f, -0.15f);
        CreateMonitorGlow(lightingRoot.transform, desk1ScreenWorld);

        // --- Emergency / Exit Light (Red accent on back wall) ---
        GameObject exitSign = CreateBox("Exit_Sign", lightingRoot.transform, new Vector3(2.5f, 2.5f, 3.92f), new Vector3(0.45f, 0.25f, 0.12f), emergencyMat);
        GameObject redLightObj = new GameObject("Emergency_Red_Light");
        redLightObj.transform.SetParent(lightingRoot.transform);
        redLightObj.transform.position = new Vector3(2.5f, 2.4f, 3.6f);
        Light redLight = redLightObj.AddComponent<Light>();
        redLight.type = LightType.Point;
        redLight.color = new Color(1.0f, 0.15f, 0.1f);
        redLight.intensity = 3.0f;
        redLight.range = 5.5f;
        redLight.shadows = LightShadows.None;

        // 7. Post-Processing Global Volume (URP)
        GameObject volumeObj = new GameObject("Global_PostProcess_Volume");
        Volume volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "OfficeLightingProfile";

        // Bloom: Gives fluorescent tubes and CRT monitors realistic glow
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(1.2f);
        bloom.scatter.Override(0.7f);

        // Tonemapping: ACES
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        // Vignette
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.4f);

        volume.sharedProfile = profile;

        // 8. Main Camera Setup (Positioned for cinematic first-person view of the office)
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        camObj.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();
        camObj.AddComponent<UniversalAdditionalCameraData>();

        // Stand in front of the desk looking across the room
        camObj.transform.position = new Vector3(-2.2f, 1.45f, -2.4f);
        camObj.transform.rotation = Quaternion.Euler(11.0f, 38.0f, 0);
        cam.fieldOfView = 65f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        // 9. Save Scene
        string scenePath = "Assets/Scenes/OfficeLightingDemo.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("Complete Office Lighting Scene created successfully at: " + scenePath);
    }

    private static void CreateOfficeDesk(string name, Transform parent, Vector3 pos, float rotY, Material deskMat, Material pcMat, Material screenMat)
    {
        GameObject deskRoot = new GameObject(name);
        deskRoot.transform.SetParent(parent);
        deskRoot.transform.position = pos;
        deskRoot.transform.rotation = Quaternion.Euler(0, rotY, 0);

        // Desk Tabletop (1.6m x 0.85m, height 0.75m)
        CreateBox("Tabletop", deskRoot.transform, new Vector3(0, 0.72f, 0), new Vector3(1.6f, 0.06f, 0.85f), deskMat);

        // Legs
        float lx = 0.72f, lz = 0.36f, legH = 0.7f;
        CreateBox("Leg_BL", deskRoot.transform, new Vector3(-lx, legH * 0.5f, -lz), new Vector3(0.08f, legH, 0.08f), pcMat);
        CreateBox("Leg_BR", deskRoot.transform, new Vector3(lx, legH * 0.5f, -lz), new Vector3(0.08f, legH, 0.08f), pcMat);
        CreateBox("Leg_FL", deskRoot.transform, new Vector3(-lx, legH * 0.5f, lz), new Vector3(0.08f, legH, 0.08f), pcMat);
        CreateBox("Leg_FR", deskRoot.transform, new Vector3(lx, legH * 0.5f, lz), new Vector3(0.08f, legH, 0.08f), pcMat);

        // Under-desk Drawer Unit
        CreateBox("Drawer", deskRoot.transform, new Vector3(0.48f, 0.36f, 0), new Vector3(0.42f, 0.65f, 0.75f), pcMat);

        // CRT Monitor Casing (Angled slightly toward user)
        CreateBox("PC_Monitor_Casing", deskRoot.transform, new Vector3(-0.1f, 0.96f, 0.12f), new Vector3(0.46f, 0.40f, 0.42f), pcMat);
        // CRT Screen (Emissive front face facing forward toward chair/camera)
        CreateBox("PC_Screen", deskRoot.transform, new Vector3(-0.1f, 0.96f, -0.095f), new Vector3(0.38f, 0.32f, 0.02f), screenMat);

        // Keyboard & Mouse
        CreateBox("Keyboard", deskRoot.transform, new Vector3(-0.1f, 0.76f, -0.22f), new Vector3(0.42f, 0.02f, 0.15f), pcMat);
        CreateBox("Mouse", deskRoot.transform, new Vector3(0.24f, 0.76f, -0.22f), new Vector3(0.06f, 0.025f, 0.1f), pcMat);

        // PC Tower
        CreateBox("PC_Tower", deskRoot.transform, new Vector3(-0.48f, 0.24f, 0.1f), new Vector3(0.18f, 0.44f, 0.42f), pcMat);

        // Office Chair
        GameObject chairRoot = new GameObject("Office_Chair");
        chairRoot.transform.SetParent(deskRoot.transform);
        chairRoot.transform.localPosition = new Vector3(-0.1f, 0, -0.65f);
        chairRoot.transform.localRotation = Quaternion.Euler(0, 20f, 0);

        CreateBox("Chair_Seat", chairRoot.transform, chairRoot.transform.position + new Vector3(0, 0.48f, 0), new Vector3(0.45f, 0.08f, 0.45f), deskMat);
        CreateBox("Chair_Back", chairRoot.transform, chairRoot.transform.position + new Vector3(0, 0.82f, -0.2f), new Vector3(0.42f, 0.5f, 0.08f), deskMat);
        CreateBox("Chair_Pole", chairRoot.transform, chairRoot.transform.position + new Vector3(0, 0.22f, 0), new Vector3(0.08f, 0.42f, 0.08f), pcMat);
        CreateBox("Chair_Base", chairRoot.transform, chairRoot.transform.position + new Vector3(0, 0.04f, 0), new Vector3(0.5f, 0.06f, 0.5f), pcMat);
    }

    private static void CreateMonitorGlow(Transform parent, Vector3 pos)
    {
        GameObject glowObj = new GameObject("Monitor_Glow");
        glowObj.transform.SetParent(parent);
        glowObj.transform.position = pos;
        Light light = glowObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.15f, 1.0f, 0.45f);
        light.intensity = 1.8f;
        light.range = 2.6f;
        light.shadows = LightShadows.None;
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (mat != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        return go;
    }
}
