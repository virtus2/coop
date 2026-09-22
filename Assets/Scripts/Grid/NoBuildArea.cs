using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 블록 설치를 차단하는 금지 구역 컴포넌트입니다.
/// 코어 주변, 몬스터 스폰 포인트(환기구/문), 특정 스토리 트리거 구역 등에 부착하여
/// 플레이어가 블록을 설치해 경로를 봉쇄하는 것을 방지합니다.
/// </summary>
[DisallowMultipleComponent]
public class NoBuildArea : MonoBehaviour
{
    private static readonly List<NoBuildArea> _activeAreas = new List<NoBuildArea>();
    public static IReadOnlyList<NoBuildArea> ActiveAreas => _activeAreas;

    [Header("Area Settings")]
    [Tooltip("금지 구역 설명 또는 식별 이름 (예: Core Defense Zone)")]
    [SerializeField] private string _areaName = "Restricted Build Zone";

    [Tooltip("영역 형태 (Box 또는 Sphere)")]
    [SerializeField] private AreaShape _shape = AreaShape.Box;

    [Tooltip("Box 형태일 때의 로컬 크기")]
    [SerializeField] private Vector3 _boxSize = new Vector3(4f, 3f, 4f);

    [Tooltip("Sphere 형태일 때의 반지름")]
    [SerializeField] private float _sphereRadius = 3f;

    [Tooltip("로컬 오프셋")]
    [SerializeField] private Vector3 _centerOffset = Vector3.zero;

    [Header("Gizmo Visualization")]
    [SerializeField] private bool _showGizmo = true;
    [SerializeField] private Color _gizmoColor = new Color(1f, 0.1f, 0.1f, 0.35f);

    public enum AreaShape
    {
        Box,
        Sphere
    }

    public string AreaName => _areaName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        _activeAreas.Clear();
    }

    private void OnEnable()
    {
        if (!_activeAreas.Contains(this))
        {
            _activeAreas.Add(this);
        }
    }

    private void OnDisable()
    {
        _activeAreas.Remove(this);
    }

    /// <summary>
    /// 지정된 바운드(중심 위치와 크기)가 이 금지 구역과 교차하는지 검사합니다.
    /// </summary>
    public bool Overlaps(Vector3 center, Vector3 extents)
    {
        Vector3 areaCenter = transform.TransformPoint(_centerOffset);

        if (_shape == AreaShape.Box)
        {
            Bounds checkBounds = new Bounds(center, extents * 2f);
            Bounds areaBounds = new Bounds(areaCenter, _boxSize);
            return checkBounds.Intersects(areaBounds);
        }
        else
        {
            // Sphere vs AABB 검사
            float sqrDist = 0f;
            Vector3 min = center - extents;
            Vector3 max = center + extents;

            if (areaCenter.x < min.x) sqrDist += (min.x - areaCenter.x) * (min.x - areaCenter.x);
            else if (areaCenter.x > max.x) sqrDist += (areaCenter.x - max.x) * (areaCenter.x - max.x);

            if (areaCenter.y < min.y) sqrDist += (min.y - areaCenter.y) * (min.y - areaCenter.y);
            else if (areaCenter.y > max.y) sqrDist += (areaCenter.y - max.y) * (areaCenter.y - max.y);

            if (areaCenter.z < min.z) sqrDist += (min.z - areaCenter.z) * (min.z - areaCenter.z);
            else if (areaCenter.z > max.z) sqrDist += (areaCenter.z - max.z) * (areaCenter.z - max.z);

            return sqrDist <= (_sphereRadius * _sphereRadius);
        }
    }

    /// <summary>
    /// 전역 활성 금지 구역 목록을 순회하여 해당 위치가 어떤 금지 구역과도 겹치는지 검사합니다.
    /// </summary>
    public static bool IsInAnyNoBuildArea(Vector3 center, Vector3 extents, out string hitAreaName)
    {
        hitAreaName = string.Empty;
        for (int i = 0; i < _activeAreas.Count; i++)
        {
            var area = _activeAreas[i];
            if (area != null && area.Overlaps(center, extents))
            {
                hitAreaName = area._areaName;
                return true;
            }
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!_showGizmo) return;

        Gizmos.color = _gizmoColor;
        Vector3 pos = transform.TransformPoint(_centerOffset);

        if (_shape == AreaShape.Box)
        {
            Gizmos.DrawCube(pos, _boxSize);
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 1f);
            Gizmos.DrawWireCube(pos, _boxSize);
        }
        else
        {
            Gizmos.DrawSphere(pos, _sphereRadius);
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 1f);
            Gizmos.DrawWireSphere(pos, _sphereRadius);
        }
    }
}
