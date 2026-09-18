using UnityEngine;

/// <summary>
/// 3D 모델 또는 비주얼이 루트의 자식 GameObject에 분리되어 있고 Animator가 자식에 부착된 경우,
/// 애니메이션 이벤트를 상위 또는 지정된 CharacterFootsteps 컴포넌트로 전달(Forwarding)해주는 헬퍼 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public class FootstepEventForwarder : MonoBehaviour
{
    [Tooltip("발걸음 사운드를 처리할 CharacterFootsteps 컴포넌트입니다. 비워둘 경우 부모 객체에서 자동으로 탐색합니다.")]
    [SerializeField] private CharacterFootsteps _targetFootsteps;

    private void Awake()
    {
        EnsureTargetFootsteps();
    }

    private void EnsureTargetFootsteps()
    {
        if (_targetFootsteps == null)
        {
            _targetFootsteps = GetComponentInParent<CharacterFootsteps>();
        }
    }

    #region Animation Event Handlers

    public void OnFootstep()
    {
        EnsureTargetFootsteps();
        if (_targetFootsteps != null)
        {
            _targetFootsteps.OnFootstep();
        }
    }

    public void OnFootstep(AnimationEvent animationEvent)
    {
        EnsureTargetFootsteps();
        if (_targetFootsteps != null)
        {
            _targetFootsteps.OnFootstep(animationEvent);
        }
    }

    public void Footstep()
    {
        EnsureTargetFootsteps();
        if (_targetFootsteps != null)
        {
            _targetFootsteps.Footstep();
        }
    }

    public void Step()
    {
        EnsureTargetFootsteps();
        if (_targetFootsteps != null)
        {
            _targetFootsteps.Step();
        }
    }

    public void PlayFootstepSound()
    {
        EnsureTargetFootsteps();
        if (_targetFootsteps != null)
        {
            _targetFootsteps.PlayFootstepSound();
        }
    }

    public void PlayFootstepSound(string surfaceName)
    {
        EnsureTargetFootsteps();
        if (_targetFootsteps != null)
        {
            _targetFootsteps.PlayFootstepSound(surfaceName);
        }
    }

    #endregion
}
