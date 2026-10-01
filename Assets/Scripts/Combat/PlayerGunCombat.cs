using System;
using System.Collections.Generic;
using Coop.VFX;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ?�건 ??바이 ??1발씩) ?�전 ?�계
/// </summary>
public enum ShellReloadStage
{
    None = 0,
    Starting = 1,
    Inserting = 2,
    Ending = 3
}

/// <summary>
/// ?�트?�크 ?�기?�용 총기 ?�동 ?�태 (?��? ?�장?? 차징)
/// </summary>
public enum GunCombatActionState : byte
{
    Idle = 0,
    Reloading = 1,
    Charging = 2
}

/// <summary>
/// ?�레?�어??총기 ?�격(?�발, ?�사, 차�??? ?�건 ?�탄), ?�장???�창?? ??바이 ??, 반동, ?�버 권한 ?�트?�캔 ?�정 �??�기?��? 총괄?�는 컴포?�트?�니??
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

    [Header("Debug Settings (F1 ?�로 ?�제??지�?가??")]
    [Tooltip("체크 ??게임 ?�작 ???�벤?�리??총기?� ?�약???�다�??�동?�로 ?�스??총기 �??�약??지급합?�다.")]
    [SerializeField] private bool _autoEquipGunsOnStart = true;

    private Camera _mainCamera;
    private Animator _animator;
    private GunItemData _currentGunData;
    public GunItemData CurrentGunData => _currentGunData;
    private InventorySlot _currentGunSlot;

    // ?�격 �?쿨�????�태
    private float _fireCooldownTimer;
    private bool _isFiringInputHeld;

    // 차�????�태
    private bool _isCharging;
    private float _currentChargeTime;
    private bool _hasPlayedChargeReadySound;

    // ?�반 ?�창 ?�장???�태
    private bool _isReloading;
    private float _reloadTimer;

    // ??바이 ??1발씩) ?�전 ?�태 (?�건 ?�용)
    private ShellReloadStage _shellReloadStage = ShellReloadStage.None;
    private float _shellReloadTimer;

    // ?�리�?Sprint) ?�호?�용 �??�딜?�이 ?�태
    private float _sprintToFireTimer;
    private bool _requireMouseReleaseToFire;

    // HUD �??��? ?�동???�로?�티 �??�벤??    public GunItemData CurrentGunData => _currentGunData;
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

        // 마우??좌클�?�� ?�다�??�클�??�구 ?�제
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _requireMouseReleaseToFire = false;
        }

        // ?�반 ?�창 ?�장???�?�머
        if (_isReloading)
        {
            _reloadTimer += Time.deltaTime;
            if (_reloadTimer >= _currentGunData.ReloadDuration)
            {
                CompleteReload();
            }
        }

        // ??바이 ??1발씩) ?�전 ?�?�머 �??�계 ?�환
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

        // 1. ?�장???�중 ?�리�?Shift) ?�도 ???�장??즉시 취소 (E-06, E-14)
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

        // 2. ??바이 ???�전 �?좌클�??�력 처리 (E-04, E-05)
        if (_shellReloadStage != ShellReloadStage.None)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckInterruptSprintOnAttack();
                if (CurrentAmmoInClip <= 0)
                {
                    // 0�?�?�??�태?�서 1�??�입 ???�릭: ?�전 즉시 캔슬 + 빈총 ?�리 (E-04)
                    CancelShellReload();
                    PlayDryFireSound();
                    return;
                }
                else
                {
                    // 1�??�상 ?�어�??�태?�서 ?�전 ?�중 ?�릭: ?�레??0초로 즉시 ?�전???�고 바로 ?? 격발 (E-05)
                    CancelShellReload();
                    TryFire(false);
                    return;
                }
            }

            // ?�격 ?�력???�다�???바이 ???�전 계속 진행
            return;
        }

        // 3. ?�반 ?�창 ?�장??진행 중에??발사 ?�력 무시?�고 ?�장??계속 진행 (E-03)
        if (_isReloading)
        {
            return;
        }

        // 4. ?�동 ?�장???�력 (R ??
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            TryStartReload();
            return;
        }

        // 5. 차징 ?�중 마우???�클�?취소 (E-05)
        if (_isCharging && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelCharge();
            return;
        }

        // 6. 발사 모드�??�력 처리
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

            // ?�약??1발이?�도 ?�어??차징 ?�작
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
            Debug.Log("[PlayerGunCombat] 차징 취소??");
        }
    }

    #endregion

    #region Firing & Prediction

    private void TryFire(bool isCharged)
    {
        if (_currentGunData == null || _currentGunSlot == null) return;

        // ?�리�?�??�격 캔슬 ??마우???�클�??�구 (?�황 A - E-12)
        if (_requireMouseReleaseToFire) return;

        // ?�리�???�?�??�딜?�이 진행 �?(E-13)
        if (_sprintToFireTimer > 0f) return;

        // ?�격 쿨�???검??        if (_fireCooldownTimer > 0f) return;

        // ?�탄??검??(E-01)
        if (_currentGunSlot.CurrentAmmo <= 0)
        {
            PlayDryFireSound();
            _fireCooldownTimer = 0.2f;
            return;
        }

        // 1. ?�약 1�??�모 (?�라?�언??즉시 반영 ?�측)
        _currentGunSlot.CurrentAmmo--;
        _fireCooldownTimer = _currentGunData.FireInterval;
        OnAmmoChanged?.Invoke();

        // 2. ?�격 ???�리�??�제 �??�동 ?�도 감속 ?�널???�용 (75% ?�도)
        if (_playerController != null)
        {
            float penaltyDuration = _currentGunData.FireInterval + 0.08f;
            _playerController.CancelSprint(penaltyDuration);
            _playerController.ApplyShootingPenalty(_currentGunData.ShootingMovementMultiplier, penaltyDuration);
        }

        // 3. 로컬 비주??�??�디??즉각 ?�생 (0ms 체감) �??�버 RPC ?�송
        Ray aimRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (_currentGunData.IsShotgun)
        {
            // ?�건: ?�수 ?�드 ?�성 �?로컬 즉시 ?�각/�?�� ?�드�?(8�?Tracer ?�함)
            int randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            ExecuteLocalShotgunPrediction(aimRay.origin, aimRay.direction, randomSeed);
            RequestShotgunFireServerRpc(aimRay.origin, aimRay.direction, randomSeed, isCharged, NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            // ?�반 총기 로컬 ?�측 �??�버 ?�일 ?�이캐스???�청
            ExecuteLocalFirePrediction(isCharged, aimRay);
            RequestFireServerRpc(aimRay.origin, aimRay.direction, isCharged, NetworkManager.Singleton.LocalClientId);
        }
    }

    private void PlayFireVisuals()
    {
        Transform muzzlePoint = null;
        if (_playerItemHolder != null && _playerItemHolder.CurrentHeldInstance != null)
        {
            var visual = _playerItemHolder.CurrentHeldInstance.GetComponent<HeldItemVisual>();
            if (visual != null)
            {
                muzzlePoint = visual.MuzzlePoint;
                // ?�약???�아?�는 경우(�? ?�격 ?�공 ?? 무기 ?�체 ?�니메이???�생
                if (visual.WeaponAnimator != null)
                {
                    visual.WeaponAnimator.SetTrigger("Fire");
                }
            }
        }

        if (_currentGunData.MuzzleFlashPrefab != null)
        {
            Transform holdPoint = _playerInteraction != null ? _playerInteraction.HoldPoint : transform;
            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : holdPoint.position + holdPoint.forward * 0.4f;
            Quaternion spawnRot = muzzlePoint != null ? muzzlePoint.rotation : holdPoint.rotation;
            Transform parentTransform = muzzlePoint != null ? muzzlePoint : holdPoint;

            Instantiate(_currentGunData.MuzzleFlashPrefab, spawnPos, spawnRot, parentTransform);
        }
    }

    private void ExecuteLocalShotgunPrediction(Vector3 origin, Vector3 direction, int seed)
    {
        if (_currentGunData == null) return;

        // 격발???�생
        if (_currentGunData.FireSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentGunData.FireSound, _mainCamera.transform.position);
        }

        // 반동 ?�용
        if (_playerController != null)
        {
            _playerController.ApplyRecoil(_currentGunData.RecoilPitch, _currentGunData.RecoilYaw, _currentGunData.RecoilRecoverySpeed, _currentGunData.RecoilSnappiness);
        }

        if (_animator != null)
        {
            float fireSpeed = Mathf.Clamp(0.25f / Mathf.Max(0.05f, _currentGunData.FireInterval), 0.5f, 4.0f);
            _animator.SetFloat("FireSpeed", fireSpeed);
            _animator.SetTrigger("Fire");
        }
        
        PlayFireVisuals();

        // 로컬 8가????궤적(Tracer Line) �?총탄 구멍 ?�칼 즉시 ?�더�?(0ms 체감)
        Vector3[] dirs = GenerateConeDirections(direction, _currentGunData.SpreadAngle, _currentGunData.PelletCount, seed);
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 targetPoint = origin + dirs[i] * Mathf.Min(_currentGunData.MaxRange, 15f);
            if (Physics.Raycast(origin, dirs[i], out RaycastHit hit, _currentGunData.MaxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
                SurfaceType surfaceType = DetermineSurfaceType(hit.collider);
                if (surfaceType != SurfaceType.Flesh)
                {
                    DecalPoolManager.Instance.SpawnBulletHole(hit.point, hit.normal, surfaceType, hit.collider.transform);
                }
                
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.GetComponent<PickableItem>() == null)
                {
                    float impulseForce = _currentGunData.BaseDamage * 0.15f;
                    hit.rigidbody.AddForceAtPosition(dirs[i] * impulseForce, hit.point, ForceMode.Impulse);
                }
            }
            SpawnTracer(origin, targetPoint);
        }
    }

    private void ExecuteLocalFirePrediction(bool isCharged, Ray aimRay)
    {
        if (_currentGunData == null) return;

        // 격발???�생
        if (_currentGunData.FireSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentGunData.FireSound, _mainCamera.transform.position);
        }

        // ?�면 반동 ?�용 (부?�러??복구)
        if (_playerController != null)
        {
            float pitch = _currentGunData.RecoilPitch * (isCharged ? 1.4f : 1.0f);
            float yaw = _currentGunData.RecoilYaw * (isCharged ? 1.4f : 1.0f);
            _playerController.ApplyRecoil(pitch, yaw, _currentGunData.RecoilRecoverySpeed, _currentGunData.RecoilSnappiness);
        }

        if (_animator != null)
        {
            float fireSpeed = Mathf.Clamp(0.25f / Mathf.Max(0.05f, _currentGunData.FireInterval), 0.5f, 4.0f);
            _animator.SetFloat("FireSpeed", fireSpeed);
            _animator.SetTrigger("Fire");
        }

        PlayFireVisuals();

        // ?�반 총기 로컬 ?�흔 ?�칼 즉시 ?�성 (0ms 반응??
        if (Physics.Raycast(aimRay, out RaycastHit hit, _currentGunData.MaxRange, ~0, QueryTriggerInteraction.Ignore))
        {
            SurfaceType surfaceType = DetermineSurfaceType(hit.collider);
            if (surfaceType != SurfaceType.Flesh)
            {
                DecalPoolManager.Instance.SpawnBulletHole(hit.point, hit.normal, surfaceType, hit.collider.transform);
            }
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.GetComponent<PickableItem>() == null)
            {
                float impulseForce = _currentGunData.BaseDamage * (isCharged ? _currentGunData.ChargedDamageMultiplier : 1f) * 0.15f;
                hit.rigidbody.AddForceAtPosition(aimRay.direction * impulseForce, hit.point, ForceMode.Impulse);
            }
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
    /// PlayerItemHolder?????�이??갱신(로컬 ?�업, ?�바 변�? ?�트?�크 ?�기???????�해 ?�출?�어 ?�재 ?�착 총기 ?�이?��? ?�기?�합?�다.
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
    /// ?�위 ?�환???��???메서?�입?�다.
    /// </summary>
    public void SetEquippedGunFromNetwork(GunItemData gunData)
    {
        SetEquippedGun(gunData);
    }

    #region Server-Authoritative Hitscan & Damage

    [ServerRpc]
    private void RequestFireServerRpc(Vector3 origin, Vector3 direction, bool isCharged, ulong instigatorClientId)
    {
        // 1. ?�버 �?_currentGunData ?�락 ??PlayerItemHolder�??�한 복구 ?�도
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
            Debug.LogWarning($"[Server] RequestFireServerRpc 무시: 발사??ClientId={instigatorClientId})???�착 총기 ?�이?��? 찾을 ???�습?�다.");
            return;
        }

        float maxRange = _currentGunData.MaxRange;
        int damageToApply = _currentGunData.BaseDamage;
        if (isCharged)
        {
            damageToApply = Mathf.RoundToInt(damageToApply * _currentGunData.ChargedDamageMultiplier);
        }

        // ?�버 ?�드 ?�치 기�??�로 ?�일 ?�이캐스???�행 (E-08: 카메???�점 ?�작)
        Ray ray = new Ray(origin, direction);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Ignore);

        Vector3 hitPoint = hitSomething ? hit.point : (origin + direction * maxRange);
        Vector3 hitNormal = hitSomething ? hit.normal : -direction;

        SurfaceType surfaceType = SurfaceType.Default;

        if (hitSomething)
        {
            surfaceType = DetermineSurfaceType(hit.collider);

            // 1. 발사??본인 ?�는 ?�군 ?�레?�어?��? 검??(Friendly Fire 방�? - E-09)
            PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
            if (hitPlayer != null)
            {
                // ?�군 ?�레?�어 ?�격 ???��?지??무시?�고 먼�?/?�격 ?�펙?�만 ?�폰
                NotifyFireHitClientRpc(origin, hitPoint, hitNormal, true, (byte)surfaceType);
                return;
            }

            // 2. Hitbox(?�드??부?�별) ?�인
            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox != null && hitbox.Damageable != null)
            {
                bool isHeadshot = hitbox.Type == HitboxType.Head;
                int finalDamage = isHeadshot ? Mathf.RoundToInt(damageToApply * 1.5f) : damageToApply;

                DamageInfo dmg = new DamageInfo(finalDamage, instigatorClientId, hitPoint, hitNormal, hitbox.Type, isCharged, _currentGunData.KnockbackForce, direction);
                hitbox.Damageable.TakeDamage(dmg);

                Debug.Log($"[Server] Hitbox ?�중! ?�?? {hitbox.Damageable.transform.name}, 부?? {hitbox.Type}, ?��?지: {finalDamage} (?�드?? {isHeadshot}, 차�?: {isCharged})");
            }
            // 3. ?�반 IDamageable ?�인 (몬스???�는 DestructibleObject)
            else
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    DamageInfo dmg = new DamageInfo(damageToApply, instigatorClientId, hitPoint, hitNormal, HitboxType.Body, isCharged, _currentGunData.KnockbackForce, direction);
                    damageable.TakeDamage(dmg);

                    Debug.Log($"[Server] IDamageable ?�중! ?�?? {damageable.transform.name}, ?��?지: {damageToApply} (차�?: {isCharged})");
                }
            }

            // 4. 물리 객체(PickableItem / Rigidbody) ?�력 ?�달
            if (hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                float impulseForce = damageToApply * 0.15f;
                pickable.ApplyImpulse(direction * impulseForce, hitPoint);
            }
            
        }

        // 모든 ?�라?�언?�에 ?�격 지??먼�?/?�편 ?�펙??브로?�캐?�트
        NotifyFireHitClientRpc(origin, hitPoint, hitNormal, hitSomething, (byte)surfaceType);
    }

    private SurfaceType DetermineSurfaceType(Collider hitCollider)
    {
        if (hitCollider == null) return SurfaceType.Default;

        // 몬스??/ ?�체 ?��?(Hitbox ?�는 IDamageable)
        if (hitCollider.GetComponent<Hitbox>() != null)
        {
            return SurfaceType.Flesh;
        }

        if (hitCollider.GetComponentInParent<NonPlayerCharacter>() != null)
        {
            return SurfaceType.Flesh;
        }

        // ?�경 ?�브?�트??부착된 SurfaceIdentifier ?�인
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
        // 1. ?�격 ?�레?�어??경우 총구 격발??�?총구 ?�염(Muzzle Flash) ?�생
        if (!IsOwner && _currentGunData != null)
        {
            if (_currentGunData.FireSound != null)
            {
                AudioSource.PlayClipAtPoint(_currentGunData.FireSound, origin);
            }

            PlayFireVisuals();
        }

        // 2. ?�격 지?�에 ?�질�??�티???�폰 (?��?지 ?�무 무�??�게 ?�면 ?�트 ????�� 출력)
        if (hitSomething)
        {
            SurfaceType surfaceType = (SurfaceType)surfaceTypeByte;
            SpawnImpactEffect(hitPoint, hitNormal, surfaceType);

            // ?�격 ?�레?�어 ?�격??경우 ?�칼 ?�폰 (로컬 ?�레?�어??발사 ?�간 0ms ?�반???�료)
            if (!IsOwner && surfaceType != SurfaceType.Flesh)
            {
                Transform hitTarget = null;
                if (Physics.Raycast(hitPoint + hitNormal * 0.05f, -hitNormal, out RaycastHit rHit, 0.15f, ~0, QueryTriggerInteraction.Ignore))
                {
                    hitTarget = rHit.collider.transform;
                }
                DecalPoolManager.Instance.SpawnBulletHole(hitPoint, hitNormal, surfaceType, hitTarget);
            }
        }

        // 3. ??궤적(Tracer) ?�더�?        SpawnTracer(origin, hitPoint);
    }

    private class ShotgunAccumulatedDamage
    {
        public int HeadDamage;
        public int BodyDamage;
        public int HitCount;
        public Vector3 Direction;
        public Vector3 LastHitPoint;
        public Vector3 LastHitNormal;
    }

    /// <summary>
    /// 결정론적 ?�수 ?�드(Seed)�?기반?�로 ?�방 벡터(forward)�?중심?�로 spreadAngle 반경 ?�에 균등 분산??count개의 방향 벡터�??�성?�니??
    /// ?�라?�언?��? ?�버가 ?�일???�드�??�용?�면 100% ?�일??방향 벡터 배열???�습?�다.
    /// </summary>
    public static Vector3[] GenerateConeDirections(Vector3 forward, float spreadAngle, int count, int seed)
    {
        Vector3[] directions = new Vector3[count];
        System.Random rng = new System.Random(seed);

        Quaternion forwardRotation = Quaternion.LookRotation(forward);

        for (int i = 0; i < count; i++)
        {
            // 0 ~ spreadAngle ?�이??무작??각도 (?�형 균등 분포�??�해 ?�곱�??�용)
            double r = rng.NextDouble();
            float theta = Mathf.Sqrt((float)r) * spreadAngle;
            // 0 ~ 360??무작??�?각도
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
            Debug.LogWarning($"[Server] RequestShotgunFireServerRpc 무시: 발사??ClientId={instigatorClientId})???�착 총기 ?�이?��? 찾을 ???�습?�다.");
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

        // ?�릿 1발당 기본 ?��?지 (?�림 계산)
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

                // 1. 발사??본인 ?�는 ?�군 ?�레?�어 검??(Friendly Fire 방�? - E-12)
                PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
                if (hitPlayer != null)
                {
                    continue;
                }

                // 2. 거리�??��?지 감쇄 계산
                // 0 ~ 3m: 100% ?� ?��?지
                // 3 ~ 10m: 거리 비�? ?�형 감쇄
                // 10 ~ 15m: ?�릿??최소 1 ?��?지 보장
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

                // 3. Hitbox(?�드??부?�별) ?�인
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
                    acc.HitCount++;
                    acc.Direction = dir;

                    if (hitbox.Type == HitboxType.Head)
                    {
                        acc.HeadDamage += Mathf.RoundToInt(currentPelletDamage * 1.5f);
                    }
                    else
                    {
                        acc.BodyDamage += currentPelletDamage;
                    }
                }
                // 4. ?�반 IDamageable ?�인 (몬스???�는 DestructibleObject)
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
                    acc.HitCount++;
                    acc.Direction = dir;
                        acc.BodyDamage += currentPelletDamage;
                    }
                }

                // 5. 물리 객체(PickableItem / Rigidbody) ?�력 ?�달
                if (hit.collider.TryGetComponent<PickableItem>(out var pickable))
                {
                    float impulseForce = currentPelletDamage * 0.15f;
                    pickable.ApplyImpulse(dir * impulseForce, hit.point);
                }
                
            }
        }

        // ?�?�별�?[?�드 ?�산 1?? + [몸통 ?�산 1?? ?��?지 ?�용 (E-15)
        foreach (var kvp in targetDamageMap)
        {
            IDamageable target = kvp.Key;
            ShotgunAccumulatedDamage acc = kvp.Value;

            if (acc.HeadDamage > 0)
            {
                DamageInfo headDmg = new DamageInfo(acc.HeadDamage, instigatorClientId, acc.LastHitPoint, acc.LastHitNormal, HitboxType.Head, isCharged, _currentGunData.KnockbackForce * acc.HitCount, acc.Direction);
                target.TakeDamage(headDmg);
                Debug.Log($"[Server] ?�건 ?�드 ?�산 ?�중! ?�?? {((Component)target).name}, ?��?지: {acc.HeadDamage} (차�?: {isCharged})");
            }

            if (acc.BodyDamage > 0)
            {
                DamageInfo bodyDmg = new DamageInfo(acc.BodyDamage, instigatorClientId, acc.LastHitPoint, acc.LastHitNormal, HitboxType.Body, isCharged, _currentGunData.KnockbackForce * acc.HitCount, acc.Direction);
                target.TakeDamage(bodyDmg);
                Debug.Log($"[Server] ?�건 몸통 ?�산 ?�중! ?�?? {((Component)target).name}, ?��?지: {acc.BodyDamage} (차�?: {isCharged})");
            }
        }

        // 모든 ?�라?�언?�에 8�??�격 지???�티??�??�격 ?�레?�어 ?�레?�서 브로?�캐?�트
        NotifyShotgunHitClientRpc(origin, hitPoints, hitNormals, hitSomethings, surfaceTypes);
    }

    [ClientRpc]
    private void NotifyShotgunHitClientRpc(Vector3 origin, Vector3[] hitPoints, Vector3[] hitNormals, bool[] hitSomethings, byte[] surfaceTypes)
    {
        // 1. ?�격 ?�레?�어??경우 총구 격발?? Muzzle Flash �?8�?Tracer ?�생
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

        // 2. ?�격 지?�에 ?�질�??�티???�폰 (모든 ?�라?�언??
        if (hitPoints != null && hitSomethings != null && surfaceTypes != null)
        {
            for (int i = 0; i < hitPoints.Length; i++)
            {
                if (hitSomethings[i])
                {
                    SurfaceType surfaceType = (SurfaceType)surfaceTypes[i];
                    SpawnImpactEffect(hitPoints[i], hitNormals[i], surfaceType);

                    // ?�격 ?�레?�어 ?�격??경우 8�??�칼 ?�폰 (로컬 ?�레?�어??발사 ?�간 0ms ?�반???�료)
                    if (!IsOwner && surfaceType != SurfaceType.Flesh)
                    {
                        Transform hitTarget = null;
                        if (Physics.Raycast(hitPoints[i] + hitNormals[i] * 0.05f, -hitNormals[i], out RaycastHit rHit, 0.15f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            hitTarget = rHit.collider.transform;
                        }
                        DecalPoolManager.Instance.SpawnBulletHole(hitPoints[i], hitNormals[i], surfaceType, hitTarget);
                    }
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
            Vector3 startPos = from;
            
            if (_playerItemHolder != null && _playerItemHolder.CurrentHeldInstance != null)
            {
                var visual = _playerItemHolder.CurrentHeldInstance.GetComponent<HeldItemVisual>();
                if (visual != null && visual.MuzzlePoint != null)
                {
                    startPos = visual.MuzzlePoint.position;
                }
            }
            else if (_playerInteraction != null && _playerInteraction.HoldPoint != null)
            {
                startPos = _playerInteraction.HoldPoint.position + _playerInteraction.HoldPoint.forward * 0.4f;
            }

            GameObject tracer = Instantiate(_currentGunData.BulletTracerPrefab, startPos, Quaternion.identity);
            LineRenderer lr = tracer.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.SetPosition(0, startPos);
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

        // ?��? ?�창??가??�??�으�??�장??불필??        if (_currentGunSlot.CurrentAmmo >= _currentGunData.MagazineCapacity)
        {
            return;
        }

        // ?�벤?�리???�약??0개이�??�전 불�? (E-07, E-10)
        int reserveAmmo = TotalReserveAmmo;
        if (reserveAmmo <= 0)
        {
            Debug.Log("[PlayerGunCombat] ?�장??불�?: ?�벤?�리???�약???�습?�다.");
            return;
        }

        // 차징 중이?�다�?취소
        CancelCharge();

        if (_animator != null)
        {
            _animator.SetTrigger("Reload");
        }

        // ?�리???�중 ?�장???�도 ???�리�?즉시 ?�제 (E-18)
        if (_playerController != null && _playerController.IsSprinting)
        {
            _playerController.CancelSprint(0.15f);
        }

        // ??바이 ???�전 무기??경우 1발씩 ?�전 루틴 ?�작
        if (_currentGunData.UseShellByShellReload)
        {
            StartShellReload();
            return;
        }

        // ?�반 ?�창 교체???�장??        _isReloading = true;
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
        Debug.Log($"[PlayerGunCombat] ?�장???�작... ({_currentGunData.ReloadDuration}�??�요)");
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
            // ?�벤?�리?�서 ?�는 만큼�??�모?�여 충전
            int consumed = _playerInventory.ConsumeAmmo(_currentGunData.RequiredAmmoItemId, needed);
            _currentGunSlot.CurrentAmmo += consumed;
            Debug.Log($"[PlayerGunCombat] ?�장???�료! 충전?? {consumed}�? ?�재 ?�창: {_currentGunSlot.CurrentAmmo}/{_currentGunData.MagazineCapacity}");
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
            Debug.Log("[PlayerGunCombat] ?�장??취소??(무기 교체 ??.");
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

        // ?�작???�생
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
        Debug.Log($"[PlayerGunCombat] ??바이 ???�전 ?�작 (?�딜?�이: {_currentGunData.ReloadStartDelay}�?");
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

                    // ?�벤?�리 ?�여 ?�약 검??(E-07, E-08)
                    int reserve = TotalReserveAmmo;
                    if (reserve <= 0)
                    {
                        // ???�상 ?�약???�으�??�전 종료 ?�계�?즉시 ?�환
                        _shellReloadStage = ShellReloadStage.Ending;
                        return;
                    }

                    // 1�??�모 �??�전
                    int consumed = _playerInventory != null ? _playerInventory.ConsumeAmmo(_currentGunData.RequiredAmmoItemId, 1) : 0;
                    if (consumed > 0)
                    {
                        _currentGunSlot.CurrentAmmo += consumed;
                        OnAmmoChanged?.Invoke();

                        // 1�??�입 ?�운???�생
                        if (_currentGunData.ReloadInsertSound != null)
                        {
                            AudioSource.PlayClipAtPoint(_currentGunData.ReloadInsertSound, _mainCamera.transform.position);
                        }
                        if (IsSpawned)
                        {
                            RequestShellReloadSoundServerRpc(transform.position, 1);
                        }

                        Debug.Log($"[PlayerGunCombat] ??1�??�전 ?�료! ({_currentGunSlot.CurrentAmmo}/{_currentGunData.MagazineCapacity})");
                    }

                    // ?�창??가??찼거???�벤?�리 ?�약??바닥?�다�?종료 ?�계�??�환
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
            Debug.Log("[PlayerGunCombat] ??바이 ???�전 취소??(?�재 ?�전???�약 보존).");
        }
    }

    private void CompleteShellReload()
    {
        _shellReloadStage = ShellReloadStage.None;
        _shellReloadTimer = 0f;
        UpdateCombatActionStateSync();

        // ?�전 종료 ?�운???�생
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
        Debug.Log("[PlayerGunCombat] ??바이 ???�전 ?�체 ?�료!");
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
        // 1. PlayerItemHolder가 ?�에 ?�고 ?�는 ?�이???�선 ?�인 (바닥?�서 주운 Pending ?�이???�함)
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

            // ?�롯???�탄??미초기화(-1)??경우 ?�창 ?�충?�로 초기??            if (_currentGunSlot.CurrentAmmo < 0)
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

        // ?�벤?�리???��? 총기가 ?�는지 검??        
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
    /// F1 ???�력 ???�는 ?�작 ???�스?�용 총기 3�?�??�약???�벤?�리??지급합?�다.
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
        Debug.Log("<color=green>[PlayerGunCombat] ?�스??총기 4�??�격?�총, 권총, 차�??�이?�, ?�프?�건) �??�약???�벤?�리??지급되?�습?�다! (?�축?? F1)</color>");
    }

    #endregion
}
