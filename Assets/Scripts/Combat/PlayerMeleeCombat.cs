using System;
using System.Collections;
using Coop.VFX;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 근접 무기(단검, 둔기 등) 장착 감지, 공격 입력(Auto-Swing),
/// 선딜레이 및 구체 캐스트 판정, 서버 권한 데미지/넉백/경직 부여를 총괄하는 컴포넌트입니다.
/// </summary>
public class PlayerMeleeCombat : NetworkBehaviour
{
    public static PlayerMeleeCombat LocalInstance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private PlayerItemHolder _playerItemHolder;
    [SerializeField] private PlayerInteraction _playerInteraction;

    [Header("Hit Detection Settings")]
    [Tooltip("근접 공격 대상 레이어 마스크입니다. 기본값은 모든 레이어(~0)이며, 런타임에 로컬 플레이어의 콜라이더 레이어가 자동 제외됩니다.")]
    [SerializeField] private LayerMask _hitLayerMask = ~0;

    private Camera _mainCamera;
    private Animator _animator;
    private MeleeItemData _currentMeleeData;

    // 공격 타이밍 및 쿨타임 상태
    private bool _isAttacking;
    private float _attackTimer;
    private float _cooldownTimer;
    private bool _hitEvaluated;

    // 뷰모델 스윙 연출용 코루틴
    private Coroutine _swingVisualCoroutine;

    // 외부 노출 프로퍼티
    public MeleeItemData CurrentMeleeData => _currentMeleeData;
    public bool HasMeleeEquipped => _currentMeleeData != null;
    /// <summary>
    /// 공격 모션(선딜레이 + 후딜레이)이 진행 중인지 여부입니다.
    /// 공격 중에는 핫바 무기 교체 및 Q키 드롭 입력이 차단됩니다.
    /// </summary>
    public bool IsAttacking => _isAttacking;

