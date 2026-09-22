using UnityEngine;

/// <summary>
/// 개별 콜라이더에 부착하여 헤드샷, 몸통 등 피격 부위를 식별하고
/// 부모 IDamageable 컴포넌트로 데미지 처리를 중계하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Hitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    [Tooltip("피격 부위 유형 (Head 지정 시 1.5배 헤드샷 적용)")]
    [SerializeField] private HitboxType _hitboxType = HitboxType.Body;

    [Tooltip("데미지를 수신할 IDamageable 대상 (비워두면 부모 오브젝트에서 자동 검색)")]
    [SerializeField] private Component _damageableTarget;

    private IDamageable _cachedDamageable;

    public HitboxType Type => _hitboxType;

    public IDamageable Damageable
    {
        get
        {
            if (_cachedDamageable == null)
            {
                if (_damageableTarget != null && _damageableTarget is IDamageable directTarget)
                {
                    _cachedDamageable = directTarget;
                }
                else
                {
                    _cachedDamageable = GetComponentInParent<IDamageable>();
                }
            }
            return _cachedDamageable;
        }
    }

    private void Awake()
    {
        // Collider 캐싱 및 초기화
        if (_damageableTarget != null && _damageableTarget is IDamageable directTarget)
        {
            _cachedDamageable = directTarget;
        }
    }

    public void Setup(HitboxType type, IDamageable owner)
    {
        _hitboxType = type;
        _cachedDamageable = owner;
        if (owner is Component comp)
        {
            _damageableTarget = comp;
        }
    }
}
