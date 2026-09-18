using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Coop.Audio
{
    /// <summary>
    /// 게임 내 배경음(BGM)과 효과음(SFX)을 총괄 관리하는 싱글톤 사운드 매니저입니다.
    /// 1. 16개의 SFX AudioSource 풀링 및 우선순위(SoundPriority) 기반 선점 로직
    /// 2. BGM 듀얼 소스(A/B) 기반 크로스페이드 및 루프 재생
    /// 3. AudioMixer(Master, BGM, SFX) 연동 및 SettingsManager 기반 볼륨 제어
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Audio Mixer Configuration")]
        [Tooltip("오디오 믹서 에셋입니다. 미지정 시에도 AudioSource 볼륨 직접 제어로 안전하게 동작합니다.")]
        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private AudioMixerGroup _masterGroup;
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Header("Mixer Exposed Parameter Names")]
        [SerializeField] private string _masterVolumeParam = "MasterVolume";
        [SerializeField] private string _bgmVolumeParam = "BgmVolume";
        [SerializeField] private string _sfxVolumeParam = "SfxVolume";

        [Header("SFX Pool Configuration")]
        [Tooltip("게임 시작 시 생성할 SFX AudioSource 개수입니다. (기본 16개)")]
        [SerializeField] private int _sfxPoolSize = 16;

        [Header("BGM Settings")]
        [SerializeField] private float _defaultBgmFadeDuration = 1.0f;

        // BGM 듀얼 채널 (크로스페이드 전용)
        private AudioSource _bgmSourceA;
        private AudioSource _bgmSourceB;
        private AudioSource _activeBgmSource;
        private Coroutine _bgmFadeCoroutine;
        private float _currentBgmBaseVolume = 1.0f;

        // SFX 풀 아이템
        private class SfxEntry
        {
            public AudioSource Source;
            public SoundPriority Priority;
            public float PlayStartTime;
            public float BaseVolume = 1.0f;
            public bool IsLooping = false;
        }

        private readonly List<SfxEntry> _sfxPool = new List<SfxEntry>();
        private Transform _sfxContainer;
        private Transform _bgmContainer;

        private const float MIN_DECIBEL = -80f;
        private const float MAX_DECIBEL = 0f;

        private bool _isInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureInitialized();
        }

        /// <summary>
        /// 풀 및 오디오 소스 컨테이너를 초기화합니다. 에디터 테스트 및 런타임 시작 시 안전하게 호출됩니다.
        /// </summary>
        public void EnsureInitialized()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (_isInitialized)
            {
                return;
            }

            InitializeContainers();
            InitializeBgmSources();
            InitializeSfxPool();

            _isInitialized = true;
        }

        private void Start()
        {
            RegisterSettingsEvents();
            ApplyAllVolumesFromSettings();
        }

        private void OnEnable()
        {
            RegisterSettingsEvents();
        }

        private void OnDisable()
        {
            UnregisterSettingsEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnregisterSettingsEvents();
        }

        #region Initialization

        private void InitializeContainers()
        {
            if (_bgmContainer == null)
            {
                Transform existing = transform.Find("BGM_Sources");
                if (existing != null)
                {
                    _bgmContainer = existing;
                }
                else
                {
                    GameObject bgmObj = new GameObject("BGM_Sources");
                    bgmObj.transform.SetParent(transform, false);
                    _bgmContainer = bgmObj.transform;
                }
            }

            if (_sfxContainer == null)
            {
                Transform existing = transform.Find("SFX_Pool");
                if (existing != null)
                {
                    _sfxContainer = existing;
                }
                else
                {
                    GameObject sfxObj = new GameObject("SFX_Pool");
                    sfxObj.transform.SetParent(transform, false);
                    _sfxContainer = sfxObj.transform;
                }
            }
        }

        private void InitializeBgmSources()
        {
            if (_bgmSourceA == null)
            {
                _bgmSourceA = CreateAudioSourceComponent("BGM_Source_A", _bgmContainer, _bgmGroup);
                _bgmSourceA.loop = true;
            }

            if (_bgmSourceB == null)
            {
                _bgmSourceB = CreateAudioSourceComponent("BGM_Source_B", _bgmContainer, _bgmGroup);
                _bgmSourceB.loop = true;
            }

            _activeBgmSource = null;
        }

        private void InitializeSfxPool()
        {
            if (_sfxPool.Count > 0)
            {
                return;
            }

            for (int i = 0; i < _sfxPoolSize; i++)
            {
                AudioSource source = CreateAudioSourceComponent($"SFX_Source_{i:D2}", _sfxContainer, _sfxGroup);
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 기본 2D

                _sfxPool.Add(new SfxEntry
                {
                    Source = source,
                    Priority = SoundPriority.Low,
                    PlayStartTime = 0f,
                    BaseVolume = 1f,
                    IsLooping = false
                });
            }
        }

        private AudioSource CreateAudioSourceComponent(string name, Transform parent, AudioMixerGroup mixerGroup)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (mixerGroup != null)
            {
                source.outputAudioMixerGroup = mixerGroup;
            }
            return source;
        }

        #endregion

        #region SFX Playback & Priority Preemption

        /// <summary>
        /// 2D 효과음을 재생합니다. 유휴 AudioSource가 없으면 우선순위 비교를 통해 선점 재생합니다.
        /// </summary>
        /// <param name="clip">재생할 오디오 클립</param>
        /// <param name="volume">볼륨 배율 (0~1)</param>
        /// <param name="pitch">피치 배율 (기본 1)</param>
        /// <param name="priority">효과음 우선순위</param>
        /// <param name="loop">루프 재생 여부</param>
        /// <returns>재생에 사용된 AudioSource (재생 실패 시 null)</returns>
        public AudioSource PlaySfx(
            AudioClip clip,
            float volume = 1.0f,
            float pitch = 1.0f,
            SoundPriority priority = SoundPriority.Normal,
            bool loop = false)
        {
            if (clip == null)
            {
                return null;
            }

            SfxEntry targetEntry = AcquireSfxSource(priority);
            if (targetEntry == null)
            {
                // 풀에 여유가 없고 기존 재생 중인 사운드가 모두 더 높거나 같은 우선순위인 경우 드롭
                return null;
            }

            AudioSource source = targetEntry.Source;
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f; // 2D
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.loop = loop;

            if (_sfxGroup != null && source.outputAudioMixerGroup != _sfxGroup)
            {
                source.outputAudioMixerGroup = _sfxGroup;
            }

            targetEntry.Priority = priority;
            targetEntry.PlayStartTime = Time.time;
            targetEntry.BaseVolume = volume;
            targetEntry.IsLooping = loop;

            source.Play();
            return source;
        }

        /// <summary>
        /// 3D 월드 좌표에서 효과음을 재생합니다.
        /// </summary>
        /// <param name="clip">재생할 오디오 클립</param>
        /// <param name="position">월드 위치 좌표</param>
        /// <param name="volume">볼륨 배율 (0~1)</param>
        /// <param name="pitch">피치 배율</param>
        /// <param name="priority">효과음 우선순위</param>
        /// <param name="spatialBlend">3D 공간화 배율 (0=2D, 1=완전 3D)</param>
        /// <param name="minDistance">사운드 최소 감쇄 거리</param>
        /// <param name="maxDistance">사운드 최대 도달 거리</param>
        /// <param name="loop">루프 재생 여부</param>
        /// <returns>재생에 사용된 AudioSource (재생 실패 시 null)</returns>
        public AudioSource PlaySfxAt(
            AudioClip clip,
            Vector3 position,
            float volume = 1.0f,
            float pitch = 1.0f,
            SoundPriority priority = SoundPriority.Normal,
            float spatialBlend = 1.0f,
            float minDistance = 1.0f,
            float maxDistance = 50.0f,
            bool loop = false)
        {
            if (clip == null)
            {
                return null;
            }

            SfxEntry targetEntry = AcquireSfxSource(priority);
            if (targetEntry == null)
            {
                return null;
            }

            AudioSource source = targetEntry.Source;
            source.transform.position = position;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.minDistance = Mathf.Max(0.1f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.loop = loop;

            if (_sfxGroup != null && source.outputAudioMixerGroup != _sfxGroup)
            {
                source.outputAudioMixerGroup = _sfxGroup;
            }

            targetEntry.Priority = priority;
            targetEntry.PlayStartTime = Time.time;
            targetEntry.BaseVolume = volume;
            targetEntry.IsLooping = loop;

            source.Play();
            return source;
        }

        /// <summary>
        /// 피치 범위(minPitch ~ maxPitch) 내에서 무작위 피치를 적용하여 2D 효과음을 재생합니다.
        /// </summary>
        public AudioSource PlaySfxWithRandomPitch(
            AudioClip clip,
            float volume = 1.0f,
            float minPitch = 0.9f,
            float maxPitch = 1.1f,
            SoundPriority priority = SoundPriority.Normal,
            bool loop = false)
        {
            float randomPitch = UnityEngine.Random.Range(minPitch, maxPitch);
            return PlaySfx(clip, volume, randomPitch, priority, loop);
        }

        /// <summary>
        /// 피치 범위(minPitch ~ maxPitch) 내에서 무작위 피치를 적용하여 3D 위치 효과음을 재생합니다.
        /// </summary>
        public AudioSource PlaySfxWithRandomPitchAt(
            AudioClip clip,
            Vector3 position,
            float volume = 1.0f,
            float minPitch = 0.9f,
            float maxPitch = 1.1f,
            SoundPriority priority = SoundPriority.Normal,
            float spatialBlend = 1.0f,
            float minDistance = 1.0f,
            float maxDistance = 50.0f,
            bool loop = false)
        {
            float randomPitch = UnityEngine.Random.Range(minPitch, maxPitch);
            return PlaySfxAt(clip, position, volume, randomPitch, priority, spatialBlend, minDistance, maxDistance, loop);
        }

        /// <summary>
        /// 제공된 오디오 클립 목록 중 무작위로 하나를 선택하여 2D 효과음을 재생합니다.
        /// </summary>
        public AudioSource PlayRandomSfx(
            IReadOnlyList<AudioClip> clips,
            float volume = 1.0f,
            float pitch = 1.0f,
            SoundPriority priority = SoundPriority.Normal,
            bool loop = false)
        {
            AudioClip clip = PickRandomClip(clips);
            return PlaySfx(clip, volume, pitch, priority, loop);
        }

        /// <summary>
        /// 제공된 오디오 클립 목록 중 무작위로 하나를 선택하고, 무작위 피치를 적용하여 2D 효과음을 재생합니다.
        /// </summary>
        public AudioSource PlayRandomSfxWithRandomPitch(
            IReadOnlyList<AudioClip> clips,
            float volume = 1.0f,
            float minPitch = 0.9f,
            float maxPitch = 1.1f,
            SoundPriority priority = SoundPriority.Normal,
            bool loop = false)
        {
            AudioClip clip = PickRandomClip(clips);
            float randomPitch = UnityEngine.Random.Range(minPitch, maxPitch);
            return PlaySfx(clip, volume, randomPitch, priority, loop);
        }

        /// <summary>
        /// 제공된 오디오 클립 목록 중 무작위로 하나를 선택하여 3D 위치 효과음을 재생합니다.
        /// </summary>
        public AudioSource PlayRandomSfxAt(
            IReadOnlyList<AudioClip> clips,
            Vector3 position,
            float volume = 1.0f,
            float pitch = 1.0f,
            SoundPriority priority = SoundPriority.Normal,
            float spatialBlend = 1.0f,
            float minDistance = 1.0f,
            float maxDistance = 50.0f,
            bool loop = false)
        {
            AudioClip clip = PickRandomClip(clips);
            return PlaySfxAt(clip, position, volume, pitch, priority, spatialBlend, minDistance, maxDistance, loop);
        }

        /// <summary>
        /// 제공된 오디오 클립 목록 중 무작위로 하나를 선택하고, 무작위 피치를 적용하여 3D 위치 효과음을 재생합니다.
        /// </summary>
        public AudioSource PlayRandomSfxWithRandomPitchAt(
            IReadOnlyList<AudioClip> clips,
            Vector3 position,
            float volume = 1.0f,
            float minPitch = 0.9f,
            float maxPitch = 1.1f,
            SoundPriority priority = SoundPriority.Normal,
            float spatialBlend = 1.0f,
            float minDistance = 1.0f,
            float maxDistance = 50.0f,
            bool loop = false)
        {
            AudioClip clip = PickRandomClip(clips);
            float randomPitch = UnityEngine.Random.Range(minPitch, maxPitch);
            return PlaySfxAt(clip, position, volume, randomPitch, priority, spatialBlend, minDistance, maxDistance, loop);
        }

        /// <summary>
        /// 유효한(null이 아닌) 오디오 클립 목록 중 임의의 클립 하나를 선택합니다.
        /// </summary>
        public static AudioClip PickRandomClip(IReadOnlyList<AudioClip> clips)
        {
            if (clips == null || clips.Count == 0)
            {
                return null;
            }

            int count = clips.Count;
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

        /// <summary>
        /// 풀에서 사용 가능한 AudioSource를 가져옵니다.
        /// 1. 재생 중이지 않은 AudioSource가 있다면 즉시 반환
        /// 2. 모두 재생 중이라면, 가장 우선순위가 낮은 사운드 탐색
        ///    - lowestPriority < incomingPriority 일 경우 해당 사운드를 중단(선점)하고 반환
        ///    - 만약 우선순위가 같거나 높다면 재생 거절(null 반환)
        /// </summary>
        private SfxEntry AcquireSfxSource(SoundPriority incomingPriority)
        {
            SfxEntry lowestPriorityActiveEntry = null;
            float oldestPlayTime = float.MaxValue;

            // 1단계: 유휴 AudioSource 탐색
            for (int i = 0; i < _sfxPool.Count; i++)
            {
                SfxEntry entry = _sfxPool[i];
                if (entry.Source == null)
                {
                    continue;
                }

                if (!entry.Source.isPlaying)
                {
                    return entry;
                }

                // 2단계를 위한 최하위 우선순위 및 가장 오래 재생된 엔트리 추적
                if (lowestPriorityActiveEntry == null || entry.Priority < lowestPriorityActiveEntry.Priority)
                {
                    lowestPriorityActiveEntry = entry;
                    oldestPlayTime = entry.PlayStartTime;
                }
                else if (entry.Priority == lowestPriorityActiveEntry.Priority)
                {
                    // 동일 우선순위라면 더 오래 재생된 쪽을 선점 후보로 선택
                    if (entry.PlayStartTime < oldestPlayTime)
                    {
                        lowestPriorityActiveEntry = entry;
                        oldestPlayTime = entry.PlayStartTime;
                    }
                }
            }

            // 모든 소스가 재생 중인 경우: 우선순위 비교
            if (lowestPriorityActiveEntry != null && lowestPriorityActiveEntry.Priority < incomingPriority)
            {
                // 낮은 우선순위의 사운드를 중단하고 새 사운드에 양보 (Preemption)
                lowestPriorityActiveEntry.Source.Stop();
                return lowestPriorityActiveEntry;
            }

            // 선점할 수 없는 경우 (재생 중인 모든 사운드가 새 사운드보다 우선순위가 높거나 같음)
            return null;
        }

        /// <summary>
        /// 특정 오디오 소스의 SFX 재생을 중단합니다.
        /// </summary>
        public void StopSfx(AudioSource source)
        {
            if (source != null && source.isPlaying)
            {
                source.Stop();
            }
        }

        /// <summary>
        /// 현재 풀에서 재생 중인 모든 효과음을 즉시 중단합니다.
        /// </summary>
        public void StopAllSfx()
        {
            for (int i = 0; i < _sfxPool.Count; i++)
            {
                if (_sfxPool[i]?.Source != null && _sfxPool[i].Source.isPlaying)
                {
                    _sfxPool[i].Source.Stop();
                }
            }
        }

        #endregion

        #region BGM Playback & Dual-Channel Crossfade

        /// <summary>
        /// 현재 재생 중인 BGM 클립입니다.
        /// </summary>
        public AudioClip CurrentBgmClip => _activeBgmSource != null ? _activeBgmSource.clip : null;

        /// <summary>
        /// BGM이 현재 재생 중인지 여부입니다.
        /// </summary>
        public bool IsBgmPlaying => _activeBgmSource != null && _activeBgmSource.isPlaying;

        /// <summary>
        /// 배경음(BGM)을 재생합니다. 이전 BGM이 재생 중일 경우 부드럽게 크로스페이드(Crossfade)합니다.
        /// </summary>
        /// <param name="clip">재생할 배경음 클립</param>
        /// <param name="fadeDuration">페이드 시간 (초). 0 이하면 즉시 전환</param>
        /// <param name="loop">루프 재생 여부</param>
        /// <param name="targetVolume">목표 볼륨 (0~1)</param>
        public void PlayBgm(
            AudioClip clip,
            float fadeDuration = -1f,
            bool loop = true,
            float targetVolume = 1.0f)
        {
            if (fadeDuration < 0f)
            {
                fadeDuration = _defaultBgmFadeDuration;
            }

            targetVolume = Mathf.Clamp01(targetVolume);
            _currentBgmBaseVolume = targetVolume;

            if (clip == null)
            {
                StopBgm(fadeDuration);
                return;
            }

            // 이미 동일한 클립이 재생 중이라면 볼륨만 보정하고 유지
            if (_activeBgmSource != null && _activeBgmSource.clip == clip && _activeBgmSource.isPlaying)
            {
                _activeBgmSource.loop = loop;
                _activeBgmSource.volume = targetVolume;
                return;
            }

            // 다음 재생 대상 소스 (A <-> B 번갈아 사용)
            AudioSource nextSource = (_activeBgmSource == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;
            AudioSource prevSource = _activeBgmSource;

            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
                _bgmFadeCoroutine = null;
            }

            if (fadeDuration <= 0f)
            {
                if (prevSource != null)
                {
                    prevSource.Stop();
                    prevSource.clip = null;
                }

                nextSource.clip = clip;
                nextSource.loop = loop;
                nextSource.volume = targetVolume;
                nextSource.Play();
                _activeBgmSource = nextSource;
            }
            else
            {
                _bgmFadeCoroutine = StartCoroutine(CrossfadeBgmRoutine(prevSource, nextSource, clip, loop, targetVolume, fadeDuration));
            }
        }

        private IEnumerator CrossfadeBgmRoutine(
            AudioSource prevSource,
            AudioSource nextSource,
            AudioClip nextClip,
            bool loop,
            float targetVolume,
            float duration)
        {
            nextSource.clip = nextClip;
            nextSource.loop = loop;
            nextSource.volume = 0f;
            nextSource.Play();

            float prevStartVol = (prevSource != null && prevSource.isPlaying) ? prevSource.volume : 0f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime; // 일시정지 중에도 페이드 가능하도록 unscaledDeltaTime 사용
                float t = Mathf.Clamp01(timer / duration);

                if (prevSource != null && prevSource.isPlaying)
                {
                    prevSource.volume = Mathf.Lerp(prevStartVol, 0f, t);
                }

                nextSource.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            if (prevSource != null)
            {
                prevSource.Stop();
                prevSource.clip = null;
                prevSource.volume = 0f;
            }

            nextSource.volume = targetVolume;
            _activeBgmSource = nextSource;
            _bgmFadeCoroutine = null;
        }

        /// <summary>
        /// 재생 중인 배경음을 페이드아웃하며 정지합니다.
        /// </summary>
        /// <param name="fadeDuration">페이드아웃 시간 (0 이하면 즉시 정지)</param>
        public void StopBgm(float fadeDuration = -1f)
        {
            if (fadeDuration < 0f)
            {
                fadeDuration = _defaultBgmFadeDuration;
            }

            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
                _bgmFadeCoroutine = null;
            }

            if (_activeBgmSource == null || !_activeBgmSource.isPlaying)
            {
                return;
            }

            if (fadeDuration <= 0f)
            {
                _activeBgmSource.Stop();
                _activeBgmSource.clip = null;
                _activeBgmSource = null;
            }
            else
            {
                _bgmFadeCoroutine = StartCoroutine(FadeOutBgmRoutine(_activeBgmSource, fadeDuration));
            }
        }

        private IEnumerator FadeOutBgmRoutine(AudioSource source, float duration)
        {
            float startVol = source.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                source.volume = Mathf.Lerp(startVol, 0f, t);
                yield return null;
            }

            source.Stop();
            source.clip = null;
            source.volume = 0f;
            if (_activeBgmSource == source)
            {
                _activeBgmSource = null;
            }
            _bgmFadeCoroutine = null;
        }

        /// <summary>
        /// 배경음을 일시 정지합니다.
        /// </summary>
        public void PauseBgm()
        {
            if (_activeBgmSource != null && _activeBgmSource.isPlaying)
            {
                _activeBgmSource.Pause();
            }
        }

        /// <summary>
        /// 일시 정지된 배경음을 재개합니다.
        /// </summary>
        public void ResumeBgm()
        {
            if (_activeBgmSource != null)
            {
                _activeBgmSource.UnPause();
            }
        }

        #endregion

        #region AudioMixer & Volume Control

        /// <summary>
        /// 선형 볼륨(0~1)을 AudioMixer 데시벨(-80dB ~ 0dB)로 변환합니다.
        /// </summary>
        public static float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f)
            {
                return MIN_DECIBEL;
            }
            return Mathf.Log10(linear) * 20.0f;
        }

        /// <summary>
        /// 마스터 볼륨(0~1)을 설정합니다.
        /// </summary>
        public void SetMasterVolume(float linearVolume)
        {
            linearVolume = Mathf.Clamp01(linearVolume);
            if (_audioMixer != null && !string.IsNullOrEmpty(_masterVolumeParam))
            {
                _audioMixer.SetFloat(_masterVolumeParam, LinearToDecibel(linearVolume));
            }
        }

        /// <summary>
        /// 배경음 볼륨(0~1)을 설정합니다.
        /// </summary>
        public void SetBgmVolume(float linearVolume)
        {
            linearVolume = Mathf.Clamp01(linearVolume);
            if (_audioMixer != null && !string.IsNullOrEmpty(_bgmVolumeParam))
            {
                _audioMixer.SetFloat(_bgmVolumeParam, LinearToDecibel(linearVolume));
            }
            else
            {
                // Fallback: AudioMixer가 없을 때 직접 AudioSource 볼륨 조절
                if (_activeBgmSource != null)
                {
                    _activeBgmSource.volume = _currentBgmBaseVolume * linearVolume;
                }
            }
        }

        /// <summary>
        /// 효과음 볼륨(0~1)을 설정합니다.
        /// </summary>
        public void SetSfxVolume(float linearVolume)
        {
            linearVolume = Mathf.Clamp01(linearVolume);
            if (_audioMixer != null && !string.IsNullOrEmpty(_sfxVolumeParam))
            {
                _audioMixer.SetFloat(_sfxVolumeParam, LinearToDecibel(linearVolume));
            }
            else
            {
                // Fallback: AudioMixer가 없을 때 풀 소스 볼륨 업데이트
                for (int i = 0; i < _sfxPool.Count; i++)
                {
                    var entry = _sfxPool[i];
                    if (entry?.Source != null && entry.Source.isPlaying)
                    {
                        entry.Source.volume = entry.BaseVolume * linearVolume;
                    }
                }
            }
        }

        private void RegisterSettingsEvents()
        {
            SettingsManager.OnMasterVolumeChanged -= SetMasterVolume;
            SettingsManager.OnMasterVolumeChanged += SetMasterVolume;

            SettingsManager.OnBgmVolumeChanged -= SetBgmVolume;
            SettingsManager.OnBgmVolumeChanged += SetBgmVolume;

            SettingsManager.OnSfxVolumeChanged -= SetSfxVolume;
            SettingsManager.OnSfxVolumeChanged += SetSfxVolume;
        }

        private void UnregisterSettingsEvents()
        {
            SettingsManager.OnMasterVolumeChanged -= SetMasterVolume;
            SettingsManager.OnBgmVolumeChanged -= SetBgmVolume;
            SettingsManager.OnSfxVolumeChanged -= SetSfxVolume;
        }

        private void ApplyAllVolumesFromSettings()
        {
            SetMasterVolume(SettingsManager.MasterVolume);
            SetBgmVolume(SettingsManager.BgmVolume);
            SetSfxVolume(SettingsManager.SfxVolume);
        }

        #endregion

        #region Mixer Group Assignment Helper

        /// <summary>
        /// 런타임 또는 에디터에서 AudioMixer 및 그룹들을 바인딩합니다.
        /// </summary>
        public void SetMixerGroups(AudioMixer mixer, AudioMixerGroup masterGroup, AudioMixerGroup bgmGroup, AudioMixerGroup sfxGroup)
        {
            _audioMixer = mixer;
            _masterGroup = masterGroup;
            _bgmGroup = bgmGroup;
            _sfxGroup = sfxGroup;

            if (_bgmSourceA != null) _bgmSourceA.outputAudioMixerGroup = _bgmGroup;
            if (_bgmSourceB != null) _bgmSourceB.outputAudioMixerGroup = _bgmGroup;

            for (int i = 0; i < _sfxPool.Count; i++)
            {
                if (_sfxPool[i]?.Source != null)
                {
                    _sfxPool[i].Source.outputAudioMixerGroup = _sfxGroup;
                }
            }

            ApplyAllVolumesFromSettings();
        }

        #endregion
    }
}
