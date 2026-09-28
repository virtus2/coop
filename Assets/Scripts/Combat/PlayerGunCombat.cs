using System;
using System.Collections.Generic;
using Coop.VFX;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 샷건 쉘 바이 쉘(1발씩) 장전 단계
/// </summary>
public enum ShellReloadStage
{
    None = 0,
    Starting = 1,
    Inserting = 2,
    Ending = 3
}

/// <summary>
/// 네트워크 동기화용 총기 행동 상태 (대기, 재장전, 차징)
/// </summary>
public enum GunCombatActionState : byte
{
    Idle = 0,
    Reloading = 1,
    Charging = 2
}

/// <summary>
/// 플레이어의 총기 사격(단발, 연사, 차지샷, 샷건 산탄), 재장전(탄창형, 쉘 바이 쉘), 반동, 서버 권한 히트스캔 판정 및 동기화를 총괄하는 컴포넌트입니다.
/// </summary>
public class PlayerGunCombat : NetworkBehaviour
{
    public static PlayerGunCombat LocalInstance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private PlayerItemHolder _playerItemHolder;
    [SerializeField] private PlayerInteraction _playerInteraction;

    [Header("Default Fallback Effects")]
    [SerializeField] private GameObject _defaultImpactEffectPrefab;

    [Header("Debug Settings (F1 키로 언제든 지급 가능)")]
    [Tooltip("체크 시 게임 시작 시 인벤토리에 총기와 탄약이 없다면 자동으로 테스트 총기 및 탄약을 지급합니다.")]
    [SerializeField] private bool _autoEquipGunsOnStart = true;

    private Camera _mainCamera;
    private Animator _animator;
    private GunItemData _currentGunData;
    private InventorySlot _currentGunSlot;

    // 사격 및 쿨타임 상태
    private float _fireCooldownTimer;
    private bool _isFiringInputHeld;

    // 차지샷 상태
    private bool _isCharging;
    private float _currentChargeTime;
    private bool _hasPlayedChargeReadySound;

    // 일반 탄창 재장전 상태
    private bool _isReloading;
    private float _reloadTimer;

    // 쉘 바이 쉘(1발씩) 장전 상태 (샷건 전용)
    private ShellReloadStage _shellReloadStage = ShellReloadStage.None;
    private float _shellReloadTimer;

    // 달리기(Sprint) 상호작용 및 선딜레이 상태
    private float _sprintToFireTimer;
    private bool _requireMouseReleaseToFire;

    // HUD 및 외부 연동용 프로퍼티 및 이벤트
    public GunItemData CurrentGunData => _currentGunData;
    public bool HasGunEquipped => _currentGunData != null;
    public int CurrentAmmoInClip => _currentGunSlot != null ? _currentGunSlot.CurrentAmmo : 0;
    public int TotalReserveAmmo => _currentGunData != null && _playerInventory != null
        ? _playerInventory.GetTotalAmmoCount(_currentGunData.RequiredAmmoItemId)
        : 0;
    public bool IsReloading => _isReloading || _shellReloadStage != ShellReloadStage.None;
    public ShellReloadStage CurrentShellReloadStage => _shellReloadStage;
    public float ReloadProgress
    {
        get
        {
            if (_currentGunData == null) return 0f;
            if (_isReloading)
            {
                return Mathf.Clamp01(_reloadTimer / _currentGunData.ReloadDuration);
            }
            if (_shellReloadStage != ShellReloadStage.None)
            {
                return (float)CurrentAmmoInClip / _currentGunData.MagazineCapacity;
            }
            return 0f;
        }
    }
    public bool IsCharging => _isCharging;
    public float ChargeProgress => _currentGunData != null && _isCharging
        ? Mathf.Clamp01(_currentChargeTime / _currentGunData.MinChargeDuration)
        : 0f;

