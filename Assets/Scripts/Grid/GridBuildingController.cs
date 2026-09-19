using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 그리드 상에서 마우스 포인팅 및 키 입력을 통해 오브젝트를 실시간 미리보기하고 설치 및 철거할 수 있는 컨트롤러입니다.
/// 설치 가능한 블록 아이템(PlaceableItem)을 손에 들었을 때 활성화되며,
/// 인게임 게임 뷰에 그리드 격자선(IGridVisualizer) 및 설치 프리뷰(GridPlacementPreview)를 실시간 렌더링합니다.
/// </summary>
[DisallowMultipleComponent]
public class GridBuildingController : MonoBehaviour
{
    public static GridBuildingController Instance { get; private set; }

    [Header("Visualizer & Preview Components")]
    [Tooltip("게임 뷰 그리드 라인 시각화 컴포넌트 (비워둘 경우 자동 탐색)")]
    [SerializeField] private MonoBehaviour _gridVisualizerComponent;

    [Tooltip("게임 뷰 설치 프리뷰 고스트 컴포넌트 (비워둘 경우 자동 탐색)")]
    [SerializeField] private GridPlacementPreview _placementPreview;

    [Header("Raycast Settings")]
    [Tooltip("지면 판정을 위한 레이어 마스크")]
    [SerializeField] private LayerMask _groundLayerMask = ~0;

    [Tooltip("레이캐스트 최대 거리")]
    [SerializeField] private float _maxRaycastDistance = 100f;

    [Header("Building State")]
    [SerializeField] private bool _isBuildModeActive = false;

    private IGridVisualizer _gridVisualizer;
    private Camera _targetCamera;
    private PlaceableItem _activePlaceableItem;
    private PlaceableObject _currentPrefab;
    private int _currentRotationAngle = 0;
    private Vector2Int _currentGridCoord;
    private bool _hasValidGroundHit;
    private bool _canPlaceAtCurrentCoord;

    public bool IsBuildModeActive => _isBuildModeActive;
    public PlaceableObject CurrentPrefab => _currentPrefab;
    public PlaceableItem ActivePlaceableItem => _activePlaceableItem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _targetCamera = Camera.main;

        InitializeComponents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializeComponents()
    {
        if (_gridVisualizerComponent is IGridVisualizer visualizer)
        {
            _gridVisualizer = visualizer;
        }
        else
        {
            _gridVisualizer = GetComponentInChildren<IGridVisualizer>();
            if (_gridVisualizer == null)
            {
                var vis = gameObject.AddComponent<ShaderGridVisualizer>();
                _gridVisualizer = vis;
                _gridVisualizerComponent = vis;
            }
        }

        if (_placementPreview == null)
        {
            _placementPreview = GetComponentInChildren<GridPlacementPreview>();
            if (_placementPreview == null)
            {
                _placementPreview = gameObject.AddComponent<GridPlacementPreview>();
            }
        }
    }

    /// <summary>
    /// 플레이어가 설치 가능한 블록 아이템을 들었을 때 건설 모드를 활성화합니다.
    /// </summary>
    public void StartBuilding(PlaceableItem placeableItem)
    {
        if (placeableItem == null || placeableItem.BuildingPrefab == null)
        {
            StopBuilding();
            return;
        }

        _activePlaceableItem = placeableItem;
        _currentPrefab = placeableItem.BuildingPrefab;
        _isBuildModeActive = true;
        _currentRotationAngle = 0;

        if (_gridVisualizer == null)
        {
            InitializeComponents();
        }

        // 1. 게임 뷰 그리드 격자 활성화
        if (_gridVisualizer != null)
        {
            _gridVisualizer.ShowGrid();
        }

        // 2. 게임 뷰 프리뷰 고스트 활성화
        if (_placementPreview != null)
        {
            _placementPreview.Show(_currentPrefab);
        }

        Debug.Log($"[GridBuildingController] 건설 모드 활성화: {_currentPrefab.DisplayName} (수량: {placeableItem.Amount})");
    }

    /// <summary>
    /// 블록 아이템을 내려놓거나 소모했을 때 건설 모드를 비활성화합니다.
    /// </summary>
    public void StopBuilding()
    {
        _isBuildModeActive = false;
        _activePlaceableItem = null;
        _currentPrefab = null;

        if (_gridVisualizer != null)
        {
            _gridVisualizer.HideGrid();
        }

        if (_placementPreview != null)
        {
            _placementPreview.Hide();
        }
    }

    private void Update()
    {
        if (!_isBuildModeActive || _currentPrefab == null)
        {
            return;
        }

        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
            if (_targetCamera == null)
            {
                return;
            }
        }

        UpdateRaycastAndGridCoord();
        UpdateInGameVisuals();

