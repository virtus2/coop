using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 뷰 상에서 설치 대상 오브젝트의 반투명 프리뷰(Ghost) 및 설치 가능/불가능 색상 피드백을 실시간으로 표시하는 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public class GridPlacementPreview : MonoBehaviour
{
    [Header("Preview Materials")]
    [Tooltip("설치 가능한 위치일 때 적용할 반투명 머티리얼 (예: 초록색)")]
    [SerializeField] private Material _validMaterial;

    [Tooltip("설치 불가능한 위치일 때 적용할 반투명 머티리얼 (예: 붉은색)")]
    [SerializeField] private Material _invalidMaterial;

    [Header("Cell Outline Highlight")]
    [Tooltip("바닥에 표시할 설치 영역 사각 라인 두께")]
    [SerializeField] private float _outlineWidth = 0.04f;

    [Tooltip("바닥 라인 머티리얼")]
    [SerializeField] private Material _outlineMaterial;

    private GameObject _ghostInstance;
    private PlaceableObject _currentPrefab;
    private readonly List<Renderer> _ghostRenderers = new List<Renderer>();
    private LineRenderer _groundOutline;
    private bool _isVisible;
    private bool _lastCanPlaceState = true;

    public bool IsVisible => _isVisible;

    private void Awake()
    {
        EnsureMaterials();
        CreateOutlineRenderer();
    }

    private void EnsureMaterials()
    {
        if (_validMaterial == null)
        {
            _validMaterial = CreateFallbackTransparentMaterial(new Color(0f, 1f, 0.4f, 0.45f));
        }

        if (_invalidMaterial == null)
        {
            _invalidMaterial = CreateFallbackTransparentMaterial(new Color(1f, 0.15f, 0.15f, 0.45f));
        }

        if (_outlineMaterial == null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader != null)
            {
                _outlineMaterial = new Material(unlitShader);
            }
        }
    }

    private Material CreateFallbackTransparentMaterial(Color color)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        var mat = new Material(urpLit);
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        return mat;
    }

    private void OnDestroy()
    {
        ClearGhost();
    }

    private void CreateOutlineRenderer()
    {
        if (_groundOutline != null)
        {
            return;
        }

        var outlineGo = new GameObject("Placement_Outline");
        outlineGo.transform.SetParent(transform, false);

        _groundOutline = outlineGo.AddComponent<LineRenderer>();
        _groundOutline.positionCount = 5; // 사각형 닫힌 루프 (5점)
        _groundOutline.startWidth = _outlineWidth;
        _groundOutline.endWidth = _outlineWidth;
        _groundOutline.useWorldSpace = true;
        _groundOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _groundOutline.receiveShadows = false;
        _groundOutline.loop = true;

        if (_outlineMaterial != null)
        {
            _groundOutline.material = _outlineMaterial;
        }

        _groundOutline.gameObject.SetActive(false);
    }

    /// <summary>
    /// 지정된 프리팹으로 프리뷰 고스트를 생성하고 시각화를 활성화합니다.
    /// </summary>
    public void Show(PlaceableObject prefab)
    {
        if (prefab == null)
        {
            Hide();
            return;
        }

        if (_currentPrefab != prefab || _ghostInstance == null)
        {
            RecreateGhost(prefab);
        }

        _isVisible = true;
        if (_ghostInstance != null)
        {
            _ghostInstance.SetActive(true);
        }
        if (_groundOutline != null)
        {
            _groundOutline.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 프리뷰 고스트 및 바닥 테두리를 숨깁니다.
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        if (_ghostInstance != null)
        {
            _ghostInstance.SetActive(false);
        }
        if (_groundOutline != null)
        {
            _groundOutline.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 프리뷰의 위치, 각도 및 설치 가능 상태를 갱신합니다.
    /// </summary>
    public void UpdateTransform(Vector3 worldPos, Quaternion rotation, bool canPlace, Vector2Int effectiveSize, float cellSize, Vector3 gridOrigin, Vector2Int originCoord)
    {
        if (!_isVisible || _ghostInstance == null)
        {
            return;
        }

        _ghostInstance.transform.SetPositionAndRotation(worldPos, rotation);

        // 설치 가능 상태에 따라 머티리얼 및 라인 색상 전환
        ApplyMaterialState(canPlace);

        // 바닥 테두리 갱신
        UpdateGroundOutline(originCoord, effectiveSize, cellSize, gridOrigin, canPlace);
    }

    private void ApplyMaterialState(bool canPlace)
    {
        _lastCanPlaceState = canPlace;
        Material targetMat = canPlace ? _validMaterial : _invalidMaterial;

        if (targetMat != null)
        {
            for (int i = 0; i < _ghostRenderers.Count; i++)
            {
                if (_ghostRenderers[i] != null)
                {
                    _ghostRenderers[i].sharedMaterial = targetMat;
                }
            }
        }
    }

    private void UpdateGroundOutline(Vector2Int originCoord, Vector2Int effectiveSize, float cellSize, Vector3 gridOrigin, bool canPlace)
    {
        if (_groundOutline == null)
        {
            return;
        }

        float y = gridOrigin.y + 0.03f; // 그리드 선보다 살짝 위
        float minX = gridOrigin.x + originCoord.x * cellSize;
        float maxX = minX + effectiveSize.x * cellSize;
        float minZ = gridOrigin.z + originCoord.y * cellSize;
        float maxZ = minZ + effectiveSize.y * cellSize;

        _groundOutline.SetPosition(0, new Vector3(minX, y, minZ));
        _groundOutline.SetPosition(1, new Vector3(maxX, y, minZ));
        _groundOutline.SetPosition(2, new Vector3(maxX, y, maxZ));
        _groundOutline.SetPosition(3, new Vector3(minX, y, maxZ));
        _groundOutline.SetPosition(4, new Vector3(minX, y, minZ));

        Color outlineColor = canPlace ? new Color(0f, 1f, 0.4f, 0.9f) : new Color(1f, 0.2f, 0.2f, 0.9f);
        _groundOutline.startColor = outlineColor;
        _groundOutline.endColor = outlineColor;
    }

    private void RecreateGhost(PlaceableObject prefab)
    {
        ClearGhost();
        _currentPrefab = prefab;

        _ghostInstance = Instantiate(prefab.gameObject, transform);
        _ghostInstance.name = $"PreviewGhost_{prefab.name}";

        // 프리뷰 고스트의 모든 충돌체, 물리, 스크립트 비활성화/제거
        Collider[] colliders = _ghostInstance.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            Destroy(col);
        }

        Rigidbody[] rigidbodies = _ghostInstance.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rigidbodies)
        {
            Destroy(rb);
        }

        MonoBehaviour[] scripts = _ghostInstance.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in scripts)
        {
            if (script != this)
            {
                Destroy(script);
            }
        }

        _ghostRenderers.Clear();
        _ghostRenderers.AddRange(_ghostInstance.GetComponentsInChildren<Renderer>(true));

        ApplyMaterialState(true);
    }

    private void ClearGhost()
    {
        if (_ghostInstance != null)
        {
            Destroy(_ghostInstance);
            _ghostInstance = null;
        }
        _ghostRenderers.Clear();
        _currentPrefab = null;
    }
}
