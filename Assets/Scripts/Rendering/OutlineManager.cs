using System.Collections.Generic;
using UnityEngine;

namespace Coop.Rendering
{
    /// <summary>
    /// 상호작용 가능한 물체의 외곽선(아웃라인) 강조 대상 렌더러와 색상을 전역 관리하는 매니저입니다.
    /// URP OutlineRenderFeature가 매 프레임 이 매니저를 조회하여 렌더링을 수행합니다.
    /// </summary>
    public static class OutlineManager
    {
        private static readonly List<Renderer> _activeRenderers = new List<Renderer>();
        private static Color _currentColor = Color.white;

        public static Color CurrentColor => _currentColor;
        public static bool HasTarget => _activeRenderers.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _activeRenderers.Clear();
            _currentColor = Color.white;
        }

        /// <summary>
        /// 외곽선을 표시할 렌더러 목록과 색상을 등록합니다.
        /// </summary>
        /// <param name="renderers">강조 대상 렌더러 배열</param>
        /// <param name="color">외곽선 색상 (예: 상호작용 가능: 흰색, 불가: 빨간색)</param>
        public static void SetHighlight(Renderer[] renderers, Color color)
        {
            _activeRenderers.Clear();
            _currentColor = color;

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r != null && r.gameObject.activeInHierarchy && r.enabled)
                    {
                        _activeRenderers.Add(r);
                    }
                }
            }
        }

        /// <summary>
        /// 현재 활성화된 외곽선 강조를 해제합니다.
        /// </summary>
        public static void ClearHighlight()
        {
            _activeRenderers.Clear();
        }

        /// <summary>
        /// 현재 유효한 강조 대상 렌더러 목록을 반환합니다. 파괴된 렌더러는 자동 정리됩니다.
        /// </summary>
        public static List<Renderer> GetActiveRenderers()
        {
            _activeRenderers.RemoveAll(r => r == null || !r.gameObject.activeInHierarchy || !r.enabled);
            return _activeRenderers;
        }
    }
}
