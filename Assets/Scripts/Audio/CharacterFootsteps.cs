using System;
using System.Collections.Generic;
using Coop.Audio;
using UnityEngine;

/// <summary>
/// 플레이어, 몬스터, NPC 등의 캐릭터가 걸어다닐 때 애니메이션 이벤트(Animation Event)를 수신하여
/// 발걸음 소리(SFX)를 재생하는 컴포넌트입니다.
///
/// 1. 기본 발걸음 클립 목록과 표면(Surface/Tag/PhysicMaterial)별 클립 목록 지원
/// 2. 사운드 피치 무작위(Random Pitch)를 통한 자연스러운 발걸음 음향 연출
/// 3. SoundManager 풀링 시스템과 자동 연동 및 Standalone AudioSource Fallback 지원
/// 4. 1인칭 로컬 플레이어는 2D 사운드로 선명하게, 원격 플레이어/몬스터/NPC는 3D 공간 음향으로 처리
/// 5. OnFootstep, Step, Footstep 등 주요 애니메이션 이벤트 메서드명 지원
/// </summary>
[DisallowMultipleComponent]
public class CharacterFootsteps : MonoBehaviour
{
    [System.Serializable]
    public struct SurfaceFootstepData
    {
        [Tooltip("지면 오브젝트의 Tag (예: Wood, Stone, Grass, Dirt)")]
        public string SurfaceTag;

        [Tooltip("지면 Collider의 물리 머티리얼 (선택 사항)")]
        public PhysicsMaterial PhysicMaterial;

        [Tooltip("해당 지면에서 재생할 발걸음 오디오 클립 목록")]
        public AudioClip[] FootstepClips;
    }

    [Header("Default Footstep Clips")]
    [Tooltip("지면 재질이 특정되지 않았거나 기본으로 재생할 발걸음 오디오 클립 목록입니다.")]
    [SerializeField] private AudioClip[] _defaultFootstepClips = Array.Empty<AudioClip>();

    [Header("Surface Footsteps (Optional)")]
    [Tooltip("지면 Tag 또는 물리 머티리얼에 따라 별도의 발걸음 클립을 지정할 수 있습니다.")]
    [SerializeField] private List<SurfaceFootstepData> _surfaceFootsteps = new List<SurfaceFootstepData>();

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 0.8f;

    [Range(0.5f, 2.0f)]
    [SerializeField] private float _minPitch = 0.9f;

    [Range(0.5f, 2.0f)]
    [SerializeField] private float _maxPitch = 1.1f;

