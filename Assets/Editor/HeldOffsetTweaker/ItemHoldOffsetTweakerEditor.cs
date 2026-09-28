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
            "1. 상단 툴바에서 [📷 1인칭 뷰모델] 또는 [🧍 3인칭 오른손] 모드를 선택합니다.\n" +
            "2. Target Item Data에 편집할 총기 에셋(Gun_AssaultRifle 등)을 할당합니다.\n" +
            "3. [🎯 Select Preview (W/E/R)] 버튼을 눌러 오른손 총기 부착 위치/각도를 손에 맞춥니다.\n" +
            "4. 양손 무기인 경우, 아래 [🎯 Select Left Hand IK Target (W/E)] 버튼을 눌러 왼손이 파지할 앞총열(핸드가드) 위치로 기즈모를 드래그합니다.\n" +
            "5. [💾 Save to ItemData] 버튼을 누르면 총기 오프셋 및 왼손 IK 파지 위치가 ItemData에 영구 저장됩니다.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        int currentModeIndex = _tweaker.Mode == ItemHoldOffsetTweaker.SocketEditMode.FirstPerson1P ? 0 : 1;
        EditorGUI.BeginChangeCheck();
        int newModeIndex = GUILayout.Toolbar(currentModeIndex, new string[] { "📷 1인칭 뷰모델 (1P Camera)", "🧍 3인칭 오른손 (3P HoldPoint3P)" }, GUILayout.Height(32));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_tweaker, "Change Socket Edit Mode");
            _tweaker.CleanupPreviewInstance();
            _tweaker.Mode = (ItemHoldOffsetTweaker.SocketEditMode)newModeIndex;
            _tweaker.HoldPoint = _tweaker.FindHoldPointForMode(_tweaker.Mode);
            _tweaker.RefreshPreview(true);
            EditorUtility.SetDirty(_tweaker);
        }
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
                EditorGUILayout.LabelField("Live Socket Local Transform (오른손 소켓 부착 오프셋)", EditorStyles.miniBoldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector3Field("Local Position", pt.localPosition);
                EditorGUILayout.Vector3Field("Local Rotation", pt.localEulerAngles);
                EditorGUILayout.Vector3Field("Local Scale", pt.localScale);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();

                // Two-Bone IK 섹션
                SerializedObject targetDataSo = new SerializedObject(_tweaker.TargetItemData);
                targetDataSo.Update();
                SerializedProperty useIKProp = targetDataSo.FindProperty("_useLeftHandIK");

                EditorGUILayout.Space(6);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(useIKProp, new GUIContent("✋ Use Left Hand IK (양손 무기)", "총기 파지 시 왼손 Two-Bone IK 활성화 여부"));
                if (EditorGUI.EndChangeCheck())
                {
                    targetDataSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_tweaker.TargetItemData);
                    _tweaker.RefreshPreview(false);
                }

                if (_tweaker.TargetItemData.UseLeftHandIK)
                {
                    if (_tweaker.IKTargetInstance != null)
                    {
                        Transform ikT = _tweaker.IKTargetInstance.transform;
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.Vector3Field("IK Local Position", ikT.localPosition);
                        EditorGUILayout.Vector3Field("IK Local Rotation", ikT.localEulerAngles);
                        EditorGUI.EndDisabledGroup();

                        EditorGUILayout.Space(4);
                        if (GUILayout.Button("🎯 Select Left Hand IK Target (W/E)", GUILayout.Height(28)))
                        {
                            _tweaker.SelectIKTargetObject();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("IK 타겟 프리뷰 오브젝트가 아직 생성되지 않았습니다.", MessageType.Info);
                    }
                }
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
