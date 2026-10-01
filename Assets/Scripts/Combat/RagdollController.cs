using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 몬스터 사망 시 애니메이터를 끄고 래그돌(물리)을 활성화하여 시체가 자연스럽게 날아가도록 제어합니다.
/// 몬스터 프리팹 최상단(NonPlayerCharacter와 같은 위치)에 부착하여 사용합니다.
/// </summary>
public class RagdollController : NetworkBehaviour
{
    private Rigidbody[] _ragdollRigidbodies;
    private Collider[] _ragdollColliders;
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        
        // 하위 본(Bone)들에 있는 모든 Rigidbody와 Collider를 찾습니다.
        // 메인 캐릭터 컨트롤러/콜라이더는 제외해야 하므로, 루트 오브젝트의 컴포넌트는 무시합니다.
        _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        _ragdollColliders = GetComponentsInChildren<Collider>();
        
        SetRagdollState(false);
    }

    /// <summary>
    /// 래그돌 상태를 켜거나 끕니다.
    /// </summary>
    public void SetRagdollState(bool isActive)
    {
        if (_animator != null)
        {
            _animator.enabled = !isActive;
        }

        foreach (var rb in _ragdollRigidbodies)
        {
            // 루트에 있는 Rigidbody(있을 경우)는 제외
            if (rb.gameObject == gameObject) continue;
            
            rb.isKinematic = !isActive;
            rb.useGravity = isActive;
        }

        foreach (var col in _ragdollColliders)
        {
            // 메인 캡슐 콜라이더나 트리거는 제외
            if (col.gameObject == gameObject) continue;
            
            col.enabled = isActive;
        }
    }

    /// <summary>
    /// 특정 위치(HitPoint)에서 타격 방향으로 래그돌 전체에 물리 힘을 가합니다.
    /// </summary>
    public void ApplyForceToRagdoll(Vector3 force, Vector3 hitPoint)
    {
        // 래그돌이 켜진 상태여야 힘을 받을 수 있습니다.
        SetRagdollState(true);

        foreach (var rb in _ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;

            // 막타 피격 위치 주변의 본(Bone)들에 폭발적인 힘을 가함
            rb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
        }
    }
}
