using UnityEngine;

/// <summary>
/// 인게임 게임 뷰에서 그리드를 렌더링하기 위한 공통 인터페이스입니다.
/// 라인 렌더러(LineRenderer) 방식이나 바닥 쿼드 + 그리드 셰이더(Grid Shader) 방식으로
/// 자유롭게 교체하여 사용할 수 있도록 추상화합니다.
/// </summary>
public interface IGridVisualizer
{
    /// <summary>
    /// 현재 그리드가 시각적으로 표시되고 있는지 여부
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// 그리드 시각화를 활성화합니다.
    /// </summary>
    void ShowGrid();

    /// <summary>
    /// 그리드 시각화를 비활성화하고 숨깁니다.
    /// </summary>
    void HideGrid();

    /// <summary>
    /// 플레이어나 카메라 등 기준점 위치 및 반경에 맞춰 그리드 시각화 범위를 갱신합니다.
    /// </summary>
    /// <param name="focusPosition">플레이어 또는 시선 기준 월드 좌표</param>
    /// <param name="visualRadius">표시할 그리드 반경 (미터 단위)</param>
    void UpdateVisualizer(Vector3 focusPosition, float visualRadius);
}
