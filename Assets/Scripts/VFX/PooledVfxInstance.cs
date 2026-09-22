using System;
using UnityEngine;

namespace Coop.VFX
{
    /// <summary>
    /// VfxPoolManager에 의해 풀링되는 개별 VFX 인스턴스입니다.
    /// 파티클 재생 완료 시 OnParticleSystemStopped 콜백을 통해 소속 풀로 자동 반환됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledVfxInstance : MonoBehaviour
    {
        private ParticleSystem _rootParticleSystem;
        private ParticleSystem[] _allParticleSystems;
        private TrailRenderer[] _trailRenderers;
        private Action<PooledVfxInstance> _returnAction;

        private bool _isReturned;

        public bool IsReturned => _isReturned;

        private void Awake()
        {
            CacheComponents();
            ConfigureStopAction();

            // 인스턴스화 순간 자동 재생 방지 및 파티클 소거
            if (_allParticleSystems != null)
            {
                for (int i = 0; i < _allParticleSystems.Length; i++)
                {
                    if (_allParticleSystems[i] != null)
                    {
                        _allParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        _allParticleSystems[i].Clear(true);
                    }
                }
            }
        }

        public void Initialize(Action<PooledVfxInstance> returnAction)
        {
            _returnAction = returnAction;
            CacheComponents();
            ConfigureStopAction();
        }

        private void CacheComponents()
        {
            if (_rootParticleSystem == null)
            {
                _rootParticleSystem = GetComponent<ParticleSystem>();
            }

            if (_allParticleSystems == null || _allParticleSystems.Length == 0)
            {
                _allParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }

            if (_trailRenderers == null || _trailRenderers.Length == 0)
            {
                _trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
            }
        }

        private void ConfigureStopAction()
        {
            if (_allParticleSystems != null)
            {
                for (int i = 0; i < _allParticleSystems.Length; i++)
                {
                    if (_allParticleSystems[i] != null)
                    {
                        var main = _allParticleSystems[i].main;
                        main.playOnAwake = false;
                    }
                }
            }

            if (_rootParticleSystem != null)
            {
                var main = _rootParticleSystem.main;
                main.stopAction = ParticleSystemStopAction.Callback;
            }
        }

        /// <summary>
        /// 새로운 위치와 회전값으로 파티클을 재배치하고 즉시 재생합니다.
        /// </summary>
        public void ResetAndPlay(Vector3 position, Quaternion rotation)
        {
            _isReturned = false;

            transform.SetPositionAndRotation(position, rotation);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // 트레일 렌더러 잔상 초기화
            if (_trailRenderers != null)
            {
                for (int i = 0; i < _trailRenderers.Length; i++)
                {
                    if (_trailRenderers[i] != null)
                    {
                        _trailRenderers[i].Clear();
                    }
                }
            }

            // 파티클 잔상 초기화 및 재생
            if (_allParticleSystems != null && _allParticleSystems.Length > 0)
            {
                for (int i = 0; i < _allParticleSystems.Length; i++)
                {
                    if (_allParticleSystems[i] != null)
                    {
                        _allParticleSystems[i].Clear(true);
                    }
                }

                if (_rootParticleSystem != null)
                {
                    _rootParticleSystem.Play(true);
                }
            }
        }

        /// <summary>
        /// ParticleSystem의 stopAction이 Callback일 때 파티클 방출 및 생명주기가 끝나는 순간 Unity 엔진에 의해 호출됩니다.
        /// </summary>
        private void OnParticleSystemStopped()
        {
            ReturnToPool();
        }

        /// <summary>
        /// 풀로 안전하게 인스턴스를 반환합니다.
        /// </summary>
        public void ReturnToPool()
        {
            if (_isReturned) return;
            _isReturned = true;

            if (_allParticleSystems != null)
            {
                for (int i = 0; i < _allParticleSystems.Length; i++)
                {
                    if (_allParticleSystems[i] != null)
                    {
                        _allParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        _allParticleSystems[i].Clear(true);
                    }
                }
            }

            gameObject.SetActive(false);
            _returnAction?.Invoke(this);
        }

        private void OnDisable()
        {
            // 인스턴스가 비활성화될 때 잔여 상태 정리
            if (_allParticleSystems != null)
            {
                for (int i = 0; i < _allParticleSystems.Length; i++)
                {
                    if (_allParticleSystems[i] != null)
                    {
                        _allParticleSystems[i].Clear(true);
                    }
                }
            }
        }
    }
}
