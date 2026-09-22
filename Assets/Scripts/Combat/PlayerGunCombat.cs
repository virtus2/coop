using System;
using Coop.VFX;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 총기 사격(단발, 연사, 차지샷), 재장전, 반동, 서버 권한 히트스캔 판정 및 동기화를 총괄하는 컴포넌트입니다.
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
    [Tooltip("체크 시 게임 시작 시 인벤토리에 총기와 탄약이 없다면 자동으로 테스트 총기 3종 및 탄약을 지급합니다.")]
    [SerializeField] private bool _autoEquipGunsOnStart = true;

    private Camera _mainCamera;
    private GunItemData _currentGunData;
    private InventorySlot _currentGunSlot;

    // 사격 및 쿨타임 상태
    private float _fireCooldownTimer;
    private bool _isFiringInputHeld;

    // 차지샷 상태
    private bool _isCharging;
    private float _currentChargeTime;
    private bool _hasPlayedChargeReadySound;

    // 재장전 상태
    private bool _isReloading;
    private float _reloadTimer;

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
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _currentGunData != null && _isReloading
        ? Mathf.Clamp01(_reloadTimer / _currentGunData.ReloadDuration)
        : 0f;
    public bool IsCharging => _isCharging;
    public float ChargeProgress => _currentGunData != null && _isCharging
        ? Mathf.Clamp01(_currentChargeTime / _currentGunData.MinChargeDuration)
        : 0f;

    public event Action OnAmmoChanged;
    public event Action OnReloadStarted;
    public event Action OnReloadCompleted;
    public event Action OnReloadCancelled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        LocalInstance = null;
    }

    private void Awake()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_playerInventory == null) _playerInventory = GetComponent<PlayerInventory>();
        if (_playerItemHolder == null) _playerItemHolder = GetComponent<PlayerItemHolder>();
        if (_playerInteraction == null) _playerInteraction = GetComponent<PlayerInteraction>();
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
    }

    public override void OnNetworkDespawn()
    {
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

        if (_isReloading)
        {
            _reloadTimer += Time.deltaTime;
            if (_reloadTimer >= _currentGunData.ReloadDuration)
            {
                CompleteReload();
            }
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

        // 1. 재장전 도중 달리기(Shift) 시도 시 재장전 즉시 취소 (E-14)
        if (_isReloading && isSprinting)
        {
            CancelReload();
        }

        // 2. 사격/차징 도중 달리기(Shift) 시도 시 즉시 중단 및 마우스 재클릭 요구 (E-12, 상황 A)
        if (isSprinting)
        {
            if (_isCharging)
            {
                CancelCharge();
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                _requireMouseReleaseToFire = true;
            }
        }

        // 3. 재장전 진행 중에는 발사 입력 무시하고 재장전 계속 진행 (E-03)
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

        // 3. 로컬 비주얼 및 오디오 즉각 재생 (0ms 체감)
        ExecuteLocalFirePrediction(isCharged);

        // 4. 서버에 완전 권한 레이캐스트 요청
        Ray aimRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RequestFireServerRpc(aimRay.origin, aimRay.direction, isCharged, NetworkManager.Singleton.LocalClientId);
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
        if (_currentGunData == null || _currentGunSlot == null || _isReloading) return;

        // 이미 탄창이 가득 차 있으면 재장전 불필요
        if (_currentGunSlot.CurrentAmmo >= _currentGunData.MagazineCapacity)
        {
            return;
        }

        // 인벤토리에 탄약이 0개이면 장전 불가 (E-07)
        int reserveAmmo = TotalReserveAmmo;
        if (reserveAmmo <= 0)
        {
            Debug.Log("[PlayerGunCombat] 재장전 불가: 인벤토리에 탄약이 없습니다.");
            return;
        }

        // 차징 중이었다면 취소
        CancelCharge();

        // 달리는 도중 재장전 시도 시 달리기 즉시 해제 (E-15)
        if (_playerController != null && _playerController.IsSprinting)
        {
            _playerController.CancelSprint(0.15f);
        }

        _isReloading = true;
        _reloadTimer = 0f;

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

        OnReloadCompleted?.Invoke();
        OnAmmoChanged?.Invoke();
    }

    public void CancelReload()
    {
        if (_isReloading)
        {
            _isReloading = false;
            _reloadTimer = 0f;
            OnReloadCancelled?.Invoke();
            Debug.Log("[PlayerGunCombat] 재장전 취소됨 (무기 교체 등).");
        }
    }

    #endregion

    #region Inventory & Weapon Change Handling

    private void HandleToolbarSlotChanged(int newIndex)
    {
        CancelCharge();
        CancelReload();
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
        ItemData ammoRifle = Resources.Load<ItemData>("ItemData/Ammo_Rifle");
        ItemData ammoPistol = Resources.Load<ItemData>("ItemData/Ammo_Pistol");
        ItemData ammoEnergy = Resources.Load<ItemData>("ItemData/Ammo_Energy");

        if (rifle != null) _playerInventory.AddItem(rifle, 1);
        if (pistol != null) _playerInventory.AddItem(pistol, 1);
        if (laser != null) _playerInventory.AddItem(laser, 1);
        if (ammoRifle != null) _playerInventory.AddItem(ammoRifle, 120);
        if (ammoPistol != null) _playerInventory.AddItem(ammoPistol, 60);
        if (ammoEnergy != null) _playerInventory.AddItem(ammoEnergy, 50);

        CheckCurrentHeldGun();
        Debug.Log("<color=green>[PlayerGunCombat] 테스트 총기 3종(돌격소총, 권총, 차지레이저) 및 탄약이 인벤토리에 지급되었습니다! (단축키: F1)</color>");
    }

    #endregion
}
