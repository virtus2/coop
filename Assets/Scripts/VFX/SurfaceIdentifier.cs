using UnityEngine;

namespace Coop.VFX
{
    /// <summary>
    /// 환경 사물, 벽, 바닥 등에 부착하여 타격 시 재생될 표면 재질(SurfaceType)을 지정하는 컴포넌트입니다.
    /// 부착되지 않은 환경 오브젝트는 기본값(Default)으로 처리됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceIdentifier : MonoBehaviour
    {
        [Tooltip("이 오브젝트의 표면 재질 유형입니다.")]
        [SerializeField] private SurfaceType _surfaceType = SurfaceType.Default;

        public SurfaceType SurfaceType
        {
            get => _surfaceType;
            set => _surfaceType = value;
        }
    }
}