    [Header("3D Spatial Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float _spatialBlend = 1.0f;

    [SerializeField] private float _minDistance = 1.0f;
    [SerializeField] private float _maxDistance = 30.0f;

    [Tooltip("로컬 플레이어 본인의 발걸음 소리인 경우 2D 사운드로 선명하게 재생할지 여부입니다.")]
    [SerializeField] private bool _isLocalPlayer2D = true;

    [Header("Ground Raycast Settings")]
    [SerializeField] private bool _useGroundRaycast = true;
    [SerializeField] private Vector3 _groundCheckOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float _groundCheckDistance = 1.5f;
    [SerializeField] private LayerMask _groundLayerMask = ~0;

    [Header("Playback Limiter")]
    [Tooltip("애니메이션 이벤트가 너무 연속으로 들어와 발걸음 소리가 겹쳐 찢어지는 것을 방지하는 최소 쿨다운(초)입니다.")]
    [SerializeField] private float _minStepInterval = 0.12f;

    private AudioSource _fallbackAudioSource;
    private PlayerController _playerController;
    private float _lastStepTime = -1f;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        if (_playerController == null)
        {
            _playerController = GetComponentInParent<PlayerController>();
        }

        // SoundManager가 없는 환경을 대비한 Fallback AudioSource 확보
        _fallbackAudioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// 발걸음 소리를 재생할 위치를 반환합니다. (캐릭터 발밑 위치)
    /// </summary>
    public Vector3 FootPosition => transform.position;

    /// <summary>
    /// 이 캐릭터가 현재 활성화된 로컬 플레이어인지 확인합니다.
    /// </summary>
    public bool IsLocalPlayer
    {
        get
        {
            if (_playerController != null)
            {
                if (PlayerController.LocalInstance != null)
                {
                    return _playerController == PlayerController.LocalInstance;
                }

                if (_playerController.IsSpawned)
                {
                    return _playerController.IsOwner;
                }
            }

            return false;
        }
    }

    #region Animation Event Handlers

    /// <summary>
    /// 표준 애니메이션 이벤트 수신 메서드 (매개변수 없음)
    /// </summary>
    public void OnFootstep()
    {
        TriggerFootstep(null);
    }

    /// <summary>
    /// AnimationEvent 객체를 전달받는 애니메이션 이벤트 수신 메서드
    /// stringParameter로 지면 재질(Wood, Metal 등) 또는 발 구분(Left, Right)을 전달할 수 있습니다.
    /// </summary>
    public void OnFootstep(AnimationEvent animationEvent)
    {
        string parameter = animationEvent != null ? animationEvent.stringParameter : null;
        TriggerFootstep(parameter);
    }

    /// <summary>
    /// 애니메이션 이벤트 별칭 (Footstep)
    /// </summary>
    public void Footstep()
    {
        TriggerFootstep(null);
    }

    /// <summary>
    /// 애니메이션 이벤트 별칭 (Step)
    /// </summary>
    public void Step()
    {
        TriggerFootstep(null);
    }

    /// <summary>
    /// 수동 또는 스크립트에서 호출 가능한 발걸음 재생 메서드
    /// </summary>
    public void PlayFootstepSound()
    {
        TriggerFootstep(null);
    }

    /// <summary>
    /// 특정 표면 태그를 지정하여 호출하는 발걸음 재생 메서드
    /// </summary>
    public void PlayFootstepSound(string surfaceName)
    {
        TriggerFootstep(surfaceName);
    }

    #endregion

    #region Core Footstep Logic

    private void TriggerFootstep(string explicitSurface)
    {
        // 최소 쿨다운 체크
        if (Time.time - _lastStepTime < _minStepInterval)
        {
            return;
        }

        _lastStepTime = Time.time;

        // 1. 재생할 오디오 클립 선택
        AudioClip clipToPlay = SelectFootstepClip(explicitSurface);
        if (clipToPlay == null)
        {
            // 사용자가 클립을 아직 등록하지 않은 경우 조용히 통과
            return;
        }

        // 2. 무작위 피치 계산
        float pitch = UnityEngine.Random.Range(_minPitch, _maxPitch);

        // 3. 로컬 1인칭 플레이어의 2D 재생 여부 판별
        bool playAs2D = _isLocalPlayer2D && IsLocalPlayer;

        // 4. 사운드 재생 실행
        PlayClip(clipToPlay, _volume, pitch, playAs2D);
    }

    private AudioClip SelectFootstepClip(string explicitSurface)
    {
        // 1) 명시적 표면 이름(이벤트 파라미터)이 넘어온 경우 해당 표면 검색
        if (!string.IsNullOrEmpty(explicitSurface))
        {
            AudioClip clip = FindClipForSurface(explicitSurface, null);
            if (clip != null)
            {
                return clip;
            }
        }

        // 2) 레이캐스트를 통한 바닥 표면(Tag / PhysicMaterial) 검출
        if (_useGroundRaycast)
        {
            Vector3 rayStart = transform.position + _groundCheckOffset;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                string groundTag = hit.collider.tag;
                PhysicsMaterial groundMat = hit.collider.sharedMaterial;

                AudioClip clip = FindClipForSurface(groundTag, groundMat);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        // 3) Fallback: 기본 발걸음 클립 목록에서 무작위 선택
        return GetRandomClipFromList(_defaultFootstepClips);
    }

    private AudioClip FindClipForSurface(string surfaceTag, PhysicsMaterial material)
    {
        if (_surfaceFootsteps == null || _surfaceFootsteps.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < _surfaceFootsteps.Count; i++)
        {
            SurfaceFootstepData data = _surfaceFootsteps[i];

            // 물리 머티리얼 일치 검사
            if (material != null && data.PhysicMaterial == material)
            {
                AudioClip clip = GetRandomClipFromList(data.FootstepClips);
                if (clip != null)
                {
                    return clip;
                }
            }

            // 태그 일치 검사
            if (!string.IsNullOrEmpty(surfaceTag) && !string.IsNullOrEmpty(data.SurfaceTag) &&
                string.Equals(data.SurfaceTag, surfaceTag, StringComparison.OrdinalIgnoreCase))
            {
                AudioClip clip = GetRandomClipFromList(data.FootstepClips);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        return null;
    }

    private AudioClip GetRandomClipFromList(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int count = clips.Length;
        int startIndex = UnityEngine.Random.Range(0, count);

        for (int i = 0; i < count; i++)
        {
            int index = (startIndex + i) % count;
            if (clips[index] != null)
            {
                return clips[index];
            }
        }

        return null;
    }

    private void PlayClip(AudioClip clip, float volume, float pitch, bool playAs2D)
    {
        if (clip == null)
        {
            return;
        }

        // SoundManager 싱글톤이 존재하는 경우 SoundManager 풀 활용
        if (SoundManager.Instance != null)
        {
            if (playAs2D)
            {
                SoundManager.Instance.PlaySfx(
                    clip: clip,
                    volume: volume,
                    pitch: pitch,
                    priority: SoundPriority.Low,
                    loop: false
                );
            }
            else
            {
                SoundManager.Instance.PlaySfxAt(
                    clip: clip,
                    position: FootPosition,
                    volume: volume,
                    pitch: pitch,
                    priority: SoundPriority.Low,
                    spatialBlend: _spatialBlend,
                    minDistance: _minDistance,
                    maxDistance: _maxDistance,
                    loop: false
                );
            }
            return;
        }

        // SoundManager가 없는 단독 씬 / Fallback
        if (_fallbackAudioSource == null)
        {
            _fallbackAudioSource = gameObject.AddComponent<AudioSource>();
            _fallbackAudioSource.playOnAwake = false;
        }

        if (_fallbackAudioSource != null)
        {
            _fallbackAudioSource.spatialBlend = playAs2D ? 0f : _spatialBlend;
            _fallbackAudioSource.minDistance = _minDistance;
            _fallbackAudioSource.maxDistance = _maxDistance;
            _fallbackAudioSource.pitch = pitch;
            _fallbackAudioSource.PlayOneShot(clip, volume);
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!_useGroundRaycast)
        {
            return;
        }

        Gizmos.color = Color.green;
        Vector3 start = transform.position + _groundCheckOffset;
        Gizmos.DrawLine(start, start + Vector3.down * _groundCheckDistance);
    }
#endif
}
