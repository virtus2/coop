using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InteractionSystemVerification
{
    [MenuItem("Tools/Run Interaction Verification Tests")]
    public static void RunAllTests()
    {
        Debug.Log("==== [상호작용 시스템 검증 테스트 시작] ====");
        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Debug.Log($"<color=green>[PASS]</color> {testName}");
                passed++;
            }
            else
            {
                Debug.LogError($"<color=red>[FAIL]</color> {testName}");
                failed++;
            }
        }

        try
        {
            // 1. IInteractable 인터페이스 검증
            Type interactableType = typeof(IInteractable);
            Assert(interactableType.IsInterface, "IInteractable은 인터페이스여야 함");
            Assert(interactableType.GetMethod("Interact") != null, "IInteractable.Interact() 메서드 존재 확인");
            Assert(interactableType.GetMethod("CanInteract") != null, "IInteractable.CanInteract() 메서드 존재 확인");
            Assert(interactableType.GetMethod("GetInteractionPrompt") != null, "IInteractable.GetInteractionPrompt() 메서드 존재 확인");

            // 2. 프리팹 에셋 검증
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab");
            Assert(playerPrefab != null, "PlayerPrefab 에셋 존재 확인");
            var playerInteraction = playerPrefab.GetComponent<PlayerInteraction>();
            Assert(playerInteraction != null, "PlayerPrefab에 PlayerInteraction 컴포넌트 부착 확인");

            GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PickableBox.prefab");
            Assert(boxPrefab != null, "PickableBox 프리팹 에셋 존재 확인");
            var pickableItem = boxPrefab.GetComponent<PickableItem>();
            Assert(pickableItem != null, "PickableBox에 PickableItem 컴포넌트 부착 확인");
            Assert(pickableItem is IInteractable, "PickableItem이 IInteractable을 구현하는지 확인");

            // 3. 인메모리 상호작용 (들기/내려놓기) 로직 검증
            GameObject testPlayerGO = UnityEngine.Object.Instantiate(playerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity);
            GameObject testBoxGO = UnityEngine.Object.Instantiate(boxPrefab, new Vector3(0f, 1f, 2f), Quaternion.identity);

            try
            {
                var interactor = testPlayerGO.GetComponent<PlayerInteraction>();
                var item = testBoxGO.GetComponent<PickableItem>();
                var rb = testBoxGO.GetComponent<Rigidbody>();

                Assert(interactor != null && item != null, "테스트 인스턴스 생성 성공");
                Assert(item.CanInteract(interactor), "들기 전 CanInteract()는 true여야 함");
                Assert(!interactor.IsHoldingItem, "들기 전 플레이어는 아이템을 들고 있지 않아야 함");

                // E키 상호작용 실행 (들기)
                item.Interact(interactor);

                Assert(item.IsHeld, "상호작용 후 item.IsHeld는 true여야 함");
                Assert(interactor.IsHoldingItem, "상호작용 후 interactor.IsHoldingItem은 true여야 함");
                Assert(interactor.HeldItem == item, "interactor.HeldItem이 들고 있는 item과 일치해야 함");
                Assert(testBoxGO.transform.parent == null, "NetworkObject 부모 제약 우회를 위해 parent는 null로 유지되어야 함");
                Assert(rb.isKinematic, "들고 있는 동안 Rigidbody.isKinematic은 true여야 함");
                Assert(!item.CanInteract(interactor), "이미 들고 있는 동안에는 CanInteract()가 false여야 함");

                // 내려놓기 실행
                interactor.DropHeldItem();

                Assert(!item.IsHeld, "내려놓은 후 item.IsHeld는 false여야 함");
                Assert(!interactor.IsHoldingItem, "내려놓은 후 interactor.IsHoldingItem은 false여야 함");
                Assert(interactor.HeldItem == null, "내려놓은 후 interactor.HeldItem은 null이어야 함");
                Assert(testBoxGO.transform.parent == null, "내려놓은 후 부모 Transform은 null이어야 함");
                Assert(!rb.isKinematic, "내려놓은 후 Rigidbody.isKinematic은 false여야 함");
                Assert(item.CanInteract(interactor), "내려놓은 후 다시 CanInteract()는 true여야 함");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testPlayerGO);
                UnityEngine.Object.DestroyImmediate(testBoxGO);
            }

            // 4. GameScene 설정 검증
            var gameScene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
            var hud = GameObject.Find("InteractionHUD");
            Assert(hud != null, "GameScene에 InteractionHUD 존재 확인");
            Assert(hud.GetComponent<InteractionUI>() != null, "InteractionHUD에 InteractionUI 컴포넌트 부착 확인");

            var sampleBox = GameObject.Find("SamplePickableBox_1");
            Assert(sampleBox != null, "GameScene에 SamplePickableBox_1 존재 확인");
            Assert(sampleBox.GetComponent<PickableItem>() != null, "SamplePickableBox_1에 PickableItem 부착 확인");

            var bootstrap = GameObject.Find("GameSceneBootstrap");
            Assert(bootstrap != null, "GameScene에 GameSceneBootstrap 존재 확인");

            Debug.Log($"==== [검증 완료] 통과: {passed}개, 실패: {failed}개 ====");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[검증 중 예외 발생] {ex}");
            failed++;
        }
    }

    [MenuItem("Tools/Simulate Look Down At Box")]
    public static void SimulateLookDownAtBox()
    {
        var player = GameObject.Find("Player (Direct Play)");
        var box = GameObject.Find("SamplePickableBox_1");
        if (player != null && box != null)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var pitchField = typeof(PlayerController).GetField("_cameraPitch", flags);
                if (pitchField != null)
                {
                    pitchField.SetValue(pc, 42f);
                    Debug.Log("[SimulateLookDownAtBox] PlayerController _cameraPitch를 42도로 설정했습니다.");
                }
            }
        }
    }

    [MenuItem("Tools/Debug Aim Raycast State")]
    public static void DebugAimRaycast()
    {
        var player = GameObject.Find("Player (Direct Play)");
        if (player != null)
        {
            var interactor = player.GetComponent<PlayerInteraction>();
            var cam = Camera.main;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, 10f);
            Debug.Log($"[DebugAim] Camera: {cam?.name}, Pos: {cam?.transform.position}, Fwd: {cam?.transform.forward}");
            Debug.Log($"[DebugAim] Ray hit: {hit}, Collider: {hitInfo.collider?.name}, Distance: {hitInfo.distance}");
            Debug.Log($"[DebugAim] InteractionUI.Instance: {InteractionUI.Instance != null}");
            if (hitInfo.collider != null)
            {
                var pickable = hitInfo.collider.GetComponentInParent<PickableItem>();
                Debug.Log($"[DebugAim] Hit Pickable: {pickable != null}");
            }
        }
    }

    [MenuItem("Tools/Simulate Pick Up in Play Mode")]
    public static void SimulatePickUp()
    {
        var player = GameObject.Find("Player (Direct Play)");
        if (player != null)
        {
            var interactor = player.GetComponent<PlayerInteraction>();
            var box = GameObject.Find("SamplePickableBox_1");
            if (interactor != null && box != null)
            {
                var pickable = box.GetComponent<PickableItem>();
                if (pickable != null)
                {
                    pickable.Interact(interactor);
                    Debug.Log("[SimulatePickUp] 상자 줍기 실행 완료!");
                }
            }
        }
    }

    [MenuItem("Tools/Simulate Drop in Play Mode")]
    public static void SimulateDrop()
    {
        var player = GameObject.Find("Player (Direct Play)");
        if (player != null)
        {
            var interactor = player.GetComponent<PlayerInteraction>();
            if (interactor != null && interactor.IsHoldingItem)
            {
                interactor.DropHeldItem();
                Debug.Log("[SimulateDrop] 상자 내려놓기 실행 완료!");
            }
        }
    }
}
