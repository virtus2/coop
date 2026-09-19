using UnityEngine;

/// <summary>
/// 바닥 쿼드(Quad)와 커스텀 URP 그리드 셰이더(WorldGridShader)를 사용하여,
/// 단 1회의 드로우콜로 부드러운 안티에일리어싱과 거리 감쇄가 적용된 그리드를 렌더링하는 컴포넌트입니다.
/// IGridVisualizer를 구현하여 시스템과 완벽하게 호환됩니다.
/// </summary>
[DisallowMultipleComponent]
public class ShaderGridVisualizer : MonoBehaviour, IGridVisualizer
{
    [Header("Shader & Material Settings")]
    [Tooltip("그리드 렌더링용 머티리얼 (비워둘 경우 Custom/WorldGridShader 기반으로 자동 생성)")]
    [SerializeField] private Material _gridMaterial;

    [Tooltip("그리드 격자 라인 색상")]
    [SerializeField] private Color _lineColor = new Color(0.8f, 0.92f, 1.0f, 0.6f);

    [Tooltip("셀 내부 배경 은은한 채우기 색상")]
    [SerializeField] private Color _fillColor = new Color(0.2f, 0.6f, 1.0f, 0.02f);

    [Tooltip("격자선 두께 (미터 단위)")]
    [SerializeField] private float _lineWidth = 0.035f;

    [Tooltip("플레이어 주변 시각화 반경 (미터 단위)")]
    [SerializeField] private float _fadeRadius = 16.0f;

    [Tooltip("가장자리 부드러운 페이드아웃 감쇄 폭")]
    [SerializeField] private float _fadeFalloff = 4.0f;

    [Tooltip("지면과의 Z-Fighting을 방지하기 위한 Y축 오프셋")]
    [SerializeField] private float _verticalOffset = 0.015f;

    private GameObject _quadObject;
    private MeshRenderer _quadRenderer;
    private Material _materialInstance;
    private bool _isVisible;

    private static readonly int PropGridOrigin = Shader.PropertyToID("_GridOrigin");
    private static readonly int PropCellSize = Shader.PropertyToID("_CellSize");
    private static readonly int PropLineWidth = Shader.PropertyToID("_LineWidth");
    private static readonly int PropLineColor = Shader.PropertyToID("_LineColor");
    private static readonly int PropFillColor = Shader.PropertyToID("_FillColor");
    private static readonly int PropFocusPosition = Shader.PropertyToID("_FocusPosition");
    private static readonly int PropFadeRadius = Shader.PropertyToID("_FadeRadius");
    private static readonly int PropFadeFalloff = Shader.PropertyToID("_FadeFalloff");

    public bool IsVisible => _isVisible;

    private void Awake()
    {
        EnsureQuad();
        HideGrid();
    }

    private void OnDestroy()
    {
        if (_quadObject != null)
        {
            Destroy(_quadObject);
        }

        if (_materialInstance != null)
        {
            Destroy(_materialInstance);
        }
    }

    private void EnsureQuad()
    {
        if (_quadObject != null)
        {
            return;
        }

        // 1. 단일 쿼드 오브젝트 생성
        _quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quadObject.name = "ShaderGrid_Plane";
        _quadObject.transform.SetParent(transform, false);

        // 콜라이더 제거 (물리 및 시선 간섭 방지)
        Collider col = _quadObject.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        // XZ 바닥에 평평하게 눕힘 (기본 쿼드는 XY)
        _quadObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // 2. 머티리얼 구성
        EnsureMaterialInstance();

        _quadRenderer = _quadObject.GetComponent<MeshRenderer>();
        _quadRenderer.sharedMaterial = _materialInstance;
        _quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _quadRenderer.receiveShadows = false;
        _quadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        // 3. 그리드 바운드에 맞춘 위치 및 크기 조정
        UpdateQuadTransform();
    }

    private void EnsureMaterialInstance()
    {
        if (_materialInstance != null)
        {
            return;
        }

        if (_gridMaterial != null)
        {
            _materialInstance = new Material(_gridMaterial);
        }
        else
        {
            Shader gridShader = Shader.Find("Custom/WorldGridShader");
            if (gridShader == null)
            {
                gridShader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            _materialInstance = new Material(gridShader);
        }

        UpdateMaterialParameters();
    }

    private void UpdateMaterialParameters()
    {
        if (_materialInstance == null)
        {
            return;
        }

        _materialInstance.SetColor(PropLineColor, _lineColor);
        _materialInstance.SetColor(PropFillColor, _fillColor);
        _materialInstance.SetFloat(PropLineWidth, _lineWidth);
        _materialInstance.SetFloat(PropFadeRadius, _fadeRadius);
        _materialInstance.SetFloat(PropFadeFalloff, _fadeFalloff);

        if (WorldGridManager.Instance != null)
        {
            _materialInstance.SetVector(PropGridOrigin, WorldGridManager.Instance.GridOrigin);
            _materialInstance.SetFloat(PropCellSize, WorldGridManager.Instance.CellSize);
        }
    }

    private void UpdateQuadTransform()
    {
        if (_quadObject == null || WorldGridManager.Instance == null)
        {
            return;
        }

        WorldGridManager gm = WorldGridManager.Instance;
        float totalWidth = gm.GridWidth * gm.CellSize;
        float totalLength = gm.GridLength * gm.CellSize;

        // 그리드 전체 영역의 중앙에 위치
        Vector3 centerPos = gm.GridOrigin + new Vector3(totalWidth * 0.5f, _verticalOffset, totalLength * 0.5f);
        _quadObject.transform.position = centerPos;
        _quadObject.transform.localScale = new Vector3(totalWidth, totalLength, 1f);
    }

    /// <summary>
    /// 그리드 표시를 활성화합니다.
    /// </summary>
    public void ShowGrid()
    {
        EnsureQuad();
        UpdateQuadTransform();
        UpdateMaterialParameters();

        _isVisible = true;
        if (_quadObject != null)
        {
            _quadObject.SetActive(true);
        }
    }

    /// <summary>
    /// 그리드 표시를 비활성화합니다.
    /// </summary>
    public void HideGrid()
    {
        _isVisible = false;
        if (_quadObject != null)
        {
            _quadObject.SetActive(false);
        }
    }

    /// <summary>
    /// 플레이어 또는 시선 위치를 셰이더에 전달하여 중심부 그리드를 렌더링하고 거리에 따라 감쇄합니다.
    /// </summary>
    public void UpdateVisualizer(Vector3 focusPosition, float visualRadius = -1f)
    {
        if (!_isVisible || _materialInstance == null)
        {
            return;
        }

        if (visualRadius > 0f)
        {
            _materialInstance.SetFloat(PropFadeRadius, visualRadius);
        }

        _materialInstance.SetVector(PropFocusPosition, new Vector4(focusPosition.x, focusPosition.y, focusPosition.z, 1f));

        if (WorldGridManager.Instance != null)
        {
            _materialInstance.SetVector(PropGridOrigin, WorldGridManager.Instance.GridOrigin);
            _materialInstance.SetFloat(PropCellSize, WorldGridManager.Instance.CellSize);
        }
    }
}
