using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerHUDCanvas 및 AmmoHUD 프리팹/씬 구성의 정합성을 검증하는 에디터 도구입니다.
/// </summary>
public static class PlayerHUDVerification
{
    private const string PREFAB_PATH = "Assets/Prefabs/PlayerHUDCanvas.prefab";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Verify Player HUD Setup")]
    public static string RunVerification()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("================ [Player HUD Canvas 검증 시작] ================");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
        {
            sb.AppendLine($"[FAIL] 프리팹을 찾을 수 없습니다: {PREFAB_PATH}");
            return sb.ToString();
        }
        sb.AppendLine("[PASS] 프리팹 로드 성공");

        var activeScene = EditorSceneManager.GetActiveScene();
        sb.AppendLine($"현재 활성 씬: '{activeScene.path}'");
        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            sb.AppendLine($"씬 열기: '{activeScene.path}'");
        }

        GameObject hudCanvasGO = GameObject.Find("PlayerHUDCanvas");
        if (hudCanvasGO == null)
        {
            sb.AppendLine($"[FAIL] {GAME_SCENE_PATH}에 PlayerHUDCanvas 오브젝트가 없습니다.");
            return sb.ToString();
        }
        sb.AppendLine("[PASS] PlayerHUDCanvas 오브젝트 발견");

        AmmoHUD ammoHUD = hudCanvasGO.GetComponentInChildren<AmmoHUD>(true);
        if (ammoHUD == null)
        {
            sb.AppendLine("[FAIL] 씬 내 PlayerHUDCanvas에 AmmoHUD가 없습니다.");
            return sb.ToString();
        }
        sb.AppendLine("[PASS] AmmoHUD 컴포넌트 발견");

        SerializedObject so = new SerializedObject(ammoHUD);
        var panel = so.FindProperty("_ammoPanel").objectReferenceValue;
        var ammoText = so.FindProperty("_ammoText").objectReferenceValue;
        var statusText = so.FindProperty("_statusText").objectReferenceValue;

        sb.AppendLine($"_ammoPanel: {panel != null}");
        sb.AppendLine($"_ammoText: {ammoText != null}");
        sb.AppendLine($"_statusText: {statusText != null}");

        if (panel == null || ammoText == null || statusText == null)
        {
            sb.AppendLine("[FAIL] 직렬화 프로퍼티 중 null이 존재합니다.");
        }
        else
        {
            sb.AppendLine("[PASS] 모든 직렬화 프로퍼티 바인딩 정상 확인!");
        }

        return sb.ToString();
    }

    private static bool VerifyPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[Verification] 프리팹을 찾을 수 없습니다: {PREFAB_PATH}");
            return false;
        }

        Canvas canvas = prefab.GetComponent<Canvas>();
        CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
        GraphicRaycaster raycaster = prefab.GetComponent<GraphicRaycaster>();
        if (canvas == null || scaler == null || raycaster == null)
        {
            Debug.LogError("[Verification] 프리팹 루트에 Canvas, CanvasScaler 또는 GraphicRaycaster가 누락되었습니다.");
            return false;
        }

        AmmoHUD ammoHUD = prefab.GetComponentInChildren<AmmoHUD>(true);
        if (ammoHUD == null)
        {
            Debug.LogError("[Verification] 프리팹 자식에 AmmoHUD 컴포넌트가 없습니다.");
            return false;
        }

        SerializedObject so = new SerializedObject(ammoHUD);
        var panelProp = so.FindProperty("_ammoPanel");
        var ammoTextProp = so.FindProperty("_ammoText");
        var statusTextProp = so.FindProperty("_statusText");

        if (panelProp.objectReferenceValue == null)
        {
            Debug.LogError("[Verification] AmmoHUD의 _ammoPanel이 바인딩되지 않았습니다.");
            return false;
        }

        if (ammoTextProp.objectReferenceValue == null)
        {
            Debug.LogError("[Verification] AmmoHUD의 _ammoText가 바인딩되지 않았습니다.");
            return false;
        }

        if (statusTextProp.objectReferenceValue == null)
        {
            Debug.LogError("[Verification] AmmoHUD의 _statusText가 바인딩되지 않았습니다.");
            return false;
        }

        Debug.Log("<color=green>[PASS]</color> PlayerHUDCanvas 프리팹 및 직렬화 필드 바인딩 검증 완료.");
        return true;
    }

    private static bool VerifyGameScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
        }

        GameObject hudCanvasGO = GameObject.Find("PlayerHUDCanvas");
        if (hudCanvasGO == null)
        {
            Debug.LogError($"[Verification] {GAME_SCENE_PATH}에 PlayerHUDCanvas 오브젝트가 없습니다.");
            return false;
        }

        AmmoHUD ammoHUD = hudCanvasGO.GetComponentInChildren<AmmoHUD>(true);
        if (ammoHUD == null)
        {
            Debug.LogError("[Verification] 씬 내 PlayerHUDCanvas에 AmmoHUD가 없습니다.");
            return false;
        }

        SerializedObject so = new SerializedObject(ammoHUD);
        if (so.FindProperty("_ammoPanel").objectReferenceValue == null ||
            so.FindProperty("_ammoText").objectReferenceValue == null ||
            so.FindProperty("_statusText").objectReferenceValue == null)
        {
            Debug.LogError("[Verification] 씬 내 AmmoHUD의 직렬화 필드가 바인딩되지 않았습니다.");
            return false;
        }

        Debug.Log($"<color=green>[PASS]</color> {GAME_SCENE_PATH} 내 PlayerHUDCanvas 배치 및 정상 연결 확인 완료.");
        return true;
    }
}
