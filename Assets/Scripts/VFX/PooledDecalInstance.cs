using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Coop.VFX
{
    /// <summary>
    /// DecalPoolManager에 의해 풀링되는 단일 총탄 구멍 데칼 인스턴스입니다.
    /// URP DecalProjector를 사용하며, 8초 유지 + 1초 페이드아웃 및 움직이는 물체(FollowTarget) 추적을 지원합니다.
    /// 대상 오브젝트가 파괴되거나 비활성화되면 즉시 풀로 자동 반환됩니다.
    /// </summary>
    [RequireComponent(typeof(DecalProjector))]
    public class PooledDecalInstance : MonoBehaviour
    {
        private DecalProjector _projector;
        private Action<PooledDecalInstance> _returnCallback;

        // 수명 제어 타이머
        private float _activeDuration = 8.0f;
        private float _fadeDuration = 1.0f;
        private float _elapsedTime = 0f;
        private bool _isAlive = false;

        // 움직이는 오브젝트 추적 (부모 자식 관계를 맺지 않아 lossyScale 왜곡 원천 방지)
        private Transform _targetTransform;
        private Vector3 _localOffset;
        private Quaternion _localRot;

        public bool IsAlive => _isAlive;

        private void Awake()
        {
            _projector = GetComponent<DecalProjector>();
        }

        public void Initialize(Action<PooledDecalInstance> returnCallback)
        {
            _returnCallback = returnCallback;
            if (_projector == null)
            {
                _projector = GetComponent<DecalProjector>();
            }
        }

        /// <summary>
        /// 데칼을 지정된 위치, 각도, 머티리얼로 활성화하고 타이머를 시작합니다.
        /// </summary>
        public void Play(
            Vector3 position,
            Quaternion rotation,
            Material decalMaterial,
            Vector3 size,
            float startAngleFade,
            float endAngleFade,
            float activeDuration,
            float fadeDuration,
            Transform target = null)
        {
            if (_projector == null)
            {
                _projector = GetComponent<DecalProjector>();
            }

            transform.position = position;
            transform.rotation = rotation;

            _projector.material = decalMaterial;
            _projector.size = size;
            _projector.pivot = Vector3.zero;
            _projector.startAngleFade = startAngleFade;
            _projector.endAngleFade = endAngleFade;
            _projector.fadeFactor = 1.0f;

            _activeDuration = activeDuration;
            _fadeDuration = fadeDuration;
            _elapsedTime = 0f;
            _isAlive = true;

            // 정적 지형이 아닌 동적 물체(문, 상자 등)일 경우 추적 설정
            if (target != null && !target.gameObject.isStatic)
            {
                _targetTransform = target;
                _localOffset = target.InverseTransformPoint(position);
                _localRot = Quaternion.Inverse(target.rotation) * rotation;
            }
            else
            {
                _targetTransform = null;
            }

            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!_isAlive) return;

            // 1. 추적 중인 대상이 파괴되었거나 비활성화된 경우(예: 파괴 가능한 상자) 즉시 회수
            if (_targetTransform != null)
            {
                if (!_targetTransform.gameObject.activeInHierarchy)
                {
                    ReturnToPool();
                    return;
                }

                // 부모-자식 종속 없이 월드 좌표 갱신 (부모의 non-uniform lossyScale 왜곡 완벽 차단)
                transform.position = _targetTransform.TransformPoint(_localOffset);
                transform.rotation = _targetTransform.rotation * _localRot;
            }

            // 2. 수명 및 페이드아웃 계산
            _elapsedTime += Time.deltaTime;

            if (_elapsedTime <= _activeDuration)
            {
                _projector.fadeFactor = 1.0f;
            }
            else if (_elapsedTime < _activeDuration + _fadeDuration)
            {
                float t = (_elapsedTime - _activeDuration) / Mathf.Max(0.01f, _fadeDuration);
                _projector.fadeFactor = Mathf.Clamp01(1.0f - t);
            }
            else
            {
                // 페이드아웃 종료 후 풀 반환
                ReturnToPool();
            }
        }

        /// <summary>
        /// 풀 용량 초과 또는 외부 요청 시 즉각 회수
        /// </summary>
        public void ForceEvict()
        {
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (!_isAlive) return;

            _isAlive = false;
            _targetTransform = null;
            if (_projector != null)
            {
                _projector.fadeFactor = 0f;
            }

            gameObject.SetActive(false);
            _returnCallback?.Invoke(this);
        }

        private void OnDisable()
        {
            _targetTransform = null;
        }
    }
}
