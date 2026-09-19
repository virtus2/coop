using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 시스템 상에 배치할 수 있는 오브젝트에 부착하는 기본 컴포넌트입니다.
/// 그리드 크기(Size), 회전, 점유 좌표 계산 및 배치/해제 생명주기 이벤트를 지원합니다.
/// </summary>
[DisallowMultipleComponent]
public class PlaceableObject : MonoBehaviour
{
    [Header("Grid Footprint Settings")]
    [Tooltip("오브젝트가 그리드 상에서 차지하는 기본 크기 (X: 가로 셀 수, Y: 세로/깊이 셀 수)")]
    [SerializeField] private Vector2Int _gridSize = new Vector2Int(1, 1);

    [Tooltip("오브젝트 식별용 표시 이름")]
    [SerializeField] private string _displayName = "Buildable Object";

    [Header("Preview / Visuals")]
    [Tooltip("선택 사항: 프리뷰 고스트 생성 시 사용할 메인 렌더러 (비워둘 경우 자식 렌더러 자동 탐색)")]
    [SerializeField] private Renderer _previewRenderer;

    private Vector2Int _originCoordinate;
    private int _currentRotationAngle; // 0, 90, 180, 270도
    private bool _isPlaced;

    public Vector2Int GridSize => _gridSize;
    public string DisplayName => _displayName;
    public Vector2Int OriginCoordinate => _originCoordinate;
    public int CurrentRotationAngle => _currentRotationAngle;
    public bool IsPlaced => _isPlaced;

    public event Action<PlaceableObject> Placed;
    public event Action<PlaceableObject> Removed;

    /// <summary>
    /// 지정된 회전 각도(0, 90, 180, 270도)가 적용되었을 때 차지하는 바운드 크기를 반환합니다.
    /// </summary>
    public Vector2Int GetRotatedSize(int rotationAngle)
    {
        int normalizedAngle = NormalizeRotationAngle(rotationAngle);
        if (normalizedAngle == 90 || normalizedAngle == 270)
        {
            return new Vector2Int(_gridSize.y, _gridSize.x);
        }

        return _gridSize;
    }

    /// <summary>
    /// 원점 좌표와 회전 각도를 기준으로 이 오브젝트가 점유하게 되는 모든 그리드 셀 좌표 목록을 계산하여 반환합니다.
    /// </summary>
    public List<Vector2Int> GetOccupiedCoordinates(Vector2Int originCoord, int rotationAngle)
    {
        List<Vector2Int> occupiedCoords = new List<Vector2Int>();
        Vector2Int effectiveSize = GetRotatedSize(rotationAngle);

        for (int x = 0; x < effectiveSize.x; x++)
        {
            for (int z = 0; z < effectiveSize.y; z++)
            {
                occupiedCoords.Add(new Vector2Int(originCoord.x + x, originCoord.y + z));
            }
        }

        return occupiedCoords;
    }

    /// <summary>
    /// 그리드에 성공적으로 배치되었을 때 호출됩니다.
    /// </summary>
    public virtual void OnPlaced(Vector2Int originCoordinate, int rotationAngle)
    {
        _originCoordinate = originCoordinate;
        _currentRotationAngle = NormalizeRotationAngle(rotationAngle);
        _isPlaced = true;

        Placed?.Invoke(this);
    }

    /// <summary>
    /// 그리드에서 제거되었을 때 호출됩니다.
    /// </summary>
    public virtual void OnRemoved()
    {
        _isPlaced = false;
        Removed?.Invoke(this);
    }

    /// <summary>
    /// 각도를 0, 90, 180, 270 중 하나로 정규화합니다.
    /// </summary>
    public static int NormalizeRotationAngle(int angle)
    {
        int normalized = angle % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        // 90도 단위로 스냅
        int rounded = Mathf.RoundToInt(normalized / 90f) * 90;
        return rounded % 360;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_gridSize.x < 1)
        {
            _gridSize.x = 1;
        }

        if (_gridSize.y < 1)
        {
            _gridSize.y = 1;
        }
    }
#endif
}
