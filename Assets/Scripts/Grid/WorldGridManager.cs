using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 3D 월드의 XZ 평면을 기반으로 그리드 분할, 오브젝트 설치 및 점유 상태 관리,
/// 디버그 시각화(Gizmos)를 제공하는 매니저 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public class WorldGridManager : MonoBehaviour
{
    public static WorldGridManager Instance { get; private set; }

    [Header("Grid Dimensions")]
    [Tooltip("그리드의 X축 셀 개수 (가로 폭)")]
    [SerializeField] private int _gridWidth = 20;

    [Tooltip("그리드의 Z축 셀 개수 (세로/깊이)")]
    [SerializeField] private int _gridLength = 20;

    [Tooltip("개별 그리드 셀 한 칸의 실제 월드 크기(단위: 미터)")]
    [SerializeField] private float _cellSize = 1.0f;

    [Tooltip("그리드의 기준점(Origin) 위치 오프셋")]
    [SerializeField] private Vector3 _gridOriginOffset = Vector3.zero;

    [Header("Character Collision Settings")]
    [Tooltip("설치를 방해하는 캐릭터 및 생명체 레이어 마스크 (기본값: 모든 레이어 대상 검사 후 캐릭터 컴포넌트 유무 판정)")]
    [SerializeField] private LayerMask _characterLayerMask = ~0;

    [Tooltip("캐릭터 감지를 위한 검사 높이 (미터 단위)")]
    [SerializeField] private float _characterCheckHeight = 2.0f;

    [Tooltip("지면 콜라이더 오인 감지를 방지하기 위한 바닥 오프셋 (미터 단위)")]
    [SerializeField] private float _characterCheckBottomOffset = 0.1f;

    [Tooltip("그리드 경계선 살짝 접촉 시의 오차를 완화하기 위한 스킨 마진")]
    [SerializeField] private float _collisionSkinWidth = 0.05f;

    [Header("Debug Gizmo Visualization")]
    [Tooltip("씬 및 게임 뷰에서 그리드 기즈모 표시 여부")]
    [SerializeField] private bool _showGridDebug = true;

    [Tooltip("컴포넌트가 선택되었을 때만 기즈모를 표시할지 여부")]
    [SerializeField] private bool _showWhenSelectedOnly = false;

    [Tooltip("점유된 셀에 색상 큐브를 채워 표시할지 여부")]
    [SerializeField] private bool _showOccupiedFill = true;

    [Tooltip("그리드 격자 라인 색상")]
    [SerializeField] private Color _gridLineColor = new Color(0.8f, 0.8f, 0.8f, 0.35f);

    [Tooltip("그리드 바깥 테두리 색상")]
    [SerializeField] private Color _gridBorderColor = new Color(0.2f, 1f, 0.4f, 0.9f);

    [Tooltip("점유된 셀 표시 색상")]
    [SerializeField] private Color _occupiedCellColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    [Tooltip("빈 셀 채우기 표시 색상 (옵션)")]
    [SerializeField] private Color _emptyCellColor = new Color(0.2f, 0.8f, 1f, 0.05f);

    private GridCell[,] _gridCells;
    private readonly HashSet<PlaceableObject> _placedObjects = new HashSet<PlaceableObject>();

    public int GridWidth => _gridWidth;
    public int GridLength => _gridLength;
    public float CellSize => _cellSize;
    public Vector3 GridOrigin => transform.position + _gridOriginOffset;

    public event Action<PlaceableObject, Vector2Int> ObjectPlaced;
    public event Action<PlaceableObject, Vector2Int> ObjectRemoved;
    public event Action GridInitialized;

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
        InitializeGrid();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 그리드 데이터 배열을 초기화합니다.
    /// </summary>
    public void InitializeGrid()
    {
        _gridCells = new GridCell[_gridWidth, _gridLength];

        for (int x = 0; x < _gridWidth; x++)
        {
            for (int z = 0; z < _gridLength; z++)
            {
                _gridCells[x, z] = new GridCell(x, z);
            }
        }

        _placedObjects.Clear();
        GridInitialized?.Invoke();
    }

    /// <summary>
    /// 런타임에 그리드 크기나 셀 크기를 동적으로 재설정합니다.
    /// 기존 배치 오브젝트는 제거되거나 초기화됩니다.
    /// </summary>
    public void ResizeGrid(int newWidth, int newLength, float newCellSize)
    {
        _gridWidth = Mathf.Max(1, newWidth);
        _gridLength = Mathf.Max(1, newLength);
        _cellSize = Mathf.Max(0.1f, newCellSize);

        InitializeGrid();
    }

    /// <summary>
    /// 3D 월드 좌표를 정수형 그리드 셀 좌표(X, Z)로 변환합니다.
    /// </summary>
    public Vector2Int WorldToGridCoordinate(Vector3 worldPosition)
    {
        Vector3 relativePosition = worldPosition - GridOrigin;
        int x = Mathf.FloorToInt(relativePosition.x / _cellSize);
        int z = Mathf.FloorToInt(relativePosition.z / _cellSize);
        return new Vector2Int(x, z);
    }

    /// <summary>
    /// 그리드 셀 좌표를 3D 월드 좌표로 변환합니다.
    /// centered가 true이면 셀의 중심점, false이면 셀의 좌하단 모서리 위치를 반환합니다.
    /// </summary>
    public Vector3 GridToWorldPosition(Vector2Int coord, bool centered = true)
    {
        float offset = centered ? _cellSize * 0.5f : 0f;
        return GridOrigin + new Vector3(coord.x * _cellSize + offset, 0f, coord.y * _cellSize + offset);
    }

    /// <summary>
    /// 해당 좌표가 그리드 유효 범위 내에 있는지 확인합니다.
    /// </summary>
    public bool IsValidCoordinate(Vector2Int coord)
    {
        return coord.x >= 0 && coord.x < _gridWidth && coord.y >= 0 && coord.y < _gridLength;
    }

    /// <summary>
    /// 해당 셀 좌표가 이미 다른 오브젝트에 의해 점유되었는지 여부를 확인합니다.
    /// 유효하지 않은 좌표일 경우 점유된 것으로 간주(true)합니다.
    /// </summary>
    public bool IsCoordinateOccupied(Vector2Int coord)
    {
        if (!IsValidCoordinate(coord))
        {
            return true;
        }

        if (_gridCells == null)
        {
            return false;
        }

        return _gridCells[coord.x, coord.y].IsOccupied;
    }

    /// <summary>
    /// 특정 위치의 GridCell 객체를 가져옵니다.
    /// </summary>
    public GridCell GetCell(Vector2Int coord)
    {
        if (!IsValidCoordinate(coord) || _gridCells == null)
        {
            return null;
        }

        return _gridCells[coord.x, coord.y];
    }

    private readonly Collider[] _characterOverlapBuffer = new Collider[16];

    /// <summary>
    /// 오브젝트를 지정된 좌표 및 각도에 설치할 수 있는지 검사합니다.
    /// 모든 점유 대상 셀이 그리드 내부여야 하며, 이미 점유된 셀이 없고, 설치 영역 내에 캐릭터가 없어야 합니다.
    /// </summary>
    public bool CanPlaceObject(PlaceableObject placeable, Vector2Int originCoord, int rotationAngle = 0, bool checkCharacters = true)
    {
        if (placeable == null)
        {
            return false;
        }

        List<Vector2Int> coords = placeable.GetOccupiedCoordinates(originCoord, rotationAngle);

        for (int i = 0; i < coords.Count; i++)
        {
            Vector2Int c = coords[i];

            // 1. 그리드 범위 벗어남 체크
            if (!IsValidCoordinate(c))
            {
                return false;
            }

            // 2. 이미 점유된 셀 체크
            if (IsCoordinateOccupied(c))
            {
                return false;
            }
        }

        // 3. 해당 영역에 어떤 캐릭터라도 서 있는지 물리 쿼리 체크
        if (checkCharacters && IsCharacterInPlacementArea(placeable, originCoord, rotationAngle))
        {
            return false;
        }

        // 4. 금지 구역(NoBuildArea) 내 위치 여부 체크
        Vector2Int effectiveSize = placeable.GetRotatedSize(rotationAngle);
        Vector3 blockCenter = GridOrigin + new Vector3(
            (originCoord.x + effectiveSize.x * 0.5f) * _cellSize,
            1.0f,
            (originCoord.y + effectiveSize.y * 0.5f) * _cellSize
        );
        Vector3 blockExtents = new Vector3(effectiveSize.x * _cellSize * 0.5f, 1.0f, effectiveSize.y * _cellSize * 0.5f);
        if (NoBuildArea.IsInAnyNoBuildArea(blockCenter, blockExtents, out _))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 지정된 설치 좌표 및 회전 영역 내에 어떤 캐릭터라도 서 있는지 물리 쿼리(Physics.OverlapBoxNonAlloc)로 검사합니다.
    /// 플레이어, 몬스터, NPC 등의 충돌체가 있을 경우 true를 반환합니다.
    /// </summary>
    public bool IsCharacterInPlacementArea(PlaceableObject placeable, Vector2Int originCoord, int rotationAngle = 0)
    {
        if (placeable == null)
        {
            return false;
        }

        Vector2Int effectiveSize = placeable.GetRotatedSize(rotationAngle);

        float halfX = Mathf.Max(0.01f, (effectiveSize.x * _cellSize * 0.5f) - _collisionSkinWidth);
        float halfZ = Mathf.Max(0.01f, (effectiveSize.y * _cellSize * 0.5f) - _collisionSkinWidth);
        float actualHeight = Mathf.Max(0.1f, _characterCheckHeight - _characterCheckBottomOffset);
        float halfY = actualHeight * 0.5f;

        Vector3 center = GridOrigin + new Vector3(
            (originCoord.x + effectiveSize.x * 0.5f) * _cellSize,
            _characterCheckBottomOffset + halfY,
            (originCoord.y + effectiveSize.y * 0.5f) * _cellSize
        );

        Vector3 halfExtents = new Vector3(halfX, halfY, halfZ);

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _characterOverlapBuffer,
            Quaternion.identity,
            _characterLayerMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _characterOverlapBuffer[i];
            if (col == null)
            {
                continue;
            }

            // 이미 배치된 PlaceableObject이거나 그 하위 콜라이더인 경우 무시
            if (col.GetComponentInParent<PlaceableObject>() != null)
            {
                continue;
            }

            // 캐릭터 판별: CharacterController, PlayerController, NonPlayerCharacter 또는 "Player" 태그
            if (col.GetComponentInParent<CharacterController>() != null ||
                col.GetComponentInParent<PlayerController>() != null ||
                col.GetComponentInParent<NonPlayerCharacter>() != null ||
                col.CompareTag("Player"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 프리팹을 인스턴스화하여 그리드 상에 배치하고 점유 상태를 갱신합니다.
    /// </summary>
    public bool TryPlaceObject(PlaceableObject prefab, Vector2Int originCoord, int rotationAngle, out PlaceableObject placedInstance)
    {
        placedInstance = null;

        if (!CanPlaceObject(prefab, originCoord, rotationAngle))
        {
            return false;
        }

        // 오브젝트 설치 월드 위치 및 회전 계산
        Vector2Int effectiveSize = prefab.GetRotatedSize(rotationAngle);
        Vector3 worldSpawnPos = GridOrigin + new Vector3(
            (originCoord.x + effectiveSize.x * 0.5f) * _cellSize,
            0f,
            (originCoord.y + effectiveSize.y * 0.5f) * _cellSize
        );

        Quaternion rotation = Quaternion.Euler(0f, PlaceableObject.NormalizeRotationAngle(rotationAngle), 0f);
        placedInstance = Instantiate(prefab, worldSpawnPos, rotation, transform);

        // 멀티플레이어 환경일 경우 서버 권한으로 스폰
        var netObj = placedInstance.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }

        // 점유 등록
        RegisterPlacement(placedInstance, originCoord, rotationAngle);

        // 설치 순간 영역에 걸친 캐릭터를 안전하게 바깥으로 살짝 밀어냄 (끼임 방지)
        Vector3 pushCenter = worldSpawnPos + new Vector3(0f, 1f, 0f);
        Vector3 pushExtents = new Vector3(effectiveSize.x * _cellSize * 0.55f, 1.2f, effectiveSize.y * _cellSize * 0.55f);
        PushCharactersOutOfArea(pushCenter, pushExtents);

        return true;
    }

    /// <summary>
    /// 설치 완료 시 블록 콜라이더 내에 겹친 캐릭터(플레이어/몬스터)를 안전하게 바깥으로 밀어내어 끼임(Stuck)을 방지합니다.
    /// </summary>
    private void PushCharactersOutOfArea(Vector3 center, Vector3 extents)
    {
        Collider[] colliders = Physics.OverlapBox(center, extents, Quaternion.identity, _characterLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.GetComponentInParent<PlaceableObject>() != null)
            {
                continue;
            }

            CharacterController cc = col.GetComponentInParent<CharacterController>();
            if (cc != null)
            {
                Vector3 pushDir = cc.transform.position - center;
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude < 0.001f)
                {
                    pushDir = Vector3.forward;
                }
                cc.Move(pushDir.normalized * 0.75f);
            }
            else
            {
                Rigidbody rb = col.GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 pushDir = rb.position - center;
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.001f)
                    {
                        pushDir = Vector3.forward;
                    }
                    rb.AddForce(pushDir.normalized * 4.0f, ForceMode.Impulse);
                }
            }
        }
    }

    /// <summary>
    /// 이미 생성된 PlaceableObject 인스턴스를 그리드에 배치하고 점유를 등록합니다.
    /// </summary>
    public bool TryPlaceExistingObject(PlaceableObject instance, Vector2Int originCoord, int rotationAngle)
    {
        if (instance == null || !CanPlaceObject(instance, originCoord, rotationAngle))
        {
            return false;
        }

        Vector2Int effectiveSize = instance.GetRotatedSize(rotationAngle);
        Vector3 worldSpawnPos = GridOrigin + new Vector3(
            (originCoord.x + effectiveSize.x * 0.5f) * _cellSize,
            0f,
            (originCoord.y + effectiveSize.y * 0.5f) * _cellSize
        );

        instance.transform.position = worldSpawnPos;
        instance.transform.rotation = Quaternion.Euler(0f, PlaceableObject.NormalizeRotationAngle(rotationAngle), 0f);

        RegisterPlacement(instance, originCoord, rotationAngle);
        return true;
    }

    /// <summary>
    /// 특정 그리드 좌표에 있는 오브젝트를 제거하고 셀 점유를 해제합니다.
    /// </summary>
    public bool TryRemoveObjectAt(Vector2Int coord, out PlaceableObject removedObject)
    {
        removedObject = null;

        if (!IsValidCoordinate(coord) || _gridCells == null)
        {
            return false;
        }

        GridCell cell = _gridCells[coord.x, coord.y];
        if (!cell.IsOccupied || cell.PlacedObject == null)
        {
            return false;
        }

        removedObject = cell.PlacedObject;
        Vector2Int origin = removedObject.OriginCoordinate;
        int angle = removedObject.CurrentRotationAngle;

        // 점유된 모든 셀에서 참조 제거
        List<Vector2Int> occupiedCoords = removedObject.GetOccupiedCoordinates(origin, angle);
        for (int i = 0; i < occupiedCoords.Count; i++)
        {
            Vector2Int c = occupiedCoords[i];
            if (IsValidCoordinate(c))
            {
                _gridCells[c.x, c.y].Clear();
            }
        }

        _placedObjects.Remove(removedObject);
        removedObject.OnRemoved();

        ObjectRemoved?.Invoke(removedObject, origin);
        return true;
    }

    private void RegisterPlacement(PlaceableObject instance, Vector2Int originCoord, int rotationAngle)
    {
        List<Vector2Int> coords = instance.GetOccupiedCoordinates(originCoord, rotationAngle);

        for (int i = 0; i < coords.Count; i++)
        {
            Vector2Int c = coords[i];
            if (IsValidCoordinate(c))
            {
                _gridCells[c.x, c.y].SetOccupant(instance);
            }
        }

        _placedObjects.Add(instance);
        instance.OnPlaced(originCoord, rotationAngle);

        ObjectPlaced?.Invoke(instance, originCoord);
    }

    #region Debug Gizmos Visualization

    private void OnDrawGizmos()
    {
        if (!_showWhenSelectedOnly)
        {
            DrawGridGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_showWhenSelectedOnly)
        {
            DrawGridGizmos();
        }
    }

    /// <summary>
    /// 씬 뷰 및 기즈모 활성화 시 그리드 및 점유 상태를 시각화합니다.
    /// </summary>
    private void DrawGridGizmos()
    {
        if (!_showGridDebug)
        {
            return;
        }

        Vector3 origin = GridOrigin;
        float widthSize = _gridWidth * _cellSize;
        float lengthSize = _gridLength * _cellSize;

        // 1. 내부 격자선 (Grid Lines)
        Gizmos.color = _gridLineColor;

        // X축 방향 라인들
        for (int x = 0; x <= _gridWidth; x++)
        {
            Vector3 start = origin + new Vector3(x * _cellSize, 0f, 0f);
            Vector3 end = origin + new Vector3(x * _cellSize, 0f, lengthSize);
            Gizmos.DrawLine(start, end);
        }

        // Z축 방향 라인들
        for (int z = 0; z <= _gridLength; z++)
        {
            Vector3 start = origin + new Vector3(0f, 0f, z * _cellSize);
            Vector3 end = origin + new Vector3(widthSize, 0f, z * _cellSize);
            Gizmos.DrawLine(start, end);
        }

        // 2. 바깥 테두리 (Border)
        Gizmos.color = _gridBorderColor;
        Vector3 corner0 = origin;
        Vector3 corner1 = origin + new Vector3(widthSize, 0f, 0f);
        Vector3 corner2 = origin + new Vector3(widthSize, 0f, lengthSize);
        Vector3 corner3 = origin + new Vector3(0f, 0f, lengthSize);

        Gizmos.DrawLine(corner0, corner1);
        Gizmos.DrawLine(corner1, corner2);
        Gizmos.DrawLine(corner2, corner3);
        Gizmos.DrawLine(corner3, corner0);

        // 3. 점유된 셀 시각화 (플레이 모드 런타임)
        if (_showOccupiedFill && _gridCells != null && Application.isPlaying)
        {
            Vector3 cellSizeVector = new Vector3(_cellSize * 0.95f, 0.1f, _cellSize * 0.95f);

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int z = 0; z < _gridLength; z++)
                {
                    GridCell cell = _gridCells[x, z];
                    if (cell != null && cell.IsOccupied)
                    {
                        Gizmos.color = _occupiedCellColor;
                        Vector3 center = GridToWorldPosition(new Vector2Int(x, z), true);
                        Gizmos.DrawCube(center, cellSizeVector);
                    }
                }
            }
        }
    }

    #endregion
}
