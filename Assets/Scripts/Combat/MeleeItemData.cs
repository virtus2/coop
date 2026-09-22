using UnityEngine;

/// <summary>
/// 근접 무기 아이템의 스탯, 공격 타이밍, 판정 범위, 넉백 및 시청각 에셋을 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewMeleeItemData", menuName = "Inventory/Melee Item Data")]
public class MeleeItemData : ItemData
{
    [Header("Melee Combat Settings")]
    [Tooltip("기본 타격 데미지")]
    [SerializeField] private int _baseDamage = 35;
    [Tooltip("전체 공격 주기 또는 쿨다운(초)")]
    [SerializeField] private float _attackInterval = 0.6f;
    [Tooltip("공격 시작 후 실제 타격 판정이 발생하기까지의 선딜레이(초)")]
    [SerializeField] private float _windupDuration = 0.18f;
    [Tooltip("타격 판정 후 다음 행동까지의 후딜레이(초)")]
    [SerializeField] private float _recoveryDuration = 0.35f;

    [Header("Hit Detection Settings")]
    [Tooltip("최대 유효 사거리 (미터)")]
    [SerializeField] private float _attackRange = 2.2f;
    [Tooltip("SphereCast 판정 구체 반경 (미터)")]
    [SerializeField] private float _attackRadius = 0.35f;

    [Header("Impact & Movement Penalty Settings")]
    [Tooltip("타격 시 적을 밀쳐내는 넉백 힘")]
    [SerializeField] private float _knockbackForce = 5.5f;
    [Tooltip("공격 모션 중 이동 속도 배율 (0.5면 기본 속도의 50%)")]
    [SerializeField] private float _movementPenaltyMultiplier = 0.5f;

    [Header("Audio Settings")]
    [Tooltip("무기를 허공에 휘두를 때 재생되는 사운드")]
    [SerializeField] private AudioClip _swingSound;
    [Tooltip("생체/적을 타격했을 때 재생되는 사운드")]
    [SerializeField] private AudioClip _hitFleshSound;
    [Tooltip("파괴 가능한 사물이나 오브젝트를 타격했을 때 재생되는 사운드")]
    [SerializeField] private AudioClip _hitObjectSound;
    [Tooltip("벽이나 장애물에 부딪혔을 때 재생되는 사운드")]
    [SerializeField] private AudioClip _hitWallSound;

    [Header("VFX Prefabs (Optional Override)")]
    [Tooltip("기본 재질별 피격 파티클(VfxPoolManager)을 무시하고 이 무기만의 고유 타격 파티클을 재생하고 싶을 때 할당합니다. 비워둘 경우 타격 표면 재질(SurfaceType)에 맞는 기본 파티클이 재생됩니다.")]
    [SerializeField] private GameObject _impactEffectPrefab;

    public int BaseDamage => _baseDamage;
    public float AttackInterval => Mathf.Max(0.1f, _attackInterval);
    public float WindupDuration => Mathf.Max(0.05f, _windupDuration);
    public float RecoveryDuration => Mathf.Max(0.05f, _recoveryDuration);
    public float AttackRange => Mathf.Max(0.5f, _attackRange);
    public float AttackRadius => Mathf.Max(0.05f, _attackRadius);
    public float KnockbackForce => _knockbackForce;
    public float MovementPenaltyMultiplier => Mathf.Clamp(_movementPenaltyMultiplier, 0.1f, 1f);

    public AudioClip SwingSound => _swingSound;
    public AudioClip HitFleshSound => _hitFleshSound;
    public AudioClip HitObjectSound => _hitObjectSound;
    public AudioClip HitWallSound => _hitWallSound;

    public GameObject ImpactEffectPrefab => _impactEffectPrefab;

    private void OnEnable()
    {
        // MeleeItemData는 기본적으로 ItemActionType.MeleeWeapon으로 설정
        SetActionType(ItemActionType.MeleeWeapon);
    }
}
