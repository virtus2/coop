using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity LineRenderer를 사용하여 게임 뷰(인게임) 상에 실시간으로 그리드 격자선을 렌더링하는 컴포넌트입니다.
/// IGridVisualizer를 구현하며, 플레이어 주변 반경을 기준으로 격자선을 동적으로 풀링하여 효율적으로 그립니다.
/// </summary>
[DisallowMultipleComponent]
public class LineRendererGridVisualizer : MonoBehaviour, IGridVisualizer
{
    [Header("Line Visual Settings")]
    [Tooltip("그리드 라인에 적용할 머티리얼 (URP Unlit 권장)")]
    [SerializeField] private Material _lineMaterial;

    [Tooltip("라인의 기본 너비 (미터 단위)")]
    [SerializeField] private float _lineWidth = 0.03f;

    [Tooltip("그리드 격자 기본 색상")]
    [SerializeField] private Color _gridLineColor = new Color(0.85f, 0.95f, 1.0f, 0.45f);

    [Tooltip("지면과의 Z-Fighting을 방지하기 위한 Y축 오프셋")]
    [SerializeField] private float _verticalOffset = 0.02f;

    [Tooltip("시각화 기본 반경 (미터 단위)")]
    [SerializeField] private float _defaultVisualRadius = 15.0f;

    private readonly List<LineRenderer> _linePool = new List<LineRenderer>();
    private Transform _linesContainer;
    private bool _isVisible;
    private Vector3 _lastFocusPosition = new Vector3(float.MinValue, 0f, 0f);

    public bool IsVisible => _isVisible;

    private void Awake()
    {
        EnsureMaterial();
        CreateLinesContainer();
    }

    private void EnsureMaterial()
    {
        if (_lineMaterial == null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader != null)
            {
                _lineMaterial = new Material(unlitShader);
                _lineMaterial.color = _gridLineColor;
            }
        }
    }

    private void OnDestroy()
    {
        ClearLines();
    }

    private void CreateLinesContainer()
    {
        if (_linesContainer != null)
        {
            return;
        }

        var go = new GameObject("GridLines_Container");
        go.transform.SetParent(transform, false);
        _linesContainer = go.transform;
        _linesContainer.gameObject.SetActive(false);
    }

    /// <summary>
    /// 그리드 표시를 활성화합니다.
    /// </summary>
    public void ShowGrid()
    {
        _isVisible = true;
        if (_linesContainer != null)
        {
            _linesContainer.gameObject.SetActive(true);
        }
        _lastFocusPosition = new Vector3(float.MinValue, 0f, 0f);
    }

    /// <summary>
    /// 그리드 표시를 비활성화합니다.
    /// </summary>
    public void HideGrid()
    {
        _isVisible = false;
        if (_linesContainer != null)
        {
            _linesContainer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 플레이어 위치를 기준으로 반경 내의 그리드 선들을 동적으로 계산하고 갱신합니다.
    /// </summary>
    public void UpdateVisualizer(Vector3 focusPosition, float visualRadius = -1f)
    {
        if (!_isVisible || WorldGridManager.Instance == null)
        {
            return;
        }

        float radius = visualRadius > 0f ? visualRadius : _defaultVisualRadius;

        // 위치 변화가 미미하면 재계산 생략
        if (Vector3.Distance(_lastFocusPosition, focusPosition) < 0.2f)
        {
            return;
        }

        _lastFocusPosition = focusPosition;
        RenderGridLines(focusPosition, radius);
    }

    private void RenderGridLines(Vector3 center, float radius)
    {
        WorldGridManager gridManager = WorldGridManager.Instance;
        float cellSize = gridManager.CellSize;
        Vector3 origin = gridManager.GridOrigin;
        int maxGridX = gridManager.GridWidth;
        int maxGridZ = gridManager.GridLength;

        // 월드 좌표 바운드 계산
        float minWorldX = Mathf.Max(origin.x, center.x - radius);
        float maxWorldX = Mathf.Min(origin.x + maxGridX * cellSize, center.x + radius);
        float minWorldZ = Mathf.Max(origin.z, center.z - radius);
        float maxWorldZ = Mathf.Min(origin.z + maxGridZ * cellSize, center.z + radius);

        if (minWorldX >= maxWorldX || minWorldZ >= maxWorldZ)
        {
            DisableUnusedLines(0);
            return;
        }

        // 그리드 인덱스 범위 계산
        int startCellX = Mathf.FloorToInt((minWorldX - origin.x) / cellSize);
        int endCellX = Mathf.CeilToInt((maxWorldX - origin.x) / cellSize);
        int startCellZ = Mathf.FloorToInt((minWorldZ - origin.z) / cellSize);
        int endCellZ = Mathf.CeilToInt((maxWorldZ - origin.z) / cellSize);

        startCellX = Mathf.Clamp(startCellX, 0, maxGridX);
        endCellX = Mathf.Clamp(endCellX, 0, maxGridX);
        startCellZ = Mathf.Clamp(startCellZ, 0, maxGridZ);
        endCellZ = Mathf.Clamp(endCellZ, 0, maxGridZ);

        float lineY = origin.y + _verticalOffset;
        int lineIndex = 0;

        // 1. Z축 방향으로 뻗는 선들 (X 고정)
        for (int x = startCellX; x <= endCellX; x++)
        {
            float lineX = origin.x + x * cellSize;
            Vector3 p0 = new Vector3(lineX, lineY, origin.z + startCellZ * cellSize);
            Vector3 p1 = new Vector3(lineX, lineY, origin.z + endCellZ * cellSize);

            SetLine(lineIndex++, p0, p1);
        }

        // 2. X축 방향으로 뻗는 선들 (Z 고정)
        for (int z = startCellZ; z <= endCellZ; z++)
        {
            float lineZ = origin.z + z * cellSize;
            Vector3 p0 = new Vector3(origin.x + startCellX * cellSize, lineY, lineZ);
            Vector3 p1 = new Vector3(origin.x + endCellX * cellSize, lineY, lineZ);

            SetLine(lineIndex++, p0, p1);
        }

        DisableUnusedLines(lineIndex);
    }

    private void SetLine(int index, Vector3 start, Vector3 end)
    {
        LineRenderer lr = GetOrCreateLineRenderer(index);
        lr.gameObject.SetActive(true);
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }

    private LineRenderer GetOrCreateLineRenderer(int index)
    {
        while (_linePool.Count <= index)
        {
            CreateLinesContainer();
            var lineGo = new GameObject($"GridLine_{_linePool.Count}");
            lineGo.transform.SetParent(_linesContainer, false);

            LineRenderer lr = lineGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = _lineWidth;
            lr.endWidth = _lineWidth;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            if (_lineMaterial != null)
            {
                lr.material = _lineMaterial;
            }

            lr.startColor = _gridLineColor;
            lr.endColor = _gridLineColor;

            _linePool.Add(lr);
        }

        return _linePool[index];
    }

    private void DisableUnusedLines(int usedCount)
    {
        for (int i = usedCount; i < _linePool.Count; i++)
        {
            if (_linePool[i] != null && _linePool[i].gameObject.activeSelf)
            {
                _linePool[i].gameObject.SetActive(false);
            }
        }
    }

    private void ClearLines()
    {
        for (int i = 0; i < _linePool.Count; i++)
        {
            if (_linePool[i] != null)
            {
                Destroy(_linePool[i].gameObject);
            }
        }
        _linePool.Clear();
    }
}