        HandleRotationInput();
        HandlePlacementInput();
    }

    /// <summary>
    /// 마우스 포인터 레이캐스트를 수행하여 현재 가리키는 그리드 좌표 및 설치 가능 여부를 갱신합니다.
    /// 마우스 커서가 잠겨있는(1인칭 화면) 경우 화면 중앙을, 그렇지 않으면 마우스 커서 위치를 기준으로 레이를 발사합니다.
    /// </summary>
    private void UpdateRaycastAndGridCoord()
    {
        if (WorldGridManager.Instance == null)
        {
            _hasValidGroundHit = false;
            return;
        }

        Ray ray;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = _targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
        else
        {
            Vector2 mousePosition = Vector2.zero;
            if (Mouse.current != null)
            {
                mousePosition = Mouse.current.position.ReadValue();
            }
            ray = _targetCamera.ScreenPointToRay(mousePosition);
        }

        if (Physics.Raycast(ray, out RaycastHit hit, _maxRaycastDistance, _groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            _hasValidGroundHit = true;
            _currentGridCoord = WorldGridManager.Instance.WorldToGridCoordinate(hit.point);
        }
        else
        {
            // 지면 콜라이더가 없을 경우 XZ 바닥 평면(Y = WorldGridManager Origin Y)과 교차 계산
            Plane groundPlane = new Plane(Vector3.up, WorldGridManager.Instance.GridOrigin);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                _hasValidGroundHit = true;
                _currentGridCoord = WorldGridManager.Instance.WorldToGridCoordinate(hitPoint);
            }
            else
            {
                _hasValidGroundHit = false;
            }
        }

        if (_hasValidGroundHit && _currentPrefab != null)
        {
            _canPlaceAtCurrentCoord = WorldGridManager.Instance.CanPlaceObject(_currentPrefab, _currentGridCoord, _currentRotationAngle);
        }
        else
        {
            _canPlaceAtCurrentCoord = false;
        }
    }

    /// <summary>
    /// 게임 뷰에 표시되는 그리드 격자선 위치 및 고스트 프리뷰를 갱신합니다.
    /// </summary>
    private void UpdateInGameVisuals()
    {
        if (WorldGridManager.Instance == null || !_hasValidGroundHit || _currentPrefab == null)
        {
            if (_placementPreview != null && _placementPreview.IsVisible)
            {
                _placementPreview.Hide();
            }
            return;
        }

        Vector3 cameraPos = _targetCamera != null ? _targetCamera.transform.position : transform.position;

        // 1. 그리드 격자 범위 갱신
        if (_gridVisualizer != null)
        {
            _gridVisualizer.UpdateVisualizer(cameraPos, 18f);
        }

        // 2. 프리뷰 고스트 갱신
        if (_placementPreview != null)
        {
            if (!_placementPreview.IsVisible)
            {
                _placementPreview.Show(_currentPrefab);
            }

            Vector2Int effectiveSize = _currentPrefab.GetRotatedSize(_currentRotationAngle);
            float cellSize = WorldGridManager.Instance.CellSize;
            Vector3 worldSpawnPos = WorldGridManager.Instance.GridOrigin + new Vector3(
                (_currentGridCoord.x + effectiveSize.x * 0.5f) * cellSize,
                0f,
                (_currentGridCoord.y + effectiveSize.y * 0.5f) * cellSize
            );

            Quaternion rotation = Quaternion.Euler(0f, PlaceableObject.NormalizeRotationAngle(_currentRotationAngle), 0f);

            _placementPreview.UpdateTransform(
                worldSpawnPos,
                rotation,
                _canPlaceAtCurrentCoord,
                effectiveSize,
                cellSize,
                WorldGridManager.Instance.GridOrigin,
                _currentGridCoord
            );
        }
    }

    /// <summary>
    /// R 키 입력 시 90도 회전 처리합니다.
    /// </summary>
    private void HandleRotationInput()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            _currentRotationAngle = (_currentRotationAngle + 90) % 360;
        }
    }

    /// <summary>
    /// 마우스 우클릭 시 현재 위치에 오브젝트를 설치하고 손의 아이템 수량을 소모합니다.
    /// </summary>
    private void HandlePlacementInput()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (!_hasValidGroundHit || _currentPrefab == null || WorldGridManager.Instance == null)
            {
                return;
            }

            if (_canPlaceAtCurrentCoord)
            {
                if (WorldGridManager.Instance.TryPlaceObject(_currentPrefab, _currentGridCoord, _currentRotationAngle, out PlaceableObject instance))
                {
                    Debug.Log($"[GridBuildingController] '{instance.DisplayName}' 설치 완료 (위치: {_currentGridCoord.x}, {_currentGridCoord.y})");

                    // 손에 든 아이템 수량 1 소모
                    if (_activePlaceableItem != null)
                    {
                        bool isEmpty = _activePlaceableItem.ConsumeOne();
                        if (isEmpty)
                        {
                            // 소지 수량이 0이 되면 아이템 파괴 및 건설 모드 종료
                            var pickable = _activePlaceableItem.Pickable;
                            StopBuilding();

                            if (pickable != null)
                            {
                                Destroy(pickable.gameObject);
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[GridBuildingController] ({_currentGridCoord.x}, {_currentGridCoord.y}) 위치에는 설치할 수 없습니다.");
            }
        }
    }
}
