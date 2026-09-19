using System;
using UnityEngine;

/// <summary>
/// 그리드의 개별 셀(단위 칸)의 좌표와 점유 상태를 나타내는 데이터 클래스입니다.
/// </summary>
[Serializable]
public class GridCell
{
    private readonly Vector2Int _coordinates;
    private PlaceableObject _placedObject;

    public Vector2Int Coordinates => _coordinates;
    public bool IsOccupied => _placedObject != null;
    public PlaceableObject PlacedObject => _placedObject;

    public GridCell(int x, int z)
    {
        _coordinates = new Vector2Int(x, z);
        _placedObject = null;
    }

    public GridCell(Vector2Int coordinates)
    {
        _coordinates = coordinates;
        _placedObject = null;
    }

    /// <summary>
    /// 해당 셀에 오브젝트를 배치합니다.
    /// </summary>
    public void SetOccupant(PlaceableObject placeableObject)
    {
        _placedObject = placeableObject;
    }

    /// <summary>
    /// 셀 점유를 해제합니다.
    /// </summary>
    public void Clear()
    {
        _placedObject = null;
    }
}
