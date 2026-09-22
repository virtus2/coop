#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// 에디터 씬 상에서 ItemData(GunItemData 등)의 손 소켓 부착 위치, 회전, 스케일을
/// 유니티 트랜스폼 기즈모(W, E, R)로 실시간 조작하고 ItemData에 저장할 수 있도록 지원하는 헬퍼 컴포넌트입니다.
/// </summary>
[ExecuteAlways]
public class ItemHoldOffsetTweaker : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("손 부착 위치/회전/스케일을 조절할 ItemData (GunItemData 등)")]
    [SerializeField] private ItemData _targetItemData;

    [Tooltip("총기/아이템이 부착될 기준 소켓 Transform (비어있으면 'HoldPoint' 이름을 자동 탐색)")]
    [SerializeField] private Transform _holdPoint;

    [Tooltip("1인칭 시점 프리뷰 카메라")]
    [SerializeField] private Camera _previewCamera;

    [Header("Live Preview Object")]
    [SerializeField] private GameObject _previewInstance;

    private const string PREVIEW_OBJECT_NAME = "[Preview] HeldItemVisual";

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
                _holdPoint = FindHoldPointRecursive(transform);
            }
            return _holdPoint != null ? _holdPoint : transform;
        }
        set => _holdPoint = value;
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
        // 씬 전환이나 비활성화 시 임시 프리뷰 정리
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
    /// <param name="resetTransformToItemData">true면 ItemData에 저장된 포지션/로테이션/스케일로 트랜스폼을 리셋합니다.</param>
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

        // 메쉬 및 머티리얼 추출
        Mesh mesh = _targetItemData.HeldMesh;
        Material mat = _targetItemData.HeldMaterial;

        // Fallback: WorldPrefab에서 메쉬/머티리얼 탐색
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

    /// <summary>
    /// 대상 ItemData에 저장되어 있는 위치, 회전, 스케일을 현재 프리뷰 오브젝트에 적용합니다.
    /// </summary>
    public void LoadFromItemData()
    {
        if (_targetItemData == null || _previewInstance == null) return;

        _previewInstance.transform.localPosition = _targetItemData.HeldLocalPosition;
        _previewInstance.transform.localRotation = Quaternion.Euler(_targetItemData.HeldLocalRotation);
        _previewInstance.transform.localScale = _targetItemData.HeldLocalScale;

        Debug.Log($"[ItemHoldOffsetTweaker] '{_targetItemData.ItemName}'의 저장된 오프셋을 프리뷰에 적용했습니다.");
    }

#if UNITY_EDITOR
    /// <summary>
    /// 현재 프리뷰 오브젝트의 로컬 위치, 회전, 스케일을 대상 ItemData에 영구 저장합니다.
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

        SerializedProperty posProp = so.FindProperty("_heldLocalPosition");
        SerializedProperty rotProp = so.FindProperty("_heldLocalRotation");
        SerializedProperty scaleProp = so.FindProperty("_heldLocalScale");

        if (posProp != null) posProp.vector3Value = currentPos;
        if (rotProp != null) rotProp.vector3Value = currentRot;
        if (scaleProp != null) scaleProp.vector3Value = currentScale;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_targetItemData);
        AssetDatabase.SaveAssets();

        Debug.Log($"<color=#4CAF50><b>[ItemHoldOffsetTweaker] '{_targetItemData.ItemName}' 저장 완료!</b></color>\n" +
                  $"Position: {currentPos}\n" +
                  $"Rotation: {currentRot}\n" +
                  $"Scale: {currentScale}");
    }

    public void SelectPreviewObject()
    {
        if (_previewInstance != null)
        {
            Selection.activeGameObject = _previewInstance;
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
                _previewInstance = new GameObject(PREVIEW_OBJECT_NAME);
                _previewInstance.transform.SetParent(parentSocket, false);
                _previewInstance.transform.localPosition = _targetItemData != null ? _targetItemData.HeldLocalPosition : Vector3.zero;
                _previewInstance.transform.localRotation = _targetItemData != null ? Quaternion.Euler(_targetItemData.HeldLocalRotation) : Quaternion.identity;
                _previewInstance.transform.localScale = _targetItemData != null ? _targetItemData.HeldLocalScale : Vector3.one;
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
    }

    public void CleanupPreviewInstance()
    {
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

    private Transform FindHoldPointRecursive(Transform current)
    {
        if (current == null) return null;
        if (current.name == "HoldPoint") return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindHoldPointRecursive(current.GetChild(i));
            if (found != null) return found;
        }

        return null;
    }
}
