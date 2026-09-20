using UnityEngine;

namespace Coop.Audio
{
    /// <summary>
    /// 사운드 재생 설정(SoundCue)을 프로젝트 에셋으로 저장하여 여러 버튼이나 오브젝트에서 공유할 수 있도록 해주는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundCue", menuName = "Coop/Audio/Sound Cue Asset", order = 100)]
    public class SoundCueAsset : ScriptableObject
    {
        [SerializeField] private SoundCue _soundCue = new SoundCue();

        public SoundCue Cue => _soundCue;

        /// <summary>
        /// 에셋에 정의된 사운드 큐를 재생합니다.
        /// </summary>
        /// <param name="position">3D 사운드일 경우 재생 위치 (선택 사항)</param>
        /// <returns>재생에 사용된 AudioSource</returns>
        public AudioSource Play(Vector3? position = null)
        {
            return _soundCue?.Play(position);
        }
    }
}
