using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coop.Audio
{
    /// <summary>
    /// 사운드 재생 설정(오디오 클립, 랜덤 피치/볼륨, 우선순위, 쿨다운, 2D/3D 등)을
    /// 인스펙터에서 직관적으로 구성하고 SoundManager와 연동하여 재생하는 데이터 클래스입니다.
    /// 버튼뿐만 아니라 아이템, 상호작용 오브젝트, 무기 등 게임 전역에서 공통으로 재사용할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class SoundCue
    {
        [Header("Audio Clips")]
        [Tooltip("기본 오디오 클립입니다.")]
        [SerializeField] private AudioClip _clip;

        [Tooltip("추가 오디오 클립 목록입니다. 등록 시 무작위로 선택하여 재생할 수 있습니다.")]
        [SerializeField] private AudioClip[] _additionalClips = Array.Empty<AudioClip>();

        [Tooltip("체크 시 단일 클립과 추가 클립 목록 중에서 무작위로 하나를 선택하여 재생합니다.")]
        [SerializeField] private bool _useRandomClip = false;

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [Tooltip("재생 볼륨 배율입니다. (0 ~ 1)")]
        [SerializeField] private float _volume = 1.0f;

        [Tooltip("볼륨을 무작위 범위 내에서 변동시킬지 여부입니다.")]
        [SerializeField] private bool _useRandomVolume = false;

        [Range(0f, 1f)]
        [SerializeField] private float _minVolume = 0.9f;

        [Range(0f, 1f)]
        [SerializeField] private float _maxVolume = 1.0f;

        [Header("Pitch Settings")]
        [Tooltip("피치를 무작위 범위 내에서 변동시켜 반복 재생 시 자연스러운 음향을 연출합니다.")]
        [SerializeField] private bool _useRandomPitch = true;

        [Range(0.5f, 2.0f)]
        [SerializeField] private float _minPitch = 0.95f;

        [Range(0.5f, 2.0f)]
        [SerializeField] private float _maxPitch = 1.05f;

        [Header("Priority & Filter")]
        [Tooltip("풀(Pool) 고갈 시 선점 여부를 결정하는 사운드 우선순위입니다.")]
        [SerializeField] private SoundPriority _priority = SoundPriority.Normal;

        [Tooltip("동일 사운드가 너무 짧은 주기로 중복 재생되는 것을 방지하는 최소 간격(초)입니다.")]
        [SerializeField] private float _cooldown = 0.05f;

        [Header("3D Spatial Settings")]
        [Tooltip("체크 시 3D 공간 사운드로 재생합니다. (UI 버튼 등은 체크 해제하여 2D로 선명하게 재생)")]
        [SerializeField] private bool _is3D = false;

        [Range(0f, 1f)]
        [SerializeField] private float _spatialBlend = 1.0f;

        [SerializeField] private float _minDistance = 1.0f;
        [SerializeField] private float _maxDistance = 30.0f;

        // 런타임 재생 시간 추적 (쿨다운 체크용)
        [NonSerialized] private float _lastPlayTime = -999f;

        public AudioClip Clip => _clip;
        public AudioClip[] AdditionalClips => _additionalClips;
        public bool UseRandomClip => _useRandomClip;
        public float Volume => _volume;
        public bool UseRandomPitch => _useRandomPitch;
        public float MinPitch => _minPitch;
        public float MaxPitch => _maxPitch;
        public SoundPriority Priority => _priority;
        public float Cooldown => _cooldown;
        public bool Is3D => _is3D;

        /// <summary>
        /// 쿨다운 간격을 고려하여 현재 재생 가능한 상태인지 여부입니다.
        /// </summary>
        public bool CanPlay => (Time.unscaledTime - _lastPlayTime) >= _cooldown;

        public SoundCue()
        {
        }

        public SoundCue(AudioClip clip, float volume = 1f, bool randomPitch = true, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            _clip = clip;
            _volume = volume;
            _useRandomPitch = randomPitch;
            _minPitch = minPitch;
            _maxPitch = maxPitch;
        }

        /// <summary>
        /// 설정에 따라 재생할 오디오 클립 하나를 선택합니다.
        /// </summary>
        public AudioClip GetClip()
        {
            if (!_useRandomClip || _additionalClips == null || _additionalClips.Length == 0)
            {
                return _clip;
            }

            // _clip과 _additionalClips 전체 풀에서 무작위 선택
            int totalClips = (_clip != null ? 1 : 0) + _additionalClips.Length;
            if (totalClips == 0)
            {
                return null;
            }

            int randomIndex = UnityEngine.Random.Range(0, totalClips);
            if (_clip != null && randomIndex == 0)
            {
                return _clip;
            }

            int additionalIndex = _clip != null ? (randomIndex - 1) : randomIndex;
            return _additionalClips[additionalIndex] != null ? _additionalClips[additionalIndex] : _clip;
        }

        /// <summary>
        /// 설정된 볼륨 또는 무작위 볼륨 값을 산출합니다.
        /// </summary>
        public float GetVolume()
        {
            if (_useRandomVolume)
            {
                float min = Mathf.Min(_minVolume, _maxVolume);
                float max = Mathf.Max(_minVolume, _maxVolume);
                return UnityEngine.Random.Range(min, max);
            }
            return _volume;
        }

        /// <summary>
        /// 설정된 피치 또는 무작위 피치 값을 산출합니다.
        /// </summary>
        public float GetPitch()
        {
            if (_useRandomPitch)
            {
                float min = Mathf.Min(_minPitch, _maxPitch);
                float max = Mathf.Max(_minPitch, _maxPitch);
                return UnityEngine.Random.Range(min, max);
            }
            return 1.0f;
        }

        /// <summary>
        /// 사운드를 재생합니다. SoundManager가 있으면 풀링 시스템을 활용하며,
        /// 없으면 기본 AudioSource Fallback을 통해 안전하게 재생합니다.
        /// </summary>
        /// <param name="position">3D 사운드일 경우 재생 위치 (미지정 시 2D 또는 Vector3.zero)</param>
        /// <returns>재생에 사용된 AudioSource (재생 실패/쿨다운/클립 없음 시 null)</returns>
        public AudioSource Play(Vector3? position = null)
        {
            if (!CanPlay)
            {
                return null;
            }

            AudioClip clipToPlay = GetClip();
            if (clipToPlay == null)
            {
                return null;
            }

            float volume = Mathf.Clamp01(GetVolume());
            float pitch = GetPitch();
            _lastPlayTime = Time.unscaledTime;

            bool playAs3D = _is3D && position.HasValue;

            // 1. SoundManager 연동 (우선순위 풀링 시스템)
            if (SoundManager.Instance != null)
            {
                if (playAs3D)
                {
                    return SoundManager.Instance.PlaySfxAt(
                        clip: clipToPlay,
                        position: position.Value,
                        volume: volume,
                        pitch: pitch,
                        priority: _priority,
                        spatialBlend: _spatialBlend,
                        minDistance: _minDistance,
                        maxDistance: _maxDistance,
                        loop: false
                    );
                }
                else
                {
                    return SoundManager.Instance.PlaySfx(
                        clip: clipToPlay,
                        volume: volume,
                        pitch: pitch,
                        priority: _priority,
                        loop: false
                    );
                }
            }

            // 2. SoundManager 부재 시 Fallback (PlayClipAtPoint)
            if (playAs3D)
            {
                AudioSource.PlayClipAtPoint(clipToPlay, position.Value, volume);
            }
            else
            {
                Camera cam = Camera.main;
                Vector3 fallbackPos = cam != null ? cam.transform.position : Vector3.zero;
                AudioSource.PlayClipAtPoint(clipToPlay, fallbackPos, volume);
            }

            return null;
        }
    }
}