    private readonly NetworkVariable<GunCombatActionState> _networkCombatActionState = new NetworkVariable<GunCombatActionState>(
        GunCombatActionState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public GunCombatActionState CombatActionState => (!IsSpawned || IsOwner) ? GetCurrentCombatActionState() : _networkCombatActionState.Value;

    public event Action OnAmmoChanged;
    public event Action OnReloadStarted;
    public event Action OnReloadCompleted;
    public event Action OnReloadCancelled;
    public event Action<GunCombatActionState> OnCombatActionStateChanged;

    public GunCombatActionState GetCurrentCombatActionState()
    {
        if (_isReloading || _shellReloadStage != ShellReloadStage.None)
        {
            return GunCombatActionState.Reloading;
        }
        if (_isCharging)
        {
            return GunCombatActionState.Charging;
        }
        return GunCombatActionState.Idle;
    }

    private void UpdateCombatActionStateSync()
    {
        if (IsSpawned && IsOwner)
        {
            GunCombatActionState state = GetCurrentCombatActionState();
            if (_networkCombatActionState.Value != state)
            {
                _networkCombatActionState.Value = state;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        LocalInstance = null;
    }

    private void Awake()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        EnsureInventoryReference();
        if (_playerItemHolder == null) _playerItemHolder = GetComponent<PlayerItemHolder>();
        if (_playerInteraction == null) _playerInteraction = GetComponent<PlayerInteraction>();
        _animator = GetComponent<Animator>();

        var pc = GetComponent<PlayerCharacter>();
        if (pc != null)
        {
            pc.OnInventoryBound -= HandleInventoryBound;
            pc.OnInventoryBound += HandleInventoryBound;
        }
    }

    private void EnsureInventoryReference()
    {
        if (_playerInventory == null)
        {
            _playerInventory = GetComponent<PlayerInventory>();
            if (_playerInventory == null)
            {
                var pc = GetComponent<PlayerCharacter>();
                if (pc != null && pc.Inventory != null)
                {
                    _playerInventory = pc.Inventory;
                }
                else if (IsOwner && PlayerInventory.LocalInstance != null)
                {
                    _playerInventory = PlayerInventory.LocalInstance;
                }
            }
        }
    }

    private void HandleInventoryBound(PlayerInventory inventory)
    {
        if (_playerInventory != null)
        {
            _playerInventory.OnSelectedToolbarSlotChanged -= HandleToolbarSlotChanged;
            _playerInventory.OnInventoryChanged -= HandleInventoryChanged;
        }
        _playerInventory = inventory;
        if (_playerInventory != null && (!IsSpawned || IsOwner))
        {
            _playerInventory.OnSelectedToolbarSlotChanged += HandleToolbarSlotChanged;
            _playerInventory.OnInventoryChanged += HandleInventoryChanged;
            CheckCurrentHeldGun();
        }
    }

    private void Start()
    {
        if (!IsSpawned || IsOwner)
        {
            LocalInstance = this;
            _mainCamera = Camera.main;
            AmmoHUD.EnsureInstance();

            if (_playerInventory != null)
            {
                _playerInventory.OnSelectedToolbarSlotChanged += HandleToolbarSlotChanged;
                _playerInventory.OnInventoryChanged += HandleInventoryChanged;
            }

            if (_autoEquipGunsOnStart)
            {
                CheckAutoEquipGuns();
            }

            CheckCurrentHeldGun();
        }
    }

    public override void OnNetworkSpawn()
    {
        _networkCombatActionState.OnValueChanged += HandleNetworkCombatActionStateChanged;

        if (IsOwner)
        {
            LocalInstance = this;
            _mainCamera = Camera.main;
            AmmoHUD.EnsureInstance();

            if (_playerInventory != null)
            {
                _playerInventory.OnSelectedToolbarSlotChanged += HandleToolbarSlotChanged;
                _playerInventory.OnInventoryChanged += HandleInventoryChanged;
            }

            if (_autoEquipGunsOnStart)
            {
                CheckAutoEquipGuns();
            }

            CheckCurrentHeldGun();
        }
        else
        {
            if (_networkCombatActionState.Value != GunCombatActionState.Idle)
            {
                HandleNetworkCombatActionStateChanged(GunCombatActionState.Idle, _networkCombatActionState.Value);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        _networkCombatActionState.OnValueChanged -= HandleNetworkCombatActionStateChanged;

        if (IsOwner)
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnSelectedToolbarSlotChanged -= HandleToolbarSlotChanged;
                _playerInventory.OnInventoryChanged -= HandleInventoryChanged;
            }
            if (LocalInstance == this)
            {
                LocalInstance = null;
            }
        }
        base.OnNetworkDespawn();
    }

    private void HandleNetworkCombatActionStateChanged(GunCombatActionState previousState, GunCombatActionState newState)
    {
        OnCombatActionStateChanged?.Invoke(newState);

        if (IsOwner) return;

        switch (newState)
        {
            case GunCombatActionState.Reloading:
                OnReloadStarted?.Invoke();
                break;
            case GunCombatActionState.Charging:
                break;
            case GunCombatActionState.Idle:
                if (previousState == GunCombatActionState.Reloading)
                {
                    OnReloadCompleted?.Invoke();
                }
                break;
        }
    }

    private void Update()
    {
        if (IsSpawned && !IsOwner) return;

        CheckDebugInput();

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        UpdateTimers();
        HandleCombatInput();
        UpdateCombatActionStateSync();
    }

    private void UpdateTimers()
    {
        if (_fireCooldownTimer > 0f)
        {
            _fireCooldownTimer -= Time.deltaTime;
        }

        if (_sprintToFireTimer > 0f)
        {
            _sprintToFireTimer -= Time.deltaTime;
        }

        // 마우스 좌클릭을 뗐다면 재클릭 요구 해제
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _requireMouseReleaseToFire = false;
        }

        // 일반 탄창 재장전 타이머
        if (_isReloading)
        {
            _reloadTimer += Time.deltaTime;
            if (_reloadTimer >= _currentGunData.ReloadDuration)
            {
                CompleteReload();
            }
        }

        // 쉘 바이 쉘(1발씩) 장전 타이머 및 단계 전환
        if (_shellReloadStage != ShellReloadStage.None)
        {
            UpdateShellReload();
        }

        if (_isCharging && _currentGunData != null)
        {
            _currentChargeTime += Time.deltaTime;
            if (!_hasPlayedChargeReadySound && _currentChargeTime >= _currentGunData.MinChargeDuration)
            {
                _hasPlayedChargeReadySound = true;
                if (_currentGunData.ChargeReadySound != null)
                {
                    AudioSource.PlayClipAtPoint(_currentGunData.ChargeReadySound, _mainCamera.transform.position);
                }
            }
        }
    }

    #region Input Handling

    private void HandleCombatInput()
    {
        if (_currentGunData == null) return;

        bool isSprinting = _playerController != null && _playerController.IsSprinting;

        // 1. 재장전 도중 달리기(Shift) 시도 시 재장전 즉시 취소 (E-06, E-14)
        if (isSprinting)
        {
            if (_isReloading)
            {
                CancelReload();
            }
            if (_shellReloadStage != ShellReloadStage.None)
            {
                CancelShellReload();
            }
            if (_isCharging)
            {
                CancelCharge();
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                _requireMouseReleaseToFire = true;
            }
        }

        // 2. 쉘 바이 쉘 장전 중 좌클릭 입력 처리 (E-04, E-05)
        if (_shellReloadStage != ShellReloadStage.None)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckInterruptSprintOnAttack();
                if (CurrentAmmoInClip <= 0)
                {
                    // 0발 빈 총 상태에서 1발 삽입 전 클릭: 장전 즉시 캔슬 + 빈총 소리 (E-04)
                    CancelShellReload();
                    PlayDryFireSound();
                    return;
                }
                else
                {
                    // 1발 이상 들어간 상태에서 장전 도중 클릭: 딜레이 0초로 즉시 장전을 끊고 바로 탕! 격발 (E-05)
                    CancelShellReload();
                    TryFire(false);
                    return;
                }
            }

            // 사격 입력이 없다면 쉘 바이 쉘 장전 계속 진행
            return;
        }

        // 3. 일반 탄창 재장전 진행 중에는 발사 입력 무시하고 재장전 계속 진행 (E-03)
        if (_isReloading)
        {
            return;
        }

