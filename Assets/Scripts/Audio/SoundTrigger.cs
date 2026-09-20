using UnityEngine;

namespace Coop.Audio
{
    /// <summary>
    /// 버튼 외의 씬 내 임의의 오브젝트나 이벤트 발생 시 SoundCue를 재생할 수 있는 범용 컴포넌트입니다.
    /// 인스펙터에서 사운드 에셋 또는 인라인 사운드 설정을 구성하고, UnityEvent 또는 자동 타이밍에 맞춰 재생할 수 있습니다.
    /// </summary>
    public class SoundTrigger : MonoBehaviour
    {
        public enum TriggerTiming
        {
            Manual,
            Awake,
            Start,
            OnEnable,
            OnDisable,
            OnDestroy
        }

        [Header("Sound Configuration")]
        [Tooltip("공유 사운드 에셋을 사용할 경우 지정합니다.")]
        [SerializeField] private SoundCueAsset _soundCueAsset;

        [Tooltip("컴포넌트 자체에 독립적인 사운드 설정을 부여할 때 사용합니다.")]
        [SerializeField] private SoundCue _inlineCue = new SoundCue();

        [Tooltip("체크 시 SoundCueAsset을 우선 사용하고, 미지정 또는 체크 해제 시 인라인 사운드 설정을 사용합니다.")]
        [SerializeField] private bool _useAsset = false;

        [Header("Trigger Options")]
        [Tooltip("컴포넌트 생명주기에 따라 자동으로 사운드를 재생할 시점입니다.")]
        [SerializeField] private TriggerTiming _playTiming = TriggerTiming.Manual;

        [Tooltip("사운드 재생 위치로 본 GameObject의 월드 위치를 사용할지 여부입니다.")]
        [SerializeField] private bool _useGameObjectPosition = true;

        public SoundCue ActiveCue => (_useAsset && _soundCueAsset != null) ? _soundCueAsset.Cue : _inlineCue;

        private void Awake()
        {
            if (_playTiming == TriggerTiming.Awake)
            {
                Play();
            }
        }

        private void Start()
        {
            if (_playTiming == TriggerTiming.Start)
            {
                Play();
            }
        }

        private void OnEnable()
        {
            if (_playTiming == TriggerTiming.OnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            if (_playTiming == TriggerTiming.OnDisable)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            if (_playTiming == TriggerTiming.OnDestroy)
            {
                Play();
            }
        }

        /// <summary>
        /// 구성된 사운드 큐를 재생합니다. (UnityEvent 등에서 인자 없이 호출 가능)
        /// </summary>
        public AudioSource Play()
        {
            Vector3? pos = _useGameObjectPosition ? transform.position : null;
            return ActiveCue?.Play(pos);
        }

        /// <summary>
        /// 지정한 월드 좌표에서 사운드를 재생합니다.
        /// </summary>
        public AudioSource PlayAt(Vector3 position)
        {
            return ActiveCue?.Play(position);
        }
    }
}
