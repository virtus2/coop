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

    [Header("Shotgun Settings (산탄총 전용)")]
    [Tooltip("산탄총(다중 펠릿 히트스캔 및 거리별 감쇄) 활성화 여부")]
    [SerializeField] private bool _isShotgun = false;
    [Tooltip("1회 발사 시 발사되는 산탄 펠릿 개수 (기본: 8발)")]
    [SerializeField] private int _pelletCount = 8;
    [Tooltip("확산 원뿔 각도 (도, 기본: 7도)")]
    [SerializeField] private float _spreadAngle = 7.0f;
    [Tooltip("최대 데미지가 온전히 들어가는 근거리 한계 (미터, 기본: 3m)")]
    [SerializeField] private float _damageFalloffStartRange = 3.0f;
    [Tooltip("최소 데미지 구간이 시작되는 거리 (미터, 기본: 10m)")]
    [SerializeField] private float _damageFalloffEndRange = 10.0f;
    [Tooltip("원거리 피격 시 펠릿당 보장되는 최소 데미지 (기본: 1)")]
    [SerializeField] private int _minDamagePerPellet = 1;

    [Header("Charge Shot Settings (차지 모드 전용)")]
    [Tooltip("완전 충전에 필요한 최소 홀드 시간(초)")]
    [SerializeField] private float _minChargeDuration = 1.0f;
    [Tooltip("완전 충전 발사 시 데미지 배율 (기본: 2.0배)")]
    [SerializeField] private float _chargedDamageMultiplier = 2.0f;

    [Header("Ammo & Magazine Settings")]
    [Tooltip("탄창 용량 (최대 장탄수)")]
    [SerializeField] private int _magazineCapacity = 30;
    [Tooltip("재장전에 걸리는 시간(초) - 일반 탄창 교체식 무기용")]
    [SerializeField] private float _reloadDuration = 2.0f;
    [Tooltip("인벤토리에서 소모할 탄약 아이템 ID (예: Ammo_Rifle, Ammo_Pistol, Ammo_Shotgun)")]
    [SerializeField] private string _requiredAmmoItemId = "Ammo_Rifle";

    [Header("Shell-by-Shell Reload Settings (1발씩 튜브 장전 전용)")]
    [Tooltip("쉘 바이 쉘(1발씩) 장전 사용 여부")]
    [SerializeField] private bool _useShellByShellReload = false;
    [Tooltip("장전 시작 선딜레이(초) - 약실 열기")]
    [SerializeField] private float _reloadStartDelay = 0.35f;
    [Tooltip("1발 삽입 주기(초) - 쉘 밀어넣기")]
    [SerializeField] private float _reloadInsertInterval = 0.5f;
    [Tooltip("장전 종료 후딜레이(초) - 펌프 차징 및 정자세 복귀")]
    [SerializeField] private float _reloadEndDelay = 0.3f;

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
    [SerializeField] private AudioClip _reloadStartSound;
    [SerializeField] private AudioClip _reloadInsertSound;
    [SerializeField] private AudioClip _reloadEndSound;
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

    public bool IsShotgun => _isShotgun;
    public int PelletCount => Mathf.Max(1, _pelletCount);
    public float SpreadAngle => Mathf.Max(0f, _spreadAngle);
    public float DamageFalloffStartRange => Mathf.Max(0f, _damageFalloffStartRange);
    public float DamageFalloffEndRange => Mathf.Max(_damageFalloffStartRange, _damageFalloffEndRange);
    public int MinDamagePerPellet => Mathf.Max(1, _minDamagePerPellet);

    public bool UseShellByShellReload => _useShellByShellReload;
    public float ReloadStartDelay => Mathf.Max(0f, _reloadStartDelay);
    public float ReloadInsertInterval => Mathf.Max(0.05f, _reloadInsertInterval);
    public float ReloadEndDelay => Mathf.Max(0f, _reloadEndDelay);

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
    public AudioClip ReloadStartSound => _reloadStartSound;
    public AudioClip ReloadInsertSound => _reloadInsertSound;
    public AudioClip ReloadEndSound => _reloadEndSound;
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
