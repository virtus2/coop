#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemHoldOffsetTweaker))]
public class ItemHoldOffsetTweakerEditor : Editor
{
    private ItemHoldOffsetTweaker _tweaker;

    private void OnEnable()
    {
        _tweaker = (ItemHoldOffsetTweaker)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "1. 아래 Target Item Data에 편집할 총기/아이템(GunItemData 등)을 할당합니다.\n" +
            "2. [Select Preview Object] 버튼을 누르거나 씬 뷰에서 총기를 선택합니다.\n" +
            "3. 씬 뷰에서 유니티 기본 기즈모(W, E, R)로 위치, 각도, 크기를 손에 맞게 맞춥니다.\n" +
            "4. [💾 Save to ItemData] 버튼을 눌러 ScriptableObject에 영구 저장합니다.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        // 기본 프로퍼티 필드들
        SerializedProperty targetItemProp = serializedObject.FindProperty("_targetItemData");
        SerializedProperty holdPointProp = serializedObject.FindProperty("_holdPoint");
        SerializedProperty previewCamProp = serializedObject.FindProperty("_previewCamera");

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(targetItemProp, new GUIContent("Target Item Data", "편집할 GunItemData 또는 ItemData 에셋"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            _tweaker.RefreshPreview(true);
        }

        EditorGUILayout.PropertyField(holdPointProp, new GUIContent("Hold Point Socket", "총기가 장착될 소켓 (비어있으면 자동 탐색)"));
        EditorGUILayout.PropertyField(previewCamProp, new GUIContent("Preview Camera", "1인칭 시점 뷰 카메라"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);

        // 현재 프리뷰 상태 표시 및 컨트롤
        if (_tweaker.TargetItemData != null)
        {
            EditorGUILayout.LabelField($"현재 편집 중: {_tweaker.TargetItemData.ItemName} ({_tweaker.TargetItemData.name})", EditorStyles.boldLabel);

            if (_tweaker.PreviewInstance != null)
            {
                Transform pt = _tweaker.PreviewInstance.transform;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Live Socket Local Transform", EditorStyles.miniBoldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector3Field("Local Position", pt.localPosition);
                EditorGUILayout.Vector3Field("Local Rotation", pt.localEulerAngles);
                EditorGUILayout.Vector3Field("Local Scale", pt.localScale);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // 선택 및 정렬 편의 버튼
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🎯 Select Preview (W/E/R)", GUILayout.Height(28)))
                {
                    _tweaker.SelectPreviewObject();
                }
                if (GUILayout.Button("🎥 Align Scene to Camera", GUILayout.Height(28)))
                {
                    AlignSceneViewToCamera();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);

                // 저장 버튼 (강조)
                Color originalBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);
                if (GUILayout.Button("💾 Save to ItemData", GUILayout.Height(38)))
                {
                    _tweaker.SaveToItemData();
                }
                GUI.backgroundColor = originalBg;

                EditorGUILayout.Space(4);

                // 리셋 및 프리뷰 갱신
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔄 Reset to Saved Offset", GUILayout.Height(26)))
                {
                    _tweaker.LoadFromItemData();
                }
                if (GUILayout.Button("♻️ Refresh Visual", GUILayout.Height(26)))
                {
                    _tweaker.RefreshPreview(false);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("프리뷰 오브젝트가 비활성화되어 있습니다.", MessageType.Warning);
                if (GUILayout.Button("프리뷰 생성", GUILayout.Height(30)))
                {
                    _tweaker.RefreshPreview(true);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Target Item Data에 편집할 총기 데이터(예: SampleGunItemData)를 드래그 앤 드롭하세요.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);
    }

    private void AlignSceneViewToCamera()
    {
        Camera cam = _tweaker.PreviewCamera;
        if (cam != null && SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.AlignViewToObject(cam.transform);
        }
    }
}
#endif
