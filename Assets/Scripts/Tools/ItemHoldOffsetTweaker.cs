#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// 에디터 씬 상에서 ItemData(GunItemData 등)의 손 소켓 부착 위치, 회전, 스케일을
/// 유니티 트랜스폼 기즈모(W, E, R)로 실시간 조작하고 ItemData에 저장할 수 있도록 지원하는 헬퍼 컴포넌트입니다.
/// 3인칭 오른손 소켓(HoldPoint3P)과 1인칭 소켓을 모두 지원합니다.
/// </summary>
[ExecuteAlways]
public class ItemHoldOffsetTweaker : MonoBehaviour
{
    public enum SocketEditMode
    {
        FirstPerson1P,
        ThirdPerson3P
    }

    [Header("Socket Selection")]
    [Tooltip("편집할 소켓 모드 (1인칭 카메라 화면 뷰모델 vs 3인칭 오른손 본 소켓)")]
    [SerializeField] private SocketEditMode _socketMode = SocketEditMode.FirstPerson1P;

    [Header("Target Settings")]
    [Tooltip("손 부착 위치/회전/스케일을 조절할 ItemData (GunItemData 등)")]
    [SerializeField] private ItemData _targetItemData;

    [Tooltip("총기/아이템이 부착될 기준 소켓 Transform (비어있으면 현재 모드에 맞춰 자동 탐색)")]
    [SerializeField] private Transform _holdPoint;

    [Tooltip("1인칭 시점 프리뷰 카메라")]
    [SerializeField] private Camera _previewCamera;

    [Header("Live Preview Object")]
    [SerializeField] private GameObject _previewInstance;
    [SerializeField] private GameObject _ikTargetInstance;

    private const string PREVIEW_OBJECT_NAME = "[Preview] HeldItemVisual";
    private const string PREVIEW_IK_NAME = "[Preview] LeftHandIKTarget";

    public GameObject IKTargetInstance => _ikTargetInstance;

    public SocketEditMode Mode
    {
        get => _socketMode;
        set
        {
            if (_socketMode != value)
            {
                _socketMode = value;
                _holdPoint = FindHoldPointForMode(_socketMode);
                RefreshPreview(true);
            }
        }
    }

    public ItemData TargetItemData
    {
        get => _targetItemData;
        set
        {
            if (_targetItemData != value)
            {
                _targetItemData = value;
                RefreshPreview(true);
            }
        }
    }

    public Transform HoldPoint
    {
        get
        {
            if (_holdPoint == null)
            {
                _holdPoint = FindHoldPointForMode(_socketMode);
            }
            return _holdPoint != null ? _holdPoint : transform;
        }
        set => _holdPoint = value;
    }

    public Transform FindHoldPointForMode(SocketEditMode mode)
    {
        if (mode == SocketEditMode.FirstPerson1P)
        {
            Transform camTarget = transform.Find("CameraTarget/HoldPoint");
            if (camTarget != null) return camTarget;
            Transform hp = FindDeepChild(transform, "HoldPoint");
            if (hp != null) return hp;
        }
        else
        {
            Transform hp3p = FindDeepChild(transform, "HoldPoint3P");
            if (hp3p != null) return hp3p;
            Transform rh = FindDeepChild(transform, "RightHand");
            if (rh != null) return rh;
        }
        return transform;
    }

    public Camera PreviewCamera
    {
        get
        {
            if (_previewCamera == null)
            {
                _previewCamera = GetComponentInChildren<Camera>(true);
                if (_previewCamera == null)
                {
                    _previewCamera = Camera.main;
                }
            }
            return _previewCamera;
        }
        set => _previewCamera = value;
    }

    public GameObject PreviewInstance => _previewInstance;

    private void OnEnable()
    {
        RefreshPreview(false);
    }

