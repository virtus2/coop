using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 그리드 상에서 마우스 포인팅 및 키 입력을 통해 오브젝트를 실시간 미리보기하고 설치 및 철거할 수 있는 컨트롤러입니다.
/// New Input System을 사용하여 마우스 클릭 및 R키(회전) 입력을 처리합니다.
/// </summary>
[DisallowMultipleComponent]
public class GridBuildingController : MonoBehaviour
{
    [Header("Placement Configuration")]
    [Tooltip("배치할 수 있는 오브젝트 프리팹 목록")]
    [SerializeField] private List<PlaceableObject> _placeablePrefabs = new List<PlaceableObject>();

    [Tooltip("지면 판정을 위한 레이어 마스크 (설정하지 않으면 모든 콜라이더를 대상으로 함)")]
    [SerializeField] private LayerMask _groundLayerMask = ~0;

    [Tooltip("레이캐스트 최대 거리")]
    [SerializeField] private float _maxRaycastDistance = 100f;

    [Header("Preview Colors")]
    [SerializeField] private Color _canBuildColor = new Color(0f, 1f, 0f, 0.4f);
    [SerializeField] private Color _cannotBuildColor = new Color(1f, 0f, 0f, 0.4f);

    [Header("Controls / Interaction")]
    [Tooltip("빌드 모드 활성화 여부")]
    [SerializeField] private bool _isBuildModeActive = true;

    private Camera _targetCamera;
    private int _selectedPrefabIndex = 0;
    private int _currentRotationAngle = 0;
    private Vector2Int _currentGridCoord;
    private bool _hasValidGroundHit;
    private bool _canPlaceAtCurrentCoord;

    public bool IsBuildModeActive
    {
        get => _isBuildModeActive;
        set => _isBuildModeActive = value;
    }

    public PlaceableObject CurrentPrefab => (_placeablePrefabs != null && _placeablePrefabs.Count > _selectedPrefabIndex)
        ? _placeablePrefabs[_selectedPrefabIndex]
        : null;

    private void Awake()
    {
        _targetCamera = Camera.main;
    }

    private void Update()
    {
        if (!_isBuildModeActive)
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
        HandlePrefabSelectionInput();
        HandleRotationInput();
        HandlePlacementInput();
        HandleDemolishInput();
    }

    /// <summary>
    /// 마우스 포인터 레이캐스트를 수행하여 현재 마우스가 가리키는 그리드 좌표 및 설치 가능 여부를 갱신합니다.
    /// 마우스 커서가 잠겨있는(1인칭 모드) 경우 화면 중앙을, 그렇지 않으면 마우스 커서 위치를 기준으로 레이를 발사합니다.
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

        if (Physics.Raycast(ray, out RaycastHit hit, _maxRaycastDistance, _groundLayerMask))
        {
            _hasValidGroundHit = true;
            _currentGridCoord = WorldGridManager.Instance.WorldToGridCoordinate(hit.point);

            PlaceableObject prefab = CurrentPrefab;
            if (prefab != null)
            {
                _canPlaceAtCurrentCoord = WorldGridManager.Instance.CanPlaceObject(prefab, _currentGridCoord, _currentRotationAngle);
            }
            else
            {
                _canPlaceAtCurrentCoord = false;
            }
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

                PlaceableObject prefab = CurrentPrefab;
                if (prefab != null)
                {
                    _canPlaceAtCurrentCoord = WorldGridManager.Instance.CanPlaceObject(prefab, _currentGridCoord, _currentRotationAngle);
                }
                else
                {
                    _canPlaceAtCurrentCoord = false;
                }
            }
            else
            {
                _hasValidGroundHit = false;
            }
        }
    }

    /// <summary>
    /// 숫자 키(1~9)를 눌러 배치할 프리팹을 빠르게 선택합니다.
    /// </summary>
    private void HandlePrefabSelectionInput()
    {
        if (Keyboard.current == null || _placeablePrefabs == null || _placeablePrefabs.Count == 0)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectPrefabIndex(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectPrefabIndex(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectPrefabIndex(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectPrefabIndex(3);
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) SelectPrefabIndex(4);
    }

    public void SelectPrefabIndex(int index)
    {
        if (_placeablePrefabs != null && index >= 0 && index < _placeablePrefabs.Count)
        {
            _selectedPrefabIndex = index;
            Debug.Log($"[GridBuildingController] 선택된 오브젝트: {_placeablePrefabs[index].DisplayName} (슬롯 {index + 1})");
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
    /// 마우스 좌클릭 시 현재 위치에 오브젝트를 설치합니다.
    /// </summary>
    private void HandlePlacementInput()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!_hasValidGroundHit || CurrentPrefab == null || WorldGridManager.Instance == null)
            {
                return;
            }

            if (_canPlaceAtCurrentCoord)
            {
                if (WorldGridManager.Instance.TryPlaceObject(CurrentPrefab, _currentGridCoord, _currentRotationAngle, out PlaceableObject instance))
                {
                    Debug.Log($"[GridBuildingController] '{instance.DisplayName}' 오브젝트가 그리드 ({_currentGridCoord.x}, {_currentGridCoord.y})에 성공적으로 설치되었습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"[GridBuildingController] 그리드 ({_currentGridCoord.x}, {_currentGridCoord.y})에는 이미 설치되어 있거나 범위를 벗어나 설치할 수 없습니다!");
            }
        }
    }

    /// <summary>
    /// 마우스 우클릭 시 해당 그리드 위치의 오브젝트를 철거합니다.
    /// </summary>
    private void HandleDemolishInput()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (!_hasValidGroundHit || WorldGridManager.Instance == null)
            {
                return;
            }

            if (WorldGridManager.Instance.TryRemoveObjectAt(_currentGridCoord, out PlaceableObject removedObject))
            {
                Debug.Log($"[GridBuildingController] 그리드 ({_currentGridCoord.x}, {_currentGridCoord.y})의 '{removedObject.DisplayName}' 오브젝트를 철거했습니다.");
                Destroy(removedObject.gameObject);
            }
        }
    }

    /// <summary>
    /// 디버그 및 씬 뷰에서 현재 커서 위치의 프리뷰 박스를 렌더링합니다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_isBuildModeActive || !_hasValidGroundHit || WorldGridManager.Instance == null)
        {
            return;
        }

        PlaceableObject prefab = CurrentPrefab;
        Vector2Int effectiveSize = prefab != null ? prefab.GetRotatedSize(_currentRotationAngle) : Vector2Int.one;
        float cellSize = WorldGridManager.Instance.CellSize;

        Vector3 center = WorldGridManager.Instance.GridOrigin + new Vector3(
            (_currentGridCoord.x + effectiveSize.x * 0.5f) * cellSize,
            0.1f,
            (_currentGridCoord.y + effectiveSize.y * 0.5f) * cellSize
        );

        Vector3 size = new Vector3(effectiveSize.x * cellSize, 0.2f, effectiveSize.y * cellSize);

        Gizmos.color = _canPlaceAtCurrentCoord ? _canBuildColor : _cannotBuildColor;
        Gizmos.DrawCube(center, size);

        Gizmos.color = _canPlaceAtCurrentCoord ? Color.green : Color.red;
        Gizmos.DrawWireCube(center, size);
    }
}
