using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Animator Controller에 상체 LookAt 및 왼손 IK 분리 처리를 위한 레이어를 설정하는 유틸리티입니다.
/// </summary>
public static class SetupAnimatorIKLayers
{
    private const string CONTROLLER_PATH = "Assets/Animations/DummyAnimatorController.controller";
    private const string UPPER_BODY_LAYER_NAME = "UpperBody";
    private const string LEFT_HAND_LAYER_NAME = "LeftHandIK";

    [MenuItem("Tools/Animation/Setup Left Hand IK Layer")]
    public static void SetupLayers()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            Debug.LogError($"[SetupAnimatorIKLayers] Controller를 찾을 수 없습니다: {CONTROLLER_PATH}");
            return;
        }

        var layers = controller.layers;

        // 1. UpperBody 레이어 IKPass 활성화 확인
        int upperBodyIndex = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == UPPER_BODY_LAYER_NAME)
            {
                upperBodyIndex = i;
                break;
            }
        }

        if (upperBodyIndex >= 0)
        {
            var upperLayer = layers[upperBodyIndex];
            upperLayer.iKPass = true;
            upperLayer.defaultWeight = 1.0f;
            layers[upperBodyIndex] = upperLayer;
        }
        else
        {
            Debug.LogWarning($"[SetupAnimatorIKLayers] '{UPPER_BODY_LAYER_NAME}' 레이어를 찾을 수 없습니다.");
        }

        // 2. LeftHandIK 레이어 확인 및 생성
        int leftHandIndex = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == LEFT_HAND_LAYER_NAME)
            {
                leftHandIndex = i;
                break;
            }
        }

        if (leftHandIndex < 0)
        {
            controller.AddLayer(LEFT_HAND_LAYER_NAME);
            layers = controller.layers;
            leftHandIndex = layers.Length - 1;
        }

        var leftHandLayer = layers[leftHandIndex];
        leftHandLayer.name = LEFT_HAND_LAYER_NAME;
        leftHandLayer.defaultWeight = 1.0f;
        leftHandLayer.iKPass = true;
        leftHandLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        layers[leftHandIndex] = leftHandLayer;
        controller.layers = layers;

        // State Machine 내 빈 IK 패스용 기본 상태 구성
        AnimatorStateMachine sm = leftHandLayer.stateMachine;
        if (sm != null)
        {
            AnimatorState emptyIKState = null;
            foreach (var state in sm.states)
            {
                if (state.state != null && state.state.name == "Empty_IK")
                {
                    emptyIKState = state.state;
                    break;
                }
            }

            if (emptyIKState == null)
            {
                emptyIKState = sm.AddState("Empty_IK", new Vector3(300, 50, 0));
            }

            emptyIKState.motion = null;
            emptyIKState.writeDefaultValues = false;
            sm.defaultState = emptyIKState;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[SetupAnimatorIKLayers]</color> '{CONTROLLER_PATH}'에 '{LEFT_HAND_LAYER_NAME}' (IK Pass On, Weight 1.0) 레이어 구성이 성공적으로 완료되었습니다!");
    }
}