    private void OnDisable()
    {
        CleanupPreviewInstance();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RefreshPreview(false);
        }
    }

    /// <summary>
    /// 현재 대상 ItemData를 바탕으로 소켓 하위에 프리뷰 오브젝트를 갱신합니다.
    /// </summary>
    public void RefreshPreview(bool resetTransformToItemData = false)
    {
        if (_targetItemData == null)
        {
            CleanupPreviewInstance();
            return;
        }

        Transform targetSocket = HoldPoint;
        if (targetSocket == null) return;

        EnsurePreviewInstance(targetSocket);

        if (_previewInstance == null) return;

        Mesh mesh = _targetItemData.HeldMesh;
        Material mat = _targetItemData.HeldMaterial;

        if (mesh == null && _targetItemData.WorldPrefab != null)
        {
            var mf = _targetItemData.WorldPrefab.GetComponentInChildren<MeshFilter>(true);
            if (mf != null) mesh = mf.sharedMesh;
        }
        if (mat == null && _targetItemData.WorldPrefab != null)
        {
            var mr = _targetItemData.WorldPrefab.GetComponentInChildren<MeshRenderer>(true);
            if (mr != null) mat = mr.sharedMaterial;
        }

        var filter = _previewInstance.GetComponent<MeshFilter>();
        var renderer = _previewInstance.GetComponent<MeshRenderer>();

        if (filter != null) filter.sharedMesh = mesh;
        if (renderer != null) renderer.sharedMaterial = mat;

        if (resetTransformToItemData)
        {
            LoadFromItemData();
        }
    }

    public bool IsHandSocket()
    {
        if (_socketMode == SocketEditMode.ThirdPerson3P) return true;
        Transform socket = HoldPoint;
        if (socket == null) return false;
        if (socket.name == "HoldPoint3P" || socket.name == "RightHand") return true;
        Transform rh = FindDeepChild(transform, "RightHand");
        if (rh != null && (socket == rh || socket.IsChildOf(rh))) return true;
        return false;
    }

    /// <summary>
    /// 대상 ItemData에 저장되어 있는 위치, 회전, 스케일을 현재 프리뷰 오브젝트에 적용합니다.
    /// </summary>
    public void LoadFromItemData()
    {
        if (_targetItemData == null || _previewInstance == null) return;

        bool isHand = IsHandSocket();
        _previewInstance.transform.localPosition = isHand ? _targetItemData.HeldLocalPosition3P : _targetItemData.HeldLocalPosition;
        _previewInstance.transform.localRotation = Quaternion.Euler(isHand ? _targetItemData.HeldLocalRotation3P : _targetItemData.HeldLocalRotation);
        _previewInstance.transform.localScale = isHand ? _targetItemData.HeldLocalScale3P : _targetItemData.HeldLocalScale;

        EnsureIKTargetInstance();
        if (_ikTargetInstance != null)
        {
            _ikTargetInstance.transform.localPosition = _targetItemData.LeftHandIKLocalPosition;
            _ikTargetInstance.transform.localRotation = Quaternion.Euler(_targetItemData.LeftHandIKLocalRotation);
        }

        Debug.Log($"[ItemHoldOffsetTweaker] '{_targetItemData.ItemName}'의 저장된 오프셋(isHand={isHand})을 프리뷰에 적용했습니다.");
    }

#if UNITY_EDITOR
    /// <summary>
    /// 현재 프리뷰 오브젝트의 로컬 위치, 회전, 스케일 및 왼손 IK 타겟 위치를 대상 ItemData에 영구 저장합니다.
    /// </summary>
    public void SaveToItemData()
    {
        if (_targetItemData == null)
        {
            Debug.LogWarning("[ItemHoldOffsetTweaker] Target ItemData가 지정되지 않았습니다.");
            return;
        }

        if (_previewInstance == null)
        {
            Debug.LogWarning("[ItemHoldOffsetTweaker] 프리뷰 인스턴스가 존재하지 않아 저장할 수 없습니다.");
            return;
        }

        Vector3 currentPos = _previewInstance.transform.localPosition;
        Vector3 currentRot = _previewInstance.transform.localEulerAngles;
        Vector3 currentScale = _previewInstance.transform.localScale;

        SerializedObject so = new SerializedObject(_targetItemData);
        so.Update();

        bool isHand = IsHandSocket();
        string posField = isHand ? "_heldLocalPosition3P" : "_heldLocalPosition";
        string rotField = isHand ? "_heldLocalRotation3P" : "_heldLocalRotation";
        string scaleField = isHand ? "_heldLocalScale3P" : "_heldLocalScale";

        SerializedProperty posProp = so.FindProperty(posField);
        SerializedProperty rotProp = so.FindProperty(rotField);
        SerializedProperty scaleProp = so.FindProperty(scaleField);

        if (posProp != null) posProp.vector3Value = currentPos;
        if (rotProp != null) rotProp.vector3Value = currentRot;
        if (scaleProp != null) scaleProp.vector3Value = currentScale;

        string ikLog = "";
        if (_targetItemData.UseLeftHandIK && _ikTargetInstance != null)
        {
            SerializedProperty ikPosProp = so.FindProperty("_leftHandIKLocalPosition");
            SerializedProperty ikRotProp = so.FindProperty("_leftHandIKLocalRotation");
            if (ikPosProp != null) ikPosProp.vector3Value = _ikTargetInstance.transform.localPosition;
            if (ikRotProp != null) ikRotProp.vector3Value = _ikTargetInstance.transform.localEulerAngles;

            ikLog = $"\nLeftHandIK Pos: {_ikTargetInstance.transform.localPosition}\nLeftHandIK Rot: {_ikTargetInstance.transform.localEulerAngles}";
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_targetItemData);
        AssetDatabase.SaveAssets();

        Debug.Log($"<color=#4CAF50><b>[ItemHoldOffsetTweaker] '{_targetItemData.ItemName}' 저장 완료 (소켓: {(isHand ? "3P 오른손 소켓" : "1P 카메라 소켓")})!</b></color>\n" +
                  $"Position: {currentPos}\n" +
                  $"Rotation: {currentRot}\n" +
                  $"Scale: {currentScale}{ikLog}");
    }

    public void SelectPreviewObject()
    {
        if (_previewInstance != null)
        {
            Selection.activeGameObject = _previewInstance;
        }
    }

    public void SelectIKTargetObject()
    {
        if (_ikTargetInstance != null)
        {
            Selection.activeGameObject = _ikTargetInstance;
        }
    }
