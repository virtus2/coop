using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

public static class SetupPlayerSessionArchitecture
{
    [MenuItem("Tools/Coop/Setup Player Session Architecture")]
    public static void Execute()
    {
        Debug.Log("[SetupPlayerSessionArchitecture] 플레이어 세션 및 캐릭터 분리 아키텍처 설정을 시작합니다...");

        // 1. PlayerSessionPrefab 생성/로드
        GameObject sessionPrefab = SetupPlayerSessionPrefab();

        // 2. Dummy FBX 기반 PlayerDummyPrefab 정돈
        GameObject characterPrefab = SetupDummyCharacterPrefab();

        // 3. MainScene의 NetworkManager 및 PlayerSpawner 설정
        SetupMainScene(sessionPrefab, characterPrefab);

        // 4. GameScene의 PlayerCameraController 설정
        SetupGameScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPlayerSessionArchitecture] Dummy FBX 캐릭터 연동 및 모든 설정이 성공적으로 완료되었습니다!");
    }

    private static GameObject SetupPlayerSessionPrefab()
    {
        string path = "Assets/Prefabs/PlayerSessionPrefab.prefab";
        GameObject sessionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (sessionPrefab == null)
        {
            GameObject sessionGO = new GameObject("PlayerSessionPrefab");
            sessionGO.AddComponent<NetworkObject>();
            sessionGO.AddComponent<PlayerInventory>();
            sessionGO.AddComponent<NetworkPlayer>();

            sessionPrefab = PrefabUtility.SaveAsPrefabAsset(sessionGO, path);
            Object.DestroyImmediate(sessionGO);
            Debug.Log($"[SetupPlayerSessionArchitecture] {path} 신규 생성 완료.");
        }
        else
        {
            // 컴포넌트 유효성 검사
            string prefabPath = AssetDatabase.GetAssetPath(sessionPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
            if (root.GetComponent<PlayerInventory>() == null) root.AddComponent<PlayerInventory>();
            if (root.GetComponent<NetworkPlayer>() == null) root.AddComponent<NetworkPlayer>();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[SetupPlayerSessionArchitecture] {path} 검증 완료.");
        }

        return sessionPrefab;
    }

    private static GameObject SetupDummyCharacterPrefab()
    {
        // 사용자가 Dummy FBX로 제작한 최신 캐릭터 프리팹
        string dummyPrefabPath = "Assets/Prefabs/PlayerDummyPrefab.prefab";
        GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dummyPrefabPath);

        if (dummyPrefab == null)
        {
            Debug.LogError($"[SetupPlayerSessionArchitecture] {dummyPrefabPath}를 찾을 수 없습니다!");
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(dummyPrefabPath);

        // 인벤토리 컴포넌트 제거 (인벤토리는 이제 NetworkPlayer가 보유)
        var inv = root.GetComponent<PlayerInventory>();
        if (inv != null)
        {
            Object.DestroyImmediate(inv, true);
            Debug.Log("[SetupPlayerSessionArchitecture] PlayerDummyPrefab에서 PlayerInventory 컴포넌트를 제거했습니다 (NetworkPlayer 세션 귀속).");
        }

        // PlayerCharacter / PlayerController 컴포넌트 검증
        var pc = root.GetComponent<PlayerCharacter>();
        if (pc == null)
        {
            var controller = root.GetComponent<PlayerController>();
            if (controller == null)
            {
                root.AddComponent<PlayerController>();
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, dummyPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // 하위 호환을 위해 PlayerCharacterPrefab에도 동일하게 복제/동기화
        string characterPrefabPath = "Assets/Prefabs/PlayerCharacterPrefab.prefab";
        AssetDatabase.DeleteAsset(characterPrefabPath);
        AssetDatabase.CopyAsset(dummyPrefabPath, characterPrefabPath);

        Debug.Log($"[SetupPlayerSessionArchitecture] {dummyPrefabPath} 정돈 및 {characterPrefabPath} 동기화 완료.");
        return dummyPrefab;
    }

    private static void SetupMainScene(GameObject sessionPrefab, GameObject characterPrefab)
    {
        string mainScenePath = "Assets/Scenes/MainScene.unity";
        var scene = EditorSceneManager.OpenScene(mainScenePath, OpenSceneMode.Single);

        var netManager = Object.FindFirstObjectByType<NetworkManager>();
        if (netManager != null)
        {
            // PlayerPrefab을 PlayerSessionPrefab으로 지정
            netManager.NetworkConfig.PlayerPrefab = sessionPrefab;

            // CharacterPrefab(PlayerDummyPrefab)을 NetworkPrefabs 목록에 추가
            bool alreadyAdded = false;
            foreach (var item in netManager.NetworkConfig.Prefabs.Prefabs)
            {
                if (item.Prefab == characterPrefab)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                netManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = characterPrefab });
                Debug.Log($"[SetupPlayerSessionArchitecture] NetworkManager NetworkPrefabs에 {characterPrefab.name}을 등록했습니다.");
            }

            EditorUtility.SetDirty(netManager);
        }

        var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
        if (spawner != null)
        {
            var serializedSpawner = new SerializedObject(spawner);
            var charProp = serializedSpawner.FindProperty("_characterPrefab");
            if (charProp != null)
            {
                charProp.objectReferenceValue = characterPrefab;
                serializedSpawner.ApplyModifiedProperties();
                Debug.Log($"[SetupPlayerSessionArchitecture] PlayerSpawner의 _characterPrefab에 {characterPrefab.name}을 할당했습니다.");
            }
            EditorUtility.SetDirty(spawner);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupPlayerSessionArchitecture] {mainScenePath} 저장 완료.");
    }

    private static void SetupGameScene()
    {
        string gameScenePath = "Assets/Scenes/GameScene.unity";
        var scene = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Single);

        var camCtrl = Object.FindFirstObjectByType<PlayerCameraController>();
        if (camCtrl == null)
        {
            var vcam = Object.FindFirstObjectByType<CinemachineCamera>();
            GameObject camCtrlGO = new GameObject("PlayerCameraController");
            camCtrl = camCtrlGO.AddComponent<PlayerCameraController>();

            if (vcam != null)
            {
                var serializedCam = new SerializedObject(camCtrl);
                var vcamProp = serializedCam.FindProperty("_virtualCamera");
                if (vcamProp != null)
                {
                    vcamProp.objectReferenceValue = vcam;
                    serializedCam.ApplyModifiedProperties();
                }
            }

            Debug.Log("[SetupPlayerSessionArchitecture] GameScene에 PlayerCameraController를 추가했습니다.");
            EditorUtility.SetDirty(camCtrlGO);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupPlayerSessionArchitecture] {gameScenePath} 저장 완료.");
    }
}
