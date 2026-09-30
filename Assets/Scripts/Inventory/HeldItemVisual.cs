using UnityEngine;

/// <summary>
/// 손에 장착되는 아이템(무기, 도구 등)의 프리팹 최상단에 부착되어,
/// 총구 위치, 애니메이터, 왼손 IK 타겟 등의 시각적 요소와 기믹을 제공합니다.
/// </summary>
public class HeldItemVisual : MonoBehaviour
{
    [Tooltip("총구 화염 및 탄 궤적이 시작되는 지점")]
    public Transform MuzzlePoint;

    [Tooltip("탄피가 배출되는 지점 (선택 사항)")]
    public Transform EjectionPoint;

    [Tooltip("왼손 Two-Bone IK 타겟 (선택 사항)")]
    public Transform LeftHandIKTarget;

    [Tooltip("무기 자체의 애니메이터 (펌프 액션, 슬라이드 등)")]
    public Animator WeaponAnimator;

    /// <summary>
    /// 하위 렌더러들의 그림자 캐스팅 모드를 일괄 변경합니다.
    /// (1인칭: Off, 3인칭: ShadowsOnly 또는 On)
    /// </summary>
    public void SetShadowCastingMode(UnityEngine.Rendering.ShadowCastingMode mode)
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
        {
            r.shadowCastingMode = mode;
        }
    }

    /// <summary>
    /// 프리팹과 하위 객체의 레이어를 일괄 변경합니다.
    /// (1인칭 카메라 렌더링용 분리 등)
    /// </summary>
    public void SetLayerRecursively(int layer)
    {
        SetLayerRecursive(transform, layer);
    }
    
    private void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
        {
            SetLayerRecursive(child, layer);
        }
    }
}