#endif

    private void EnsurePreviewInstance(Transform parentSocket)
    {
        if (_previewInstance == null)
        {
            Transform existing = parentSocket.Find(PREVIEW_OBJECT_NAME);
            if (existing != null)
            {
                _previewInstance = existing.gameObject;
            }
            else
            {
                bool isHand = IsHandSocket();
                _previewInstance = new GameObject(PREVIEW_OBJECT_NAME);
                _previewInstance.transform.SetParent(parentSocket, false);
                _previewInstance.transform.localPosition = _targetItemData != null ? (isHand ? _targetItemData.HeldLocalPosition3P : _targetItemData.HeldLocalPosition) : Vector3.zero;
                _previewInstance.transform.localRotation = _targetItemData != null ? Quaternion.Euler(isHand ? _targetItemData.HeldLocalRotation3P : _targetItemData.HeldLocalRotation) : Quaternion.identity;
                _previewInstance.transform.localScale = _targetItemData != null ? (isHand ? _targetItemData.HeldLocalScale3P : _targetItemData.HeldLocalScale) : Vector3.one;
            }

            _previewInstance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        if (_previewInstance.GetComponent<MeshFilter>() == null)
        {
            _previewInstance.AddComponent<MeshFilter>();
        }
        if (_previewInstance.GetComponent<MeshRenderer>() == null)
        {
            _previewInstance.AddComponent<MeshRenderer>();
        }

        _previewInstance.SetActive(true);
        EnsureIKTargetInstance();
    }

    private void EnsureIKTargetInstance()
    {
        if (_previewInstance == null || _targetItemData == null)
        {
            CleanupIKTargetInstance();
            return;
        }

        if (!_targetItemData.UseLeftHandIK)
        {
            CleanupIKTargetInstance();
            return;
        }

        if (_ikTargetInstance == null)
        {
            Transform existing = _previewInstance.transform.Find(PREVIEW_IK_NAME);
            if (existing != null)
            {
                _ikTargetInstance = existing.gameObject;
            }
            else
            {
                _ikTargetInstance = new GameObject(PREVIEW_IK_NAME);
                _ikTargetInstance.transform.SetParent(_previewInstance.transform, false);
                _ikTargetInstance.transform.localPosition = _targetItemData.LeftHandIKLocalPosition;
                _ikTargetInstance.transform.localRotation = Quaternion.Euler(_targetItemData.LeftHandIKLocalRotation);
                _ikTargetInstance.transform.localScale = Vector3.one;
            }

            _ikTargetInstance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        _ikTargetInstance.SetActive(true);
    }

    public void CleanupPreviewInstance()
    {
        CleanupIKTargetInstance();

        if (_previewInstance != null)
        {
            DestroyImmediate(_previewInstance);
            _previewInstance = null;
        }

        Transform socket = _holdPoint != null ? _holdPoint : FindHoldPointRecursive(transform);
        if (socket != null)
        {
            Transform existing = socket.Find(PREVIEW_OBJECT_NAME);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }
        }
    }

    public void CleanupIKTargetInstance()
    {
        if (_ikTargetInstance != null)
        {
            DestroyImmediate(_ikTargetInstance);
            _ikTargetInstance = null;
        }

        if (_previewInstance != null)
        {
            Transform existing = _previewInstance.transform.Find(PREVIEW_IK_NAME);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (_targetItemData != null && _targetItemData.UseLeftHandIK && _previewInstance != null)
        {
            Vector3 ikWorldPos = _ikTargetInstance != null
                ? _ikTargetInstance.transform.position
                : _previewInstance.transform.TransformPoint(_targetItemData.LeftHandIKLocalPosition);

            Gizmos.color = new Color(0f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(ikWorldPos, 0.035f);
            Gizmos.DrawLine(_previewInstance.transform.position, ikWorldPos);

#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = new Color(0f, 0.85f, 1f, 1f);
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            Handles.Label(ikWorldPos + Vector3.up * 0.05f, "✋ Left Hand IK Grip", style);
#endif
        }
    }

    private Transform FindHoldPointRecursive(Transform current)
    {
        if (current == null) return null;

        Transform hp3p = FindDeepChild(current, "HoldPoint3P");
        if (hp3p != null) return hp3p;

        Transform rh = FindDeepChild(current, "RightHand");
        if (rh != null) return rh;

        if (current.name == "HoldPoint") return current;

        for (int i = 0; i < current.childCount; i++)
        {
            var found = FindHoldPointRecursive(current.GetChild(i));
            if (found != null) return found;
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindDeepChild(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