    public event Action OnMeleeAttackStarted;
    public event Action OnMeleeAttackCompleted;

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
            CheckCurrentHeldMelee();
        }
    }

    private void Start()
    {
        if (!IsSpawned || IsOwner)
        {
            LocalInstance = this;
            _mainCamera = Camera.main;

            if (_playerInventory != null)
            {
                _playerInventory.OnSelectedToolbarSlotChanged += HandleToolbarSlotChanged;
                _playerInventory.OnInventoryChanged += HandleInventoryChanged;
            }

            CheckCurrentHeldMelee();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
            _mainCamera = Camera.main;

            if (_playerInventory != null)
            {
                _playerInventory.OnSelectedToolbarSlotChanged += HandleToolbarSlotChanged;
                _playerInventory.OnInventoryChanged += HandleInventoryChanged;
            }

            CheckCurrentHeldMelee();
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
    }

    public override void OnDestroy()
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

        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        UpdateCooldownTimers();
        UpdateAttackProgress();
        HandleMeleeInput();
    }

    #region Inventory & Item Check

    private void HandleToolbarSlotChanged(int newIndex)
    {
        CheckCurrentHeldMelee();
    }

    private void HandleInventoryChanged()
    {
        CheckCurrentHeldMelee();
    }

    private void CheckCurrentHeldMelee()
    {
        // 1. PlayerItemHolder가 손에 들고 있는 아이템 우선 확인 (바닥에서 주운 Pending 아이템 포함)
        if (_playerItemHolder != null && _playerItemHolder.CurrentHeldItemData is MeleeItemData holderMelee)
        {
            SetCurrentMelee(holderMelee);
            return;
        }

        // 2. 인벤토리 툴바 슬롯 확인
        if (_playerInventory != null)
        {
            ItemData heldItem = _playerInventory.GetHeldItemData();
            if (heldItem is MeleeItemData meleeData && heldItem.ActionType == ItemActionType.MeleeWeapon)
            {
                SetCurrentMelee(meleeData);
                return;
            }
        }

        SetCurrentMelee(null);
    }

    private void SetCurrentMelee(MeleeItemData meleeData)
    {
        if (_currentMeleeData == meleeData) return;

        if (_swingVisualCoroutine != null)
        {
            StopCoroutine(_swingVisualCoroutine);
            _swingVisualCoroutine = null;
            if (_playerItemHolder != null && _playerItemHolder.CurrentHeldInstance != null && _currentMeleeData != null)
            {
                _playerItemHolder.CurrentHeldInstance.transform.localPosition = _currentMeleeData.HeldLocalPosition;
                _playerItemHolder.CurrentHeldInstance.transform.localRotation = Quaternion.Euler(_currentMeleeData.HeldLocalRotation);
            }
        }

        _currentMeleeData = meleeData;
        _isAttacking = false;
        _hitEvaluated = false;
        _attackTimer = 0f;

        if (_animator != null)
        {
            _animator.SetInteger("WeaponType", meleeData != null ? 3 : 0);
        }
    }

    #endregion

    #region Combat Flow & Timers

    private void UpdateCooldownTimers()
    {
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }
    }

    private void UpdateAttackProgress()
    {
        if (!_isAttacking || _currentMeleeData == null) return;

        _attackTimer += Time.deltaTime;

        // 1. 선딜레이(Windup) 경과 시 타격 판정 실행 (E-03: 피격되어도 취소되지 않는 슈퍼아머 유지)
        if (!_hitEvaluated && _attackTimer >= _currentMeleeData.WindupDuration)
        {
            _hitEvaluated = true;
            EvaluateHit();
        }

        // 2. 전체 모션(선딜 + 후딜) 완료 시 공격 종료
        float totalDuration = _currentMeleeData.WindupDuration + _currentMeleeData.RecoveryDuration;
        if (_attackTimer >= totalDuration)
        {
            _isAttacking = false;
            OnMeleeAttackCompleted?.Invoke();
        }
    }

    private void HandleMeleeInput()
    {
        if (_currentMeleeData == null) return;

        // 마우스 좌클릭 홀드 감지 (Auto-Swing)
        bool isLeftButtonPressed = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (isLeftButtonPressed && !_isAttacking && _cooldownTimer <= 0f)
        {
            StartAttack();
        }
    }

    private void StartAttack()
    {
        if (_currentMeleeData == null) return;

        _isAttacking = true;
        _hitEvaluated = false;
        _attackTimer = 0f;
        _cooldownTimer = _currentMeleeData.AttackInterval;

        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }

        // 달리기 취소 및 이동 속도 감속 페널티 부여
        if (_playerController != null)
        {
            float totalDuration = _currentMeleeData.WindupDuration + _currentMeleeData.RecoveryDuration;
            _playerController.CancelSprint(totalDuration + 0.05f);
            _playerController.ApplyShootingPenalty(_currentMeleeData.MovementPenaltyMultiplier, totalDuration);
        }

        // 로컬 스윙 사운드 재생
        if (_currentMeleeData.SwingSound != null && _mainCamera != null)
        {
            AudioSource.PlayClipAtPoint(_currentMeleeData.SwingSound, _mainCamera.transform.position);
        }

        if (IsSpawned)
        {
            RequestMeleeSwingServerRpc(transform.position);
        }

        // 뷰모델 휘두르기 프로시저럴 연출
        PlaySwingVisual();

        OnMeleeAttackStarted?.Invoke();
    }

    #endregion

    #region Procedural Viewmodel Swing

    private void PlaySwingVisual()
    {
        if (_swingVisualCoroutine != null)
        {
            StopCoroutine(_swingVisualCoroutine);
        }
        _swingVisualCoroutine = StartCoroutine(AnimateSwingRoutine());
    }

    private IEnumerator AnimateSwingRoutine()
    {
        if (_playerItemHolder == null) yield break;
        EnsureMeleeDataLoaded();
        if (_currentMeleeData == null) yield break;

        GameObject heldObj = _playerItemHolder.CurrentHeldInstance;
        if (heldObj == null) yield break;

        Transform heldTransform = heldObj.transform;
        Vector3 initialPos = _currentMeleeData.HeldLocalPosition;
        Quaternion initialRot = Quaternion.Euler(_currentMeleeData.HeldLocalRotation);

        float windup = _currentMeleeData.WindupDuration;
        float recovery = _currentMeleeData.RecoveryDuration;

        // 1. 선딜레이: 뒤로 살짝 젖힘 (Wind-up)
        Vector3 windupPos = initialPos + new Vector3(-0.05f, 0.04f, -0.1f);
        Quaternion windupRot = initialRot * Quaternion.Euler(-15f, 25f, -10f);

        float elapsed = 0f;
        while (elapsed < windup)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / windup);
            heldTransform.localPosition = Vector3.Lerp(initialPos, windupPos, t);
            heldTransform.localRotation = Quaternion.Slerp(initialRot, windupRot, t);
            yield return null;
        }

        // 2. 타격 스윙: 앞으로 빠르고 강하게 휘두름 (Swing Through)
        Vector3 swingPos = initialPos + new Vector3(0.08f, -0.06f, 0.15f);
        Quaternion swingRot = initialRot * Quaternion.Euler(25f, -40f, 20f);

        float swingTime = Mathf.Min(0.08f, recovery * 0.3f);
        elapsed = 0f;
        while (elapsed < swingTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swingTime);
            heldTransform.localPosition = Vector3.Lerp(windupPos, swingPos, t);
            heldTransform.localRotation = Quaternion.Slerp(windupRot, swingRot, t);
            yield return null;
        }

        // 3. 후딜레이: 원래 자세로 부드럽게 복귀 (Recovery)
        float recoverTime = Mathf.Max(0.05f, recovery - swingTime);
        elapsed = 0f;
        while (elapsed < recoverTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoverTime);
            heldTransform.localPosition = Vector3.Lerp(swingPos, initialPos, t);
            heldTransform.localRotation = Quaternion.Slerp(swingRot, initialRot, t);
            yield return null;
        }

        heldTransform.localPosition = initialPos;
        heldTransform.localRotation = initialRot;
        _swingVisualCoroutine = null;
    }

    #endregion

    #region Hit Detection & Wall Occlusion

    /// <summary>
    /// 타격 판정 시 로컬 플레이어 본인(호스트/클라이언트 무관)의 콜라이더 레이어를 제외한 실제 레이어 마스크를 반환합니다.
    /// </summary>
    public LayerMask GetEffectiveHitMask()
    {
        LayerMask mask = _hitLayerMask;

        // 로컬 플레이어 루트 오브젝트의 레이어 제외
        mask &= ~(1 << gameObject.layer);

        // 기본 "Player" 레이어 등록 시 추가 제외
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            mask &= ~(1 << playerLayer);
        }

        return mask;
    }

    private void EvaluateHit()
    {
        if (_currentMeleeData == null || _mainCamera == null) return;

        Ray aimRay = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float maxRange = _currentMeleeData.AttackRange;
        float radius = _currentMeleeData.AttackRadius;
        LayerMask hitMask = GetEffectiveHitMask();

        // 1. 벽/장애물 차단 검사 (E-06: 벽 레이캐스트로 먼저 가로막히는지 확인)
        bool hasLineHit = Physics.Raycast(aimRay, out RaycastHit lineHit, maxRange, hitMask, QueryTriggerInteraction.Ignore);

        // 2. 판정 보정을 위한 SphereCast 실행 (단일 대상 타격)
        bool hasSphereHit = Physics.SphereCast(aimRay, radius, out RaycastHit sphereHit, maxRange, hitMask, QueryTriggerInteraction.Ignore);

        RaycastHit primaryHit;
        bool hitSomething = false;

        if (hasLineHit && hasSphereHit)
        {
            // 더 가까운 타겟을 우선하되, 선체크(정중앙) 타겟이 적이면 우선
            primaryHit = lineHit.distance <= sphereHit.distance ? lineHit : sphereHit;
            hitSomething = true;
        }
        else if (hasLineHit)
        {
            primaryHit = lineHit;
            hitSomething = true;
        }
        else if (hasSphereHit)
        {
            primaryHit = sphereHit;
            hitSomething = true;
        }
        else
        {
            primaryHit = default;
            hitSomething = false;
        }

        if (!hitSomething)
        {
            return;
        }

        Collider hitCollider = primaryHit.collider;
        Vector3 hitPoint = primaryHit.point;
        Vector3 hitNormal = primaryHit.normal;

        // 로컬 플레이어 본인 충돌 방지 (2중 안전장치: 마스크 필터링 및 인스턴스 검사)
        if (_playerController != null && hitCollider.transform.IsChildOf(_playerController.transform))
        {
            return;
        }

        // 팀킬 방지 (Friendly Fire OFF - E-09)
        PlayerController hitPlayer = hitCollider.GetComponentInParent<PlayerController>();
        if (hitPlayer != null)
        {
            // 아군 플레이어는 무시
            return;
        }

        // 대상 식별 (IDamageable 또는 Hitbox)
        Hitbox hitbox = hitCollider.GetComponent<Hitbox>();
        IDamageable damageable = hitbox != null ? hitbox.Damageable : hitCollider.GetComponentInParent<IDamageable>();

        if (damageable != null && !damageable.IsDead)
        {
            // 벽 차단 추가 검증: 공격 원점에서 대상 중심까지 레이를 쏴서 중간에 벽이 있는지 확인
            Vector3 targetCenter = hitCollider.bounds.center;
            Vector3 originToTarget = targetCenter - aimRay.origin;
            float distToTarget = originToTarget.magnitude;

            if (Physics.Raycast(aimRay.origin, originToTarget.normalized, out RaycastHit wallObstructionHit, distToTarget - 0.1f, hitMask, QueryTriggerInteraction.Ignore))
            {
                // 중간에 다른 장애물이 가로막음 (IDamageable이 아닌 환경 벽)
                if (wallObstructionHit.collider.GetComponentInParent<IDamageable>() == null)
                {
                    SurfaceType wallSurface = DetermineSurfaceType(wallObstructionHit.collider);
                    PlayWallHitSound(wallObstructionHit.point);
                    SpawnImpactEffect(wallObstructionHit.point, wallObstructionHit.normal, wallSurface);
                    if (IsSpawned)
                    {
                        RequestMeleeWallHitServerRpc(wallObstructionHit.point, wallObstructionHit.normal, (byte)wallSurface);
                    }
                    return;
                }
            }

            // 적/사물 명중!
            bool isHeadshot = hitbox != null && hitbox.Type == HitboxType.Head;
            Vector3 knockbackDir = aimRay.direction;
            knockbackDir.y = 0f;
            if (knockbackDir.sqrMagnitude > 0.001f)
            {
                knockbackDir.Normalize();
            }

            SurfaceType surfaceType = DetermineSurfaceType(hitCollider);

            // 로컬 타격 피드백 즉시 재생 (SFX / VFX)
            PlayDamageableHitFeedback(damageable, hitPoint);
            SpawnImpactEffect(hitPoint, hitNormal, surfaceType);

            // 서버 권한 타격 판정 및 데미지/넉백 요청
            NetworkObject targetNetObj = (damageable as Component)?.GetComponentInParent<NetworkObject>();
            if (targetNetObj != null)
            {
                RequestMeleeAttackServerRpc(
                    targetNetObj.NetworkObjectId,
                    hitPoint,
                    hitNormal,
                    isHeadshot,
                    knockbackDir,
                    (byte)surfaceType,
                    NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0
                );
            }
            else
            {
                // 로컬 전용 파괴 가능 사물 (NetworkObject가 없는 경우)
                RequestNonNetworkedDamageServerRpc(
                    hitPoint,
                    hitNormal,
                    _currentMeleeData.BaseDamage,
                    NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0
                );
            }
        }
        else
        {
            // 환경 벽이나 지형에 충돌
            SurfaceType surfaceType = DetermineSurfaceType(hitCollider);
            PlayWallHitSound(hitPoint);
            SpawnImpactEffect(hitPoint, hitNormal, surfaceType);
            if (IsSpawned)
            {
                RequestMeleeWallHitServerRpc(hitPoint, hitNormal, (byte)surfaceType);
            }
        }
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

    private void SpawnImpactEffect(Vector3 point, Vector3 normal, SurfaceType surfaceType)
    {
        EnsureMeleeDataLoaded();

        GameObject overridePrefab = _currentMeleeData != null && _currentMeleeData.ImpactEffectPrefab != null
            ? _currentMeleeData.ImpactEffectPrefab
            : null;

        VfxPoolManager.Instance.SpawnImpact(surfaceType, point, normal, overridePrefab);
    }

    private void PlayWallHitSound(Vector3 point)
    {
        EnsureMeleeDataLoaded();
        if (_currentMeleeData != null && _currentMeleeData.HitWallSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentMeleeData.HitWallSound, point);
        }
    }

    private void PlayDamageableHitFeedback(IDamageable damageable, Vector3 point)
    {
        EnsureMeleeDataLoaded();
        if (_currentMeleeData == null) return;

        bool isFlesh = damageable is NonPlayerCharacter;
        AudioClip sfx = isFlesh ? _currentMeleeData.HitFleshSound : _currentMeleeData.HitObjectSound;
        if (sfx != null)
        {
            AudioSource.PlayClipAtPoint(sfx, point);
        }
    }

    private void EnsureMeleeDataLoaded()
    {
        if (_currentMeleeData == null && _playerItemHolder != null)
        {
            string heldId = _playerItemHolder.NetworkHeldItemId;
            if (!string.IsNullOrEmpty(heldId))
            {
                SetCurrentMelee(ItemDatabase.GetItem(heldId) as MeleeItemData);
            }
        }
    }

    #endregion

    /// <summary>
    /// PlayerItemHolder의 손 아이템 갱신(로컬 픽업, 툴바 변경, 네트워크 동기화 등)에 의해 호출되어 현재 장착 근접무기 데이터를 동기화합니다.
    /// </summary>
    public void SetEquippedMelee(MeleeItemData meleeData)
    {
        SetCurrentMelee(meleeData);
    }

    /// <summary>
    /// 하위 호환성 유지용 메서드입니다.
    /// </summary>
    public void SetEquippedMeleeFromNetwork(MeleeItemData meleeData)
    {
        SetCurrentMelee(meleeData);
    }

    #region Server-Authoritative Melee Damage RPC

    [ServerRpc]
    private void RequestMeleeAttackServerRpc(
        ulong targetNetworkObjectId,
        Vector3 hitPoint,
        Vector3 hitNormal,
        bool isHeadshot,
        Vector3 knockbackDir,
        byte surfaceTypeByte,
        ulong instigatorClientId)
    {
        // 1. 서버 측 _currentMeleeData 누락 시 PlayerItemHolder를 통한 복구 시도
        if (_currentMeleeData == null && _playerItemHolder != null)
        {
            if (_playerItemHolder.CurrentHeldItemData is MeleeItemData heldMelee)
            {
                SetCurrentMelee(heldMelee);
            }
            else
            {
                string heldId = _playerItemHolder.NetworkHeldItemId;
                if (!string.IsNullOrEmpty(heldId))
                {
                    SetCurrentMelee(ItemDatabase.GetItem(heldId) as MeleeItemData);
                }
            }
        }

        if (_currentMeleeData == null)
        {
            Debug.LogWarning($"[Server] RequestMeleeAttackServerRpc 무시: 공격자(ClientId={instigatorClientId})의 장착 근접무기 데이터를 찾을 수 없습니다.");
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            return;
        }

        IDamageable damageable = targetNetObj.GetComponentInChildren<IDamageable>();
        if (damageable == null || damageable.IsDead)
        {
            return;
        }

        // 서버 위치 기준 사거리 검증 (레이턴시 보정 포함)
        float maxAllowedDist = _currentMeleeData.AttackRange + 1.2f;
        float actualDist = Vector3.Distance(transform.position, targetNetObj.transform.position);
        if (actualDist > maxAllowedDist)
        {
            return;
        }

        // 데미지 계산: 헤드샷 1.5배 보너스
        int damage = _currentMeleeData.BaseDamage;
        if (isHeadshot)
        {
            damage = Mathf.RoundToInt(damage * 1.5f);
        }

        DamageInfo damageInfo = new DamageInfo(
            damage,
            instigatorClientId,
            hitPoint,
            hitNormal,
            isHeadshot ? HitboxType.Head : HitboxType.Body,
            false,
            _currentMeleeData.KnockbackForce,
            knockbackDir
        );

        damageable.TakeDamage(damageInfo);

        // 원격 클라이언트들을 위한 타격 이펙트 브로드캐스트
        NotifyMeleeImpactClientRpc(hitPoint, hitNormal, surfaceTypeByte, damageable is NonPlayerCharacter);
    }

    [ServerRpc]
    private void RequestNonNetworkedDamageServerRpc(
        Vector3 hitPoint,
        Vector3 hitNormal,
        int damage,
        ulong instigatorClientId)
    {
        // 씬 내 NetworkObject가 없는 IDamageable 탐색 및 데미지 적용
        Collider[] hits = Physics.OverlapSphere(hitPoint, 0.5f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in hits)
        {
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                DamageInfo info = new DamageInfo(damage, instigatorClientId, hitPoint, hitNormal);
                damageable.TakeDamage(info);
                break;
            }
        }
    }

    [ServerRpc]
    private void RequestMeleeSwingServerRpc(Vector3 soundPos)
    {
        NotifyMeleeSwingClientRpc(soundPos);
    }

    [ClientRpc]
    private void NotifyMeleeSwingClientRpc(Vector3 soundPos)
    {
        if (IsOwner) return;

        EnsureMeleeDataLoaded();

        if (_currentMeleeData != null && _currentMeleeData.SwingSound != null)
        {
            AudioSource.PlayClipAtPoint(_currentMeleeData.SwingSound, soundPos);
        }

        // 원격 플레이어 화면에서도 3인칭 손 무기 휘두르기 모션 재생
        PlaySwingVisual();
    }

    [ServerRpc]
    private void RequestMeleeWallHitServerRpc(Vector3 hitPoint, Vector3 hitNormal, byte surfaceTypeByte)
    {
        NotifyMeleeWallHitClientRpc(hitPoint, hitNormal, surfaceTypeByte);
    }

    [ClientRpc]
    private void NotifyMeleeWallHitClientRpc(Vector3 hitPoint, Vector3 hitNormal, byte surfaceTypeByte)
    {
        if (IsOwner) return;
        PlayWallHitSound(hitPoint);
        SpawnImpactEffect(hitPoint, hitNormal, (SurfaceType)surfaceTypeByte);
    }

    [ClientRpc]
    private void NotifyMeleeImpactClientRpc(Vector3 point, Vector3 normal, byte surfaceTypeByte, bool isFlesh)
    {
        // 로컬 플레이어는 이미 즉시 재생했으므로 원격 클라이언트만 재생
        if (IsOwner) return;

        EnsureMeleeDataLoaded();
        if (_currentMeleeData == null) return;

        AudioClip sfx = isFlesh ? _currentMeleeData.HitFleshSound : _currentMeleeData.HitObjectSound;
        if (sfx != null)
        {
            AudioSource.PlayClipAtPoint(sfx, point);
        }

        SpawnImpactEffect(point, normal, (SurfaceType)surfaceTypeByte);
    }

    #endregion
}