        // 4. 수동 재장전 입력 (R 키)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            TryStartReload();
            return;
        }

        // 5. 차징 도중 마우스 우클릭 취소 (E-05)
        if (_isCharging && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelCharge();
            return;
        }

        // 6. 발사 모드별 입력 처리
        switch (_currentGunData.FireMode)
        {
            case GunFireMode.SemiAuto:
                HandleSemiAutoInput();
                break;
            case GunFireMode.FullAuto:
                HandleFullAutoInput();
                break;
            case GunFireMode.Charge:
                HandleChargeInput();
                break;
        }
    }

    private void CheckInterruptSprintOnAttack()
    {
        if (_playerController != null && _playerController.IsSprinting)
        {
            float delay = _currentGunData != null ? _currentGunData.SprintToFireDelay : 0.18f;
            _playerController.CancelSprint(delay + 0.1f);
            _sprintToFireTimer = delay;
        }
    }

    private void HandleSemiAutoInput()
    {
        bool wasPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (wasPressed)
        {
            CheckInterruptSprintOnAttack();
            TryFire(false);
        }
    }

    private void HandleFullAutoInput()
    {
        bool isPressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool wasPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (wasPressed)
        {
            CheckInterruptSprintOnAttack();
        }

        if (isPressed)
        {
            if (_playerController != null && _playerController.IsSprinting)
            {
                CheckInterruptSprintOnAttack();
            }

            if (_fireCooldownTimer <= 0f)
            {
                TryFire(false);
            }
        }
    }

    private void HandleChargeInput()
    {
        bool wasPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool wasReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        if (wasPressed)
        {
            CheckInterruptSprintOnAttack();

            if (_requireMouseReleaseToFire)
            {
                return;
            }

            // 탄약이 1발이라도 있어야 차징 시작
            if (CurrentAmmoInClip > 0)
            {
                _isCharging = true;
                _currentChargeTime = 0f;
                _hasPlayedChargeReadySound = false;

                if (_currentGunData.ChargeStartSound != null)
                {
                    AudioSource.PlayClipAtPoint(_currentGunData.ChargeStartSound, _mainCamera.transform.position);
                }
            }
            else
            {
                PlayDryFireSound();
            }
        }

        if (_isCharging && wasReleased)
        {
            bool isFullCharge = _currentChargeTime >= _currentGunData.MinChargeDuration;
            _isCharging = false;
            _currentChargeTime = 0f;

            TryFire(isFullCharge);
        }
    }

    public void CancelCharge()
    {
        if (_isCharging)
        {
            _isCharging = false;
            _currentChargeTime = 0f;
            UpdateCombatActionStateSync();
            Debug.Log("[PlayerGunCombat] 차징 취소됨.");
        }
    }

    #endregion

    #region Firing & Prediction

    private void TryFire(bool isCharged)
    {
        if (_currentGunData == null || _currentGunSlot == null) return;

        // 달리기 중 사격 캔슬 후 마우스 재클릭 요구 (상황 A - E-12)
        if (_requireMouseReleaseToFire) return;

        // 달리기 후 첫 발 선딜레이 진행 중 (E-13)
        if (_sprintToFireTimer > 0f) return;

        // 사격 쿨타임 검사
        if (_fireCooldownTimer > 0f) return;

        // 잔탄수 검사 (E-01)
        if (_currentGunSlot.CurrentAmmo <= 0)
        {
            PlayDryFireSound();
            _fireCooldownTimer = 0.2f;
            return;
        }

        // 1. 탄약 1발 소모 (클라이언트 즉시 반영 예측)
        _currentGunSlot.CurrentAmmo--;
        _fireCooldownTimer = _currentGunData.FireInterval;
        OnAmmoChanged?.Invoke();

        // 2. 사격 시 달리기 억제 및 이동 속도 감속 페널티 적용 (75% 속도)
        if (_playerController != null)
        {
            float penaltyDuration = _currentGunData.FireInterval + 0.08f;
            _playerController.CancelSprint(penaltyDuration);
            _playerController.ApplyShootingPenalty(_currentGunData.ShootingMovementMultiplier, penaltyDuration);
        }

        // 3. 로컬 비주얼 및 오디오 즉각 재생 (0ms 체감) 및 서버 RPC 전송
        Ray aimRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (_currentGunData.IsShotgun)
        {
            // 샷건: 난수 시드 생성 및 로컬 즉시 시각/청각 피드백 (8발 Tracer 포함)
            int randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            ExecuteLocalShotgunPrediction(aimRay.origin, aimRay.direction, randomSeed);
            RequestShotgunFireServerRpc(aimRay.origin, aimRay.direction, randomSeed, isCharged, NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            // 일반 총기 로컬 예측 및 서버 단일 레이캐스트 요청
            ExecuteLocalFirePrediction(isCharged);
            RequestFireServerRpc(aimRay.origin, aimRay.direction, isCharged, NetworkManager.Singleton.LocalClientId);
        }
    }

    private void ExecuteLocalShotgunPrediction(Vector3 origin, Vector3 direction, int seed)
    {
        if (_currentGunData == null) return;

        // 격발음 재생
        if (_currentGunData.FireSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentGunData.FireSound, _mainCamera.transform.position);
        }

        // 반동 적용
        if (_playerController != null)
        {
            _playerController.ApplyRecoil(_currentGunData.RecoilPitch, _currentGunData.RecoilYaw, _currentGunData.RecoilRecoverySpeed);
        }

        // 총구 화염 스폰
        Transform holdPoint = _playerInteraction != null ? _playerInteraction.HoldPoint : transform;
        if (_currentGunData.MuzzleFlashPrefab != null && holdPoint != null)
        {
            Instantiate(_currentGunData.MuzzleFlashPrefab, holdPoint.position + holdPoint.forward * 0.4f, holdPoint.rotation, holdPoint);
        }

        if (_animator != null)
        {
            float fireSpeed = Mathf.Clamp(0.25f / Mathf.Max(0.05f, _currentGunData.FireInterval), 0.5f, 4.0f);
            _animator.SetFloat("FireSpeed", fireSpeed);
            _animator.SetTrigger("Fire");
        }

        // 로컬 8가닥 탄 궤적(Tracer Line) 즉시 렌더링 (0ms 체감)
        Vector3[] dirs = GenerateConeDirections(direction, _currentGunData.SpreadAngle, _currentGunData.PelletCount, seed);
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 targetPoint = origin + dirs[i] * Mathf.Min(_currentGunData.MaxRange, 15f);
            if (Physics.Raycast(origin, dirs[i], out RaycastHit hit, _currentGunData.MaxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }
            SpawnTracer(origin, targetPoint);
        }
    }

    private void ExecuteLocalFirePrediction(bool isCharged)
    {
        if (_currentGunData == null) return;

        // 격발음 재생
        if (_currentGunData.FireSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentGunData.FireSound, _mainCamera.transform.position);
        }

        // 화면 반동 적용 (부드러운 복구)
        if (_playerController != null)
        {
            float pitch = _currentGunData.RecoilPitch * (isCharged ? 1.4f : 1.0f);
            float yaw = _currentGunData.RecoilYaw * (isCharged ? 1.4f : 1.0f);
            _playerController.ApplyRecoil(pitch, yaw, _currentGunData.RecoilRecoverySpeed);
        }

        if (_animator != null)
        {
            float fireSpeed = Mathf.Clamp(0.25f / Mathf.Max(0.05f, _currentGunData.FireInterval), 0.5f, 4.0f);
            _animator.SetFloat("FireSpeed", fireSpeed);
            _animator.SetTrigger("Fire");
        }

        // 총구 화염(Muzzle Flash) 로컬 생성
        Transform holdPoint = _playerInteraction != null ? _playerInteraction.HoldPoint : transform;
        if (_currentGunData.MuzzleFlashPrefab != null && holdPoint != null)
        {
            Instantiate(_currentGunData.MuzzleFlashPrefab, holdPoint.position + holdPoint.forward * 0.4f, holdPoint.rotation, holdPoint);
        }
    }

    private void PlayDryFireSound()
    {
        if (_currentGunData != null && _currentGunData.DryFireSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentGunData.DryFireSound, _mainCamera.transform.position);
        }
    }

    #endregion

    /// <summary>
    /// PlayerItemHolder의 손 아이템 갱신(로컬 픽업, 툴바 변경, 네트워크 동기화 등)에 의해 호출되어 현재 장착 총기 데이터를 동기화합니다.
    /// </summary>
    public void SetEquippedGun(GunItemData gunData)
    {
        _currentGunData = gunData;
        if (gunData == null)
        {
            _currentGunSlot = null;
        }

        if (_animator != null)
        {
            _animator.SetInteger("WeaponType", gunData != null ? 1 : 0);
        }
    }

    /// <summary>
    /// 하위 호환성 유지용 메서드입니다.
    /// </summary>
    public void SetEquippedGunFromNetwork(GunItemData gunData)
    {
        SetEquippedGun(gunData);
    }

    #region Server-Authoritative Hitscan & Damage

    [ServerRpc]
    private void RequestFireServerRpc(Vector3 origin, Vector3 direction, bool isCharged, ulong instigatorClientId)
    {
        // 1. 서버 측 _currentGunData 누락 시 PlayerItemHolder를 통한 복구 시도
        if (_currentGunData == null && _playerItemHolder != null)
        {
            if (_playerItemHolder.CurrentHeldItemData is GunItemData heldGun)
            {
                _currentGunData = heldGun;
            }
            else
            {
                string heldId = _playerItemHolder.NetworkHeldItemId;
                if (!string.IsNullOrEmpty(heldId))
                {
                    _currentGunData = ItemDatabase.GetItem(heldId) as GunItemData;
                }
            }
        }

        if (_currentGunData == null)
        {
            Debug.LogWarning($"[Server] RequestFireServerRpc 무시: 발사자(ClientId={instigatorClientId})의 장착 총기 데이터를 찾을 수 없습니다.");
            return;
        }

        float maxRange = _currentGunData.MaxRange;
        int damageToApply = _currentGunData.BaseDamage;
        if (isCharged)
        {
            damageToApply = Mathf.RoundToInt(damageToApply * _currentGunData.ChargedDamageMultiplier);
        }

        // 서버 월드 위치 기준으로 단일 레이캐스트 실행 (E-08: 카메라 원점 시작)
        Ray ray = new Ray(origin, direction);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Ignore);

        Vector3 hitPoint = hitSomething ? hit.point : (origin + direction * maxRange);
        Vector3 hitNormal = hitSomething ? hit.normal : -direction;

        SurfaceType surfaceType = SurfaceType.Default;

        if (hitSomething)
        {
            surfaceType = DetermineSurfaceType(hit.collider);

            // 1. 발사자 본인 또는 아군 플레이어인지 검사 (Friendly Fire 방지 - E-09)
            PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
            if (hitPlayer != null)
            {
                // 아군 플레이어 피격 시 데미지는 무시하고 먼지/피격 이펙트만 스폰
                NotifyFireHitClientRpc(origin, hitPoint, hitNormal, true, (byte)surfaceType);
                return;
            }

            // 2. Hitbox(헤드샷/부위별) 확인
            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox != null && hitbox.Damageable != null)
            {
                bool isHeadshot = hitbox.Type == HitboxType.Head;
                int finalDamage = isHeadshot ? Mathf.RoundToInt(damageToApply * 1.5f) : damageToApply;

                DamageInfo dmg = new DamageInfo(finalDamage, instigatorClientId, hitPoint, hitNormal, hitbox.Type, isCharged);
                hitbox.Damageable.TakeDamage(dmg);

                Debug.Log($"[Server] Hitbox 적중! 대상: {hitbox.Damageable.transform.name}, 부위: {hitbox.Type}, 데미지: {finalDamage} (헤드샷: {isHeadshot}, 차지: {isCharged})");
            }
            // 3. 일반 IDamageable 확인 (몬스터 또는 DestructibleObject)
            else
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    DamageInfo dmg = new DamageInfo(damageToApply, instigatorClientId, hitPoint, hitNormal, HitboxType.Body, isCharged);
                    damageable.TakeDamage(dmg);

                    Debug.Log($"[Server] IDamageable 적중! 대상: {damageable.transform.name}, 데미지: {damageToApply} (차지: {isCharged})");
                }
            }
        }

        // 모든 클라이언트에 피격 지점 먼지/파편 이펙트 브로드캐스트
        NotifyFireHitClientRpc(origin, hitPoint, hitNormal, hitSomething, (byte)surfaceType);
    }

    private SurfaceType DetermineSurfaceType(Collider hitCollider)
    {
        if (hitCollider == null) return SurfaceType.Default;

        // 몬스터 / 생체 타겟 (Hitbox 또는 IDamageable)
        if (hitCollider.GetComponent<Hitbox>() != null)
        {
            return SurfaceType.Flesh;
        }

        if (hitCollider.GetComponentInParent<NonPlayerCharacter>() != null)
        {
            return SurfaceType.Flesh;
        }

        // 환경 오브젝트에 부착된 SurfaceIdentifier 확인
        SurfaceIdentifier identifier = hitCollider.GetComponentInParent<SurfaceIdentifier>();
        if (identifier != null)
        {
            return identifier.SurfaceType;
        }

        return SurfaceType.Default;
    }

    [ClientRpc]
    private void NotifyFireHitClientRpc(Vector3 origin, Vector3 hitPoint, Vector3 hitNormal, bool hitSomething, byte surfaceTypeByte)
    {
        // 1. 원격 플레이어인 경우 총구 격발음 및 총구 화염(Muzzle Flash) 재생
        if (!IsOwner && _currentGunData != null)
        {
            if (_currentGunData.FireSound != null)
            {
                AudioSource.PlayClipAtPoint(_currentGunData.FireSound, origin);
            }

            if (_currentGunData.MuzzleFlashPrefab != null)
            {
                Transform holdPoint = _playerInteraction != null ? _playerInteraction.HoldPoint : transform;
                if (holdPoint != null)
                {
                    Instantiate(_currentGunData.MuzzleFlashPrefab, holdPoint.position + holdPoint.forward * 0.4f, holdPoint.rotation, holdPoint);
                }
            }
        }

        // 2. 피격 지점에 재질별 파티클 스폰 (데미지 유무 무관하게 표면 히트 시 항상 출력)
        if (hitSomething)
        {
            SurfaceType surfaceType = (SurfaceType)surfaceTypeByte;
            SpawnImpactEffect(hitPoint, hitNormal, surfaceType);
        }

        // 3. 탄 궤적(Tracer) 렌더링
        SpawnTracer(origin, hitPoint);
    }

    private class ShotgunAccumulatedDamage
    {
        public int HeadDamage;
        public int BodyDamage;
        public Vector3 LastHitPoint;
        public Vector3 LastHitNormal;
    }

    /// <summary>
    /// 결정론적 난수 시드(Seed)를 기반으로 전방 벡터(forward)를 중심으로 spreadAngle 반경 내에 균등 분산된 count개의 방향 벡터를 생성합니다.
    /// 클라이언트와 서버가 동일한 시드를 사용하면 100% 동일한 방향 벡터 배열을 얻습니다.
    /// </summary>
    public static Vector3[] GenerateConeDirections(Vector3 forward, float spreadAngle, int count, int seed)
    {
        Vector3[] directions = new Vector3[count];
        System.Random rng = new System.Random(seed);

        Quaternion forwardRotation = Quaternion.LookRotation(forward);

        for (int i = 0; i < count; i++)
        {
            // 0 ~ spreadAngle 사이의 무작위 각도 (원형 균등 분포를 위해 제곱근 적용)
            double r = rng.NextDouble();
            float theta = Mathf.Sqrt((float)r) * spreadAngle;
            // 0 ~ 360도 무작위 롤 각도
            float phi = (float)(rng.NextDouble() * 360.0);

            Quaternion deviation = Quaternion.AngleAxis(phi, Vector3.forward) * Quaternion.AngleAxis(theta, Vector3.up);
            Vector3 localDir = deviation * Vector3.forward;

            directions[i] = (forwardRotation * localDir).normalized;
        }

        return directions;
    }

    [ServerRpc]
    private void RequestShotgunFireServerRpc(Vector3 origin, Vector3 forwardDirection, int seed, bool isCharged, ulong instigatorClientId)
    {
        if (_currentGunData == null)
        {
            CheckCurrentHeldGun();
        }

        if (_currentGunData == null)
        {
            Debug.LogWarning($"[Server] RequestShotgunFireServerRpc 무시: 발사자(ClientId={instigatorClientId})의 장착 총기 데이터를 찾을 수 없습니다.");
            return;
        }

        int pelletCount = _currentGunData.PelletCount;
        float spreadAngle = _currentGunData.SpreadAngle;
        float maxRange = _currentGunData.MaxRange;
        int baseTotalDamage = _currentGunData.BaseDamage;
        if (isCharged)
        {
            baseTotalDamage = Mathf.RoundToInt(baseTotalDamage * _currentGunData.ChargedDamageMultiplier);
        }

        // 펠릿 1발당 기본 데미지 (내림 계산)
        int basePelletDamage = Mathf.FloorToInt((float)baseTotalDamage / pelletCount);

        Vector3[] directions = GenerateConeDirections(forwardDirection, spreadAngle, pelletCount, seed);

        Vector3[] hitPoints = new Vector3[pelletCount];
        Vector3[] hitNormals = new Vector3[pelletCount];
        bool[] hitSomethings = new bool[pelletCount];
        byte[] surfaceTypes = new byte[pelletCount];

        Dictionary<IDamageable, ShotgunAccumulatedDamage> targetDamageMap = new Dictionary<IDamageable, ShotgunAccumulatedDamage>();

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 dir = directions[i];
            Ray ray = new Ray(origin, dir);
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Ignore);

            hitSomethings[i] = hitSomething;
            hitPoints[i] = hitSomething ? hit.point : (origin + dir * maxRange);
            hitNormals[i] = hitSomething ? hit.normal : -dir;
            surfaceTypes[i] = (byte)SurfaceType.Default;

            if (hitSomething)
            {
                SurfaceType surfaceType = DetermineSurfaceType(hit.collider);
                surfaceTypes[i] = (byte)surfaceType;

                // 1. 발사자 본인 또는 아군 플레이어 검사 (Friendly Fire 방지 - E-12)
                PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
                if (hitPlayer != null)
                {
                    continue;
                }

                // 2. 거리별 데미지 감쇄 계산
                // 0 ~ 3m: 100% 풀 데미지
                // 3 ~ 10m: 거리 비례 선형 감쇄
                // 10 ~ 15m: 펠릿당 최소 1 데미지 보장
                float dist = hit.distance;
                int currentPelletDamage;
                if (dist <= _currentGunData.DamageFalloffStartRange)
                {
                    currentPelletDamage = basePelletDamage;
                }
                else if (dist >= _currentGunData.DamageFalloffEndRange)
                {
                    currentPelletDamage = _currentGunData.MinDamagePerPellet;
                }
                else
                {
                    float t = Mathf.InverseLerp(_currentGunData.DamageFalloffStartRange, _currentGunData.DamageFalloffEndRange, dist);
                    currentPelletDamage = Mathf.RoundToInt(Mathf.Lerp(basePelletDamage, _currentGunData.MinDamagePerPellet, t));
                }

                if (currentPelletDamage < _currentGunData.MinDamagePerPellet)
                {
                    currentPelletDamage = _currentGunData.MinDamagePerPellet;
                }

                // 3. Hitbox(헤드샷/부위별) 확인
                Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox.Damageable != null)
                {
                    IDamageable target = hitbox.Damageable;
                    if (!targetDamageMap.TryGetValue(target, out ShotgunAccumulatedDamage acc))
                    {
                        acc = new ShotgunAccumulatedDamage();
                        targetDamageMap[target] = acc;
                    }

                    acc.LastHitPoint = hit.point;
                    acc.LastHitNormal = hit.normal;

                    if (hitbox.Type == HitboxType.Head)
                    {
                        acc.HeadDamage += Mathf.RoundToInt(currentPelletDamage * 1.5f);
                    }
                    else
                    {
                        acc.BodyDamage += currentPelletDamage;
                    }
                }
                // 4. 일반 IDamageable 확인 (몬스터 또는 DestructibleObject)
                else
                {
                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        if (!targetDamageMap.TryGetValue(damageable, out ShotgunAccumulatedDamage acc))
                        {
                            acc = new ShotgunAccumulatedDamage();
                            targetDamageMap[damageable] = acc;
                        }

                        acc.LastHitPoint = hit.point;
                        acc.LastHitNormal = hit.normal;
                        acc.BodyDamage += currentPelletDamage;
                    }
                }
            }
        }

        // 대상별로 [헤드 합산 1회] + [몸통 합산 1회] 데미지 적용 (E-15)
        foreach (var kvp in targetDamageMap)
        {
            IDamageable target = kvp.Key;
            ShotgunAccumulatedDamage acc = kvp.Value;

            if (acc.HeadDamage > 0)
            {
                DamageInfo headDmg = new DamageInfo(acc.HeadDamage, instigatorClientId, acc.LastHitPoint, acc.LastHitNormal, HitboxType.Head, isCharged);
                target.TakeDamage(headDmg);
                Debug.Log($"[Server] 샷건 헤드 합산 적중! 대상: {((Component)target).name}, 데미지: {acc.HeadDamage} (차지: {isCharged})");
            }

            if (acc.BodyDamage > 0)
            {
                DamageInfo bodyDmg = new DamageInfo(acc.BodyDamage, instigatorClientId, acc.LastHitPoint, acc.LastHitNormal, HitboxType.Body, isCharged);
                target.TakeDamage(bodyDmg);
                Debug.Log($"[Server] 샷건 몸통 합산 적중! 대상: {((Component)target).name}, 데미지: {acc.BodyDamage} (차지: {isCharged})");
            }
        }

        // 모든 클라이언트에 8개 피격 지점 파티클 및 원격 플레이어 트레이서 브로드캐스트
        NotifyShotgunHitClientRpc(origin, hitPoints, hitNormals, hitSomethings, surfaceTypes);
    }

    [ClientRpc]
    private void NotifyShotgunHitClientRpc(Vector3 origin, Vector3[] hitPoints, Vector3[] hitNormals, bool[] hitSomethings, byte[] surfaceTypes)
    {
        // 1. 원격 플레이어인 경우 총구 격발음, Muzzle Flash 및 8발 Tracer 재생
        if (!IsOwner && _currentGunData != null)
        {
            if (_currentGunData.FireSound != null)
            {
                AudioSource.PlayClipAtPoint(_currentGunData.FireSound, origin);
            }

            Transform holdPoint = _playerInteraction != null ? _playerInteraction.HoldPoint : transform;
            if (_currentGunData.MuzzleFlashPrefab != null && holdPoint != null)
            {
                Instantiate(_currentGunData.MuzzleFlashPrefab, holdPoint.position + holdPoint.forward * 0.4f, holdPoint.rotation, holdPoint);
            }

            if (hitPoints != null)
            {
                for (int i = 0; i < hitPoints.Length; i++)
                {
                    SpawnTracer(origin, hitPoints[i]);
                }
            }
        }

        // 2. 피격 지점에 재질별 파티클 스폰 (모든 클라이언트)
        if (hitPoints != null && hitSomethings != null && surfaceTypes != null)
        {
            for (int i = 0; i < hitPoints.Length; i++)
            {
                if (hitSomethings[i])
                {
                    SurfaceType surfaceType = (SurfaceType)surfaceTypes[i];
                    SpawnImpactEffect(hitPoints[i], hitNormals[i], surfaceType);
                }
            }
        }
    }

    private void SpawnImpactEffect(Vector3 point, Vector3 normal, SurfaceType surfaceType)
    {
        GameObject overridePrefab = _currentGunData != null && _currentGunData.ImpactEffectPrefab != null
            ? _currentGunData.ImpactEffectPrefab
            : _defaultImpactEffectPrefab;

        VfxPoolManager.Instance.SpawnImpact(surfaceType, point, normal, overridePrefab);
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        if (_currentGunData != null && _currentGunData.BulletTracerPrefab != null)
        {
            GameObject tracer = Instantiate(_currentGunData.BulletTracerPrefab, from, Quaternion.identity);
            LineRenderer lr = tracer.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
            }
            Destroy(tracer, 0.15f);
        }
    }

    #endregion

    #region Reloading System

    public void TryStartReload()
    {
        if (_currentGunData == null || _currentGunSlot == null || IsReloading) return;

        // 이미 탄창이 가득 차 있으면 재장전 불필요
        if (_currentGunSlot.CurrentAmmo >= _currentGunData.MagazineCapacity)
        {
            return;
        }

        // 인벤토리에 탄약이 0개이면 장전 불가 (E-07, E-10)
        int reserveAmmo = TotalReserveAmmo;
        if (reserveAmmo <= 0)
        {
            Debug.Log("[PlayerGunCombat] 재장전 불가: 인벤토리에 탄약이 없습니다.");
            return;
        }

        // 차징 중이었다면 취소
        CancelCharge();

        if (_animator != null)
        {
            _animator.SetTrigger("Reload");
        }

        // 달리는 도중 재장전 시도 시 달리기 즉시 해제 (E-18)
        if (_playerController != null && _playerController.IsSprinting)
        {
            _playerController.CancelSprint(0.15f);
        }

        // 쉘 바이 쉘 장전 무기인 경우 1발씩 장전 루틴 시작
        if (_currentGunData.UseShellByShellReload)
        {
            StartShellReload();
            return;
        }

        // 일반 탄창 교체식 재장전
        _isReloading = true;
        _reloadTimer = 0f;

        if (_animator != null)
        {
            float reloadSpeed = Mathf.Clamp(2.0f / Mathf.Max(0.5f, _currentGunData.ReloadDuration), 0.5f, 3.0f);
            _animator.SetFloat("ReloadSpeed", reloadSpeed);
            _animator.SetTrigger("Reload");
        }

        if (_currentGunData.ReloadSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentGunData.ReloadSound, _mainCamera.transform.position);
            if (IsSpawned)
            {
                RequestReloadSoundServerRpc(transform.position);
            }
        }

        OnReloadStarted?.Invoke();
        Debug.Log($"[PlayerGunCombat] 재장전 시작... ({_currentGunData.ReloadDuration}초 소요)");
    }

    [ServerRpc]
    private void RequestReloadSoundServerRpc(Vector3 soundPosition)
    {
        NotifyReloadSoundClientRpc(soundPosition);
    }

    [ClientRpc]
    private void NotifyReloadSoundClientRpc(Vector3 soundPosition)
    {
        if (!IsOwner && _currentGunData != null && _currentGunData.ReloadSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentGunData.ReloadSound, soundPosition);
        }
    }

    private void CompleteReload()
    {
        if (!_isReloading || _currentGunData == null || _currentGunSlot == null) return;

        int needed = _currentGunData.MagazineCapacity - _currentGunSlot.CurrentAmmo;
        if (needed > 0 && _playerInventory != null)
        {
            // 인벤토리에서 있는 만큼만 소모하여 충전
            int consumed = _playerInventory.ConsumeAmmo(_currentGunData.RequiredAmmoItemId, needed);
            _currentGunSlot.CurrentAmmo += consumed;
            Debug.Log($"[PlayerGunCombat] 재장전 완료! 충전량: {consumed}발, 현재 탄창: {_currentGunSlot.CurrentAmmo}/{_currentGunData.MagazineCapacity}");
        }

        _isReloading = false;
        _reloadTimer = 0f;
        UpdateCombatActionStateSync();

        OnReloadCompleted?.Invoke();
        OnAmmoChanged?.Invoke();
    }

    public void CancelReload()
    {
        if (_isReloading)
        {
            _isReloading = false;
            _reloadTimer = 0f;
            if (_animator != null)
            {
                _animator.ResetTrigger("Reload");
            }
            UpdateCombatActionStateSync();
            OnReloadCancelled?.Invoke();
            Debug.Log("[PlayerGunCombat] 재장전 취소됨 (무기 교체 등).");
        }
    }

    #endregion

    #region Shell-by-Shell Reload System (Shotgun)

    private void StartShellReload()
    {
        _shellReloadStage = ShellReloadStage.Starting;
        _shellReloadTimer = 0f;

        if (_animator != null)
        {
            float totalShellTime = _currentGunData.ReloadStartDelay + _currentGunData.ReloadInsertInterval + _currentGunData.ReloadEndDelay;
            float reloadSpeed = Mathf.Clamp(2.0f / Mathf.Max(0.5f, totalShellTime), 0.5f, 3.0f);
            _animator.SetFloat("ReloadSpeed", reloadSpeed);
            _animator.SetTrigger("Reload");
        }

        // 시작음 재생
        AudioClip startClip = _currentGunData.ReloadStartSound != null ? _currentGunData.ReloadStartSound : _currentGunData.ReloadSound;
        if (startClip != null)
        {
            AudioSource.PlayClipAtPoint(startClip, _mainCamera.transform.position);
        }

        if (IsSpawned)
        {
            RequestShellReloadSoundServerRpc(transform.position, 0);
        }

        OnReloadStarted?.Invoke();
        Debug.Log($"[PlayerGunCombat] 쉘 바이 쉘 장전 시작 (선딜레이: {_currentGunData.ReloadStartDelay}초)");
    }

    private void UpdateShellReload()
    {
        if (_currentGunData == null || _currentGunSlot == null)
        {
            CancelShellReload();
            return;
        }

        _shellReloadTimer += Time.deltaTime;

        switch (_shellReloadStage)
        {
            case ShellReloadStage.Starting:
                if (_shellReloadTimer >= _currentGunData.ReloadStartDelay)
                {
                    _shellReloadStage = ShellReloadStage.Inserting;
                    _shellReloadTimer = 0f;
                }
                break;

            case ShellReloadStage.Inserting:
                if (_shellReloadTimer >= _currentGunData.ReloadInsertInterval)
                {
                    _shellReloadTimer = 0f;

                    // 인벤토리 잔여 탄약 검사 (E-07, E-08)
                    int reserve = TotalReserveAmmo;
                    if (reserve <= 0)
                    {
                        // 더 이상 탄약이 없으면 장전 종료 단계로 즉시 전환
                        _shellReloadStage = ShellReloadStage.Ending;
                        return;
                    }

                    // 1발 소모 및 장전
                    int consumed = _playerInventory != null ? _playerInventory.ConsumeAmmo(_currentGunData.RequiredAmmoItemId, 1) : 0;
                    if (consumed > 0)
                    {
                        _currentGunSlot.CurrentAmmo += consumed;
                        OnAmmoChanged?.Invoke();

                        // 1발 삽입 사운드 재생
                        if (_currentGunData.ReloadInsertSound != null)
                        {
                            AudioSource.PlayClipAtPoint(_currentGunData.ReloadInsertSound, _mainCamera.transform.position);
                        }
                        if (IsSpawned)
                        {
                            RequestShellReloadSoundServerRpc(transform.position, 1);
                        }

                        Debug.Log($"[PlayerGunCombat] 쉘 1발 장전 완료! ({_currentGunSlot.CurrentAmmo}/{_currentGunData.MagazineCapacity})");
                    }

                    // 탄창이 가득 찼거나 인벤토리 탄약이 바닥났다면 종료 단계로 전환
                    if (_currentGunSlot.CurrentAmmo >= _currentGunData.MagazineCapacity || TotalReserveAmmo <= 0)
                    {
                        _shellReloadStage = ShellReloadStage.Ending;
                    }
                }
                break;

            case ShellReloadStage.Ending:
                if (_shellReloadTimer >= _currentGunData.ReloadEndDelay)
                {
                    CompleteShellReload();
                }
                break;
        }
    }

    public void CancelShellReload()
    {
        if (_shellReloadStage != ShellReloadStage.None)
        {
            _shellReloadStage = ShellReloadStage.None;
            _shellReloadTimer = 0f;
            if (_animator != null)
            {
                _animator.ResetTrigger("Reload");
            }
            UpdateCombatActionStateSync();
            OnReloadCancelled?.Invoke();
            Debug.Log("[PlayerGunCombat] 쉘 바이 쉘 장전 취소됨 (현재 장전된 탄약 보존).");
        }
    }

    private void CompleteShellReload()
    {
        _shellReloadStage = ShellReloadStage.None;
        _shellReloadTimer = 0f;
        UpdateCombatActionStateSync();

        // 장전 종료 사운드 재생
        if (_currentGunData != null)
        {
            if (_currentGunData.ReloadEndSound != null)
            {
                AudioSource.PlayClipAtPoint(_currentGunData.ReloadEndSound, _mainCamera.transform.position);
            }
            if (IsSpawned)
            {
                RequestShellReloadSoundServerRpc(transform.position, 2);
            }
        }

        OnReloadCompleted?.Invoke();
        OnAmmoChanged?.Invoke();
        Debug.Log("[PlayerGunCombat] 쉘 바이 쉘 장전 전체 완료!");
    }

    [ServerRpc]
    private void RequestShellReloadSoundServerRpc(Vector3 soundPosition, byte soundType)
    {
        NotifyShellReloadSoundClientRpc(soundPosition, soundType);
    }

    [ClientRpc]
    private void NotifyShellReloadSoundClientRpc(Vector3 soundPosition, byte soundType)
    {
        if (IsOwner || _currentGunData == null) return;

        AudioClip clipToPlay = null;
        switch (soundType)
        {
            case 0: // Start
                clipToPlay = _currentGunData.ReloadStartSound != null ? _currentGunData.ReloadStartSound : _currentGunData.ReloadSound;
                break;
            case 1: // Insert
                clipToPlay = _currentGunData.ReloadInsertSound;
                break;
            case 2: // End
                clipToPlay = _currentGunData.ReloadEndSound;
                break;
        }

        if (clipToPlay != null)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, soundPosition);
        }
    }

    #endregion

    #region Inventory & Weapon Change Handling

    private void HandleToolbarSlotChanged(int newIndex)
    {
        CancelCharge();
        CancelReload();
        CancelShellReload();
        CheckCurrentHeldGun();
    }

    private void HandleInventoryChanged()
    {
        CheckCurrentHeldGun();
        OnAmmoChanged?.Invoke();
    }

    private void CheckCurrentHeldGun()
    {
        // 1. PlayerItemHolder가 손에 들고 있는 아이템 우선 확인 (바닥에서 주운 Pending 아이템 포함)
        if (_playerItemHolder != null && _playerItemHolder.CurrentHeldItemData is GunItemData holderGun)
        {
            _currentGunData = holderGun;
            _currentGunSlot = _playerInventory != null ? _playerInventory.GetSelectedToolbarSlot() : null;
            OnAmmoChanged?.Invoke();
            return;
        }

        if (_playerInventory == null) return;

        InventorySlot selectedSlot = _playerInventory.GetSelectedToolbarSlot();
        if (selectedSlot != null && !selectedSlot.IsEmpty && selectedSlot.Item is GunItemData gunData)
        {
            _currentGunData = gunData;
            _currentGunSlot = selectedSlot;

            // 슬롯에 잔탄이 미초기화(-1)된 경우 탄창 완충으로 초기화
            if (_currentGunSlot.CurrentAmmo < 0)
            {
                _currentGunSlot.CurrentAmmo = gunData.MagazineCapacity;
            }
        }
        else
        {
            CancelCharge();
            CancelReload();
            CancelShellReload();
            _currentGunData = null;
            _currentGunSlot = null;
        }

        OnAmmoChanged?.Invoke();
    }

    #endregion

    #region Debug Testing Helpers

    private void CheckDebugInput()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            GiveDebugGunsAndAmmo();
        }
    }

    private void CheckAutoEquipGuns()
    {
        if (_playerInventory == null) return;

        // 인벤토리에 이미 총기가 있는지 검사
        bool hasGun = false;
        foreach (var slot in _playerInventory.ToolbarSlots)
        {
            if (!slot.IsEmpty && slot.Item is GunItemData)
            {
                hasGun = true;
                break;
            }
        }

        if (!hasGun)
        {
            GiveDebugGunsAndAmmo();
        }
    }

    /// <summary>
    /// F1 키 입력 시 또는 시작 시 테스트용 총기 3종 및 탄약을 인벤토리에 지급합니다.
    /// </summary>
    [ContextMenu("Give Debug Guns and Ammo")]
    public void GiveDebugGunsAndAmmo()
    {
        if (_playerInventory == null) _playerInventory = GetComponent<PlayerInventory>();
        if (_playerInventory == null) return;

        ItemData rifle = Resources.Load<ItemData>("ItemData/Gun_AssaultRifle");
        ItemData pistol = Resources.Load<ItemData>("ItemData/Gun_TacticalPistol");
        ItemData laser = Resources.Load<ItemData>("ItemData/Gun_ChargeLaser");
        ItemData shotgun = Resources.Load<ItemData>("ItemData/Gun_PumpShotgun");
        ItemData ammoRifle = Resources.Load<ItemData>("ItemData/Ammo_Rifle");
        ItemData ammoPistol = Resources.Load<ItemData>("ItemData/Ammo_Pistol");
        ItemData ammoEnergy = Resources.Load<ItemData>("ItemData/Ammo_Energy");
        ItemData ammoShotgun = Resources.Load<ItemData>("ItemData/Ammo_Shotgun");

        if (rifle != null) _playerInventory.AddItem(rifle, 1);
        if (pistol != null) _playerInventory.AddItem(pistol, 1);
        if (laser != null) _playerInventory.AddItem(laser, 1);
        if (shotgun != null) _playerInventory.AddItem(shotgun, 1);
        if (ammoRifle != null) _playerInventory.AddItem(ammoRifle, 120);
        if (ammoPistol != null) _playerInventory.AddItem(ammoPistol, 60);
        if (ammoEnergy != null) _playerInventory.AddItem(ammoEnergy, 50);
        if (ammoShotgun != null) _playerInventory.AddItem(ammoShotgun, 24);

        CheckCurrentHeldGun();
        Debug.Log("<color=green>[PlayerGunCombat] 테스트 총기 4종(돌격소총, 권총, 차지레이저, 펌프샷건) 및 탄약이 인벤토리에 지급되었습니다! (단축키: F1)</color>");
    }

    #endregion
}
