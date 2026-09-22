using UnityEngine;

/// <summary>
/// 총기 발사 방식 분류
/// </summary>
public enum GunFireMode
{
    SemiAuto = 0,   // 단발
    FullAuto = 1,   // 연사 (마우스 홀드 시 지속 발사)
    Charge = 2      // 차지샷 (홀드 후 뗄 때 발사)
}

/// <summary>
/// 총기 아이템의 스탯, 발사 모드, 탄약/재장전, 반동, 비주얼/오디오 에셋을 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewGunItemData", menuName = "Inventory/Gun Item Data")]
public class GunItemData : ItemData
{
    [Header("Gun Combat Settings")]
    [Tooltip("기본 단발 데미지")]
    [SerializeField] private int _baseDamage = 25;
    [Tooltip("초당 발사 수 또는 발사 주기(초)")]
    [SerializeField] private float _fireInterval = 0.15f;
    [Tooltip("최대 유효 사거리 (미터)")]
    [SerializeField] private float _maxRange = 100f;
    [Tooltip("발사 모드 (단발, 연사, 차지샷)")]
    [SerializeField] private GunFireMode _fireMode = GunFireMode.FullAuto;

    [Header("Charge Shot Settings (차지 모드 전용)")]
    [Tooltip("완전 충전에 필요한 최소 홀드 시간(초)")]
    [SerializeField] private float _minChargeDuration = 1.0f;
    [Tooltip("완전 충전 발사 시 데미지 배율 (기본: 2.0배)")]
    [SerializeField] private float _chargedDamageMultiplier = 2.0f;

    [Header("Ammo & Magazine Settings")]
    [Tooltip("탄창 용량 (최대 장탄수)")]
    [SerializeField] private int _magazineCapacity = 30;
    [Tooltip("재장전에 걸리는 시간(초)")]
    [SerializeField] private float _reloadDuration = 2.0f;
    [Tooltip("인벤토리에서 소모할 탄약 아이템 ID (예: Ammo_Rifle, Ammo_Pistol)")]
    [SerializeField] private string _requiredAmmoItemId = "Ammo_Rifle";

    [Header("Recoil Settings (Screen Kick)")]
    [Tooltip("격발 시 화면이 위로 튕기는 피치 각도")]
    [SerializeField] private float _recoilPitch = 1.5f;
    [Tooltip("격발 시 좌우로 미세하게 흔들리는 요 각도 범위")]
    [SerializeField] private float _recoilYaw = 0.35f;
    [Tooltip("반동 후 원래 조준선 위치로 복귀하는 부드러운 속도")]
    [SerializeField] private float _recoilRecoverySpeed = 10f;

    [Header("Sprint & Movement Penalty Settings")]
    [Tooltip("전력 질주(달리기) 상태에서 사격 시 첫 발 발사까지 걸리는 선딜레이(초)")]
    [SerializeField] private float _sprintToFireDelay = 0.18f;
    [Tooltip("사격 중 이동 속도 배율 (0.75면 기본 걷기 속도의 75%로 이동)")]
    [SerializeField] private float _shootingMovementMultiplier = 0.75f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private AudioClip _dryFireSound;
    [SerializeField] private AudioClip _reloadSound;
    [SerializeField] private AudioClip _chargeStartSound;
    [SerializeField] private AudioClip _chargeReadySound;

    [Header("VFX Prefabs")]
    [SerializeField] private GameObject _muzzleFlashPrefab;
    [SerializeField] private GameObject _bulletTracerPrefab;
    [SerializeField] private GameObject _impactEffectPrefab;

    public int BaseDamage => _baseDamage;
    public float FireInterval => Mathf.Max(0.02f, _fireInterval);
    public float MaxRange => _maxRange;
    public GunFireMode FireMode => _fireMode;

    public float MinChargeDuration => _minChargeDuration;
    public float ChargedDamageMultiplier => _chargedDamageMultiplier;

    public int MagazineCapacity => Mathf.Max(1, _magazineCapacity);
    public float ReloadDuration => Mathf.Max(0.1f, _reloadDuration);
    public string RequiredAmmoItemId => _requiredAmmoItemId;

    public float RecoilPitch => _recoilPitch;
    public float RecoilYaw => _recoilYaw;
    public float RecoilRecoverySpeed => _recoilRecoverySpeed;

    public float SprintToFireDelay => Mathf.Max(0f, _sprintToFireDelay);
    public float ShootingMovementMultiplier => Mathf.Clamp(_shootingMovementMultiplier, 0.1f, 1f);

    public AudioClip FireSound => _fireSound;
    public AudioClip DryFireSound => _dryFireSound;
    public AudioClip ReloadSound => _reloadSound;
    public AudioClip ChargeStartSound => _chargeStartSound;
    public AudioClip ChargeReadySound => _chargeReadySound;

    public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;
    public GameObject BulletTracerPrefab => _bulletTracerPrefab;
    public GameObject ImpactEffectPrefab => _impactEffectPrefab;

    private void OnEnable()
    {
        // GunItemData는 기본적으로 ItemActionType.Gun으로 설정
        SetActionType(ItemActionType.Gun);
    }
}
