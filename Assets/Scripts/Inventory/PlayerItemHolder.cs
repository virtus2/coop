using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어가 손에 든 아이템의 상태를 관리하고,
/// 네트워크(NetworkVariable)를 통해 로컬 1인칭 및 원격 3인칭 손 뷰모델을 동기화합니다.
/// 단일 소켓(HeldItemVisual)의 Mesh 및 Material을 ItemData에 맞춰 교체하여
/// 런타임 Instantiate/Destroy 없이 최적의 성능으로 손 아이템을 렌더링합니다.
/// </summary>
public class PlayerItemHolder : NetworkBehaviour
{
    private PlayerInventory _inventory;
    private PlayerInteraction _interaction;

    [Header("Sockets")]
    [Tooltip("3인칭 캐릭터 모델의 오른손 소켓 (비어있으면 'HoldPoint3P' 또는 'RightHand'를 자동 탐색)")]
    [SerializeField] private Transform _thirdPersonHoldPoint;

    [Tooltip("1인칭 카메라 앞 소켓 (비어있으면 'CameraTarget/HoldPoint' 자동 탐색)")]
    [SerializeField] private Transform _firstPersonHoldPoint;

    // 1인칭 뷰모델 (로컬 플레이어 카메라 화면 전용)
    private GameObject _heldVisual1P;
    private MeshFilter _heldMeshFilter1P;
    private MeshRenderer _heldMeshRenderer1P;

    // 3인칭 월드모델 (오른손 소켓, 그림자 투영 및 원격 플레이어, 왼손 IK 기준)
    private GameObject _heldVisual3P;
    private MeshFilter _heldMeshFilter3P;
    private MeshRenderer _heldMeshRenderer3P;

    private ItemData _currentHeldItemData;
    private ItemData _pendingHeldItemData; // 바닥에서 주워 아직 툴바에 등록되지 않은 손 아이템
    private Animator _animator;
    private RuntimeAnimatorController _baseRuntimeController;

    // 네트워크 동기화용: 현재 손에 든 아이템의 고유 ID (비어있으면 string.Empty)
    private readonly NetworkVariable<FixedString64Bytes> _networkHeldItemId = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public GameObject CurrentHeldInstance => (_currentHeldItemData != null && _heldVisual1P != null && _heldVisual1P.activeSelf) ? _heldVisual1P : ((_currentHeldItemData != null && _heldVisual3P != null && _heldVisual3P.activeSelf) ? _heldVisual3P : null);
    public GameObject FirstPersonHeldInstance => (_currentHeldItemData != null && _heldVisual1P != null && _heldVisual1P.activeSelf) ? _heldVisual1P : null;
    public GameObject ThirdPersonHeldInstance => (_currentHeldItemData != null && _heldVisual3P != null && _heldVisual3P.activeSelf) ? _heldVisual3P : null;

    public ItemData CurrentHeldItemData => _currentHeldItemData;
    public ItemData PendingHeldItemData => _pendingHeldItemData;
    public bool IsHoldingItem => _currentHeldItemData != null;
    public bool IsHoldingWorldItem => _pendingHeldItemData != null;
    public string NetworkHeldItemId => _networkHeldItemId.Value.ToString();
    public Transform ThirdPersonHoldPoint => _thirdPersonHoldPoint;
    public Transform FirstPersonHoldPoint => _firstPersonHoldPoint;

    private void Awake()
    {
        EnsureComponents();
        EnsureHeldVisualSockets();
    }

    private void EnsureComponents()
    {
        if (_inventory == null)
        {
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null)
            {
                var character = GetComponent<PlayerCharacter>();
                if (character != null && character.Inventory != null)
                {
                    _inventory = character.Inventory;
                }
                else if (IsOwner && PlayerInventory.LocalInstance != null)
                {
                    _inventory = PlayerInventory.LocalInstance;
                }
            }
        }
        if (_interaction == null) _interaction = GetComponent<PlayerInteraction>();

        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }
            if (_animator != null && _baseRuntimeController == null && !(_animator.runtimeAnimatorController is AnimatorOverrideController))
            {
                _baseRuntimeController = _animator.runtimeAnimatorController;
            }
        }

        var pc = GetComponent<PlayerCharacter>();
        if (pc != null)
        {
            pc.OnInventoryBound -= HandleInventoryBound;
            pc.OnInventoryBound += HandleInventoryBound;
        }
    }

    private void HandleInventoryBound(PlayerInventory inventory)
    {
        _inventory = inventory;
        if (!IsSpawned || IsOwner)
        {
            if (_inventory != null)
            {
                _inventory.OnSelectedToolbarSlotChanged -= HandleSelectedSlotChanged;
                _inventory.OnSelectedToolbarSlotChanged += HandleSelectedSlotChanged;
                UpdateHeldItem(_inventory.SelectedToolbarIndex);
            }
        }
    }

    private void EnsureHeldVisualSockets()
    {
        Ensure1PHeldSocket();
        Ensure3PHeldSocket();
    }

    private void Ensure1PHeldSocket()
    {
        Transform target1P = Get1PHoldTarget();
        if (target1P == null) return;

        if (_heldVisual1P == null)
        {
            Transform found = target1P.Find("HeldItemVisual_1P");
            if (found == null) found = target1P.Find("HeldItemVisual");

            if (found != null)
            {
                _heldVisual1P = found.gameObject;
                _heldVisual1P.name = "HeldItemVisual_1P";
            }
            else
            {
                _heldVisual1P = new GameObject("HeldItemVisual_1P");
                _heldVisual1P.transform.SetParent(target1P, false);
                _heldVisual1P.transform.localPosition = Vector3.zero;
                _heldVisual1P.transform.localRotation = Quaternion.identity;
                _heldVisual1P.transform.localScale = Vector3.one;
            }
        }

        if (_heldVisual1P.transform.parent != target1P)
        {
            _heldVisual1P.transform.SetParent(target1P, false);
        }

        _heldVisual1P.layer = target1P.gameObject.layer;

        if (_heldMeshFilter1P == null)
        {
            _heldMeshFilter1P = _heldVisual1P.GetComponent<MeshFilter>();
            if (_heldMeshFilter1P == null) _heldMeshFilter1P = _heldVisual1P.AddComponent<MeshFilter>();
        }

        if (_heldMeshRenderer1P == null)
        {
            _heldMeshRenderer1P = _heldVisual1P.GetComponent<MeshRenderer>();
            if (_heldMeshRenderer1P == null) _heldMeshRenderer1P = _heldVisual1P.AddComponent<MeshRenderer>();
        }
    }

    private void Ensure3PHeldSocket()
    {
        Transform target3P = Get3PHoldTarget();
        if (target3P == null) return;

        if (_heldVisual3P == null)
        {
            Transform found = target3P.Find("HeldItemVisual_3P");
            if (found == null) found = target3P.Find("HeldItemVisual");

            if (found != null)
            {
                _heldVisual3P = found.gameObject;
                _heldVisual3P.name = "HeldItemVisual_3P";
            }
            else
            {
                _heldVisual3P = new GameObject("HeldItemVisual_3P");
                _heldVisual3P.transform.SetParent(target3P, false);
                _heldVisual3P.transform.localPosition = Vector3.zero;
                _heldVisual3P.transform.localRotation = Quaternion.identity;
                _heldVisual3P.transform.localScale = Vector3.one;
            }
        }

        if (_heldVisual3P.transform.parent != target3P)
        {
            _heldVisual3P.transform.SetParent(target3P, false);
        }

        _heldVisual3P.layer = target3P.gameObject.layer;

        if (_heldMeshFilter3P == null)
        {
            _heldMeshFilter3P = _heldVisual3P.GetComponent<MeshFilter>();
            if (_heldMeshFilter3P == null) _heldMeshFilter3P = _heldVisual3P.AddComponent<MeshFilter>();
        }

        if (_heldMeshRenderer3P == null)
        {
            _heldMeshRenderer3P = _heldVisual3P.GetComponent<MeshRenderer>();
            if (_heldMeshRenderer3P == null) _heldMeshRenderer3P = _heldVisual3P.AddComponent<MeshRenderer>();
        }
    }

    private void Start()
    {
        EnsureComponents();
        EnsureHeldVisualSockets();

        if (!IsSpawned || IsOwner)
        {
            if (_inventory != null)
            {
                _inventory.OnSelectedToolbarSlotChanged -= HandleSelectedSlotChanged;
                _inventory.OnSelectedToolbarSlotChanged += HandleSelectedSlotChanged;
                UpdateHeldItem(_inventory.SelectedToolbarIndex);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        EnsureComponents();
        EnsureHeldVisualSockets();

        _networkHeldItemId.OnValueChanged += HandleNetworkHeldItemChanged;

        if (IsOwner)
        {
            if (_inventory != null)
            {
                _inventory.OnSelectedToolbarSlotChanged -= HandleSelectedSlotChanged;
                _inventory.OnSelectedToolbarSlotChanged += HandleSelectedSlotChanged;
                UpdateHeldItem(_inventory.SelectedToolbarIndex);
            }
        }
        else
        {
            // 원격 클라이언트: 스폰 시점의 손 아이템 반영
            if (!string.IsNullOrEmpty(_networkHeldItemId.Value.ToString()))
            {
                ApplyRemoteHeldItem(_networkHeldItemId.Value.ToString());
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        _networkHeldItemId.OnValueChanged -= HandleNetworkHeldItemChanged;

        if (IsOwner && _inventory != null)
        {
            _inventory.OnSelectedToolbarSlotChanged -= HandleSelectedSlotChanged;
        }

        ClearHeldItem();
    }

    public override void OnDestroy()
    {
        if (_inventory != null)
        {
            _inventory.OnSelectedToolbarSlotChanged -= HandleSelectedSlotChanged;
        }

        ClearHeldItem();
        base.OnDestroy();
    }

    private void HandleNetworkHeldItemChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        if (!IsOwner)
        {
            ApplyRemoteHeldItem(newValue.ToString());
        }
    }

    private void HandleSelectedSlotChanged(int slotIndex)
    {
        UpdateHeldItem(slotIndex);
    }

    /// <summary>
    /// 현재 선택된 툴바 슬롯 인덱스에 따라 로컬 손 아이템을 갱신하고 서버에 동기화합니다.
    /// 1인칭 뷰모델과 3인칭 월드모델을 각각의 소켓에서 메쉬/머티리얼 교체 방식으로 제어합니다.
    /// </summary>
    public void UpdateHeldItem(int slotIndex)
    {
        EnsureComponents();
        EnsureHeldVisualSockets();

        // 1. 바닥에서 주워 아직 툴바에 넣지 않은 손 아이템이 있는 경우:
        //    툴바 슬롯 인덱스가 -1이더라도 손 아이템 비주얼을 유지합니다.
        if (_pendingHeldItemData != null)
        {
            ApplyLocalHeldVisual(_pendingHeldItemData);
            if (IsSpawned && IsOwner)
            {
                RequestSetHeldItemServerRpc(_pendingHeldItemData.ItemId);
            }
            return;
        }

        // 2. 툴바 슬롯이 선택되지 않았거나 범위를 벗어난 경우
        if (_inventory == null || slotIndex < 0 || slotIndex >= PlayerInventory.TOOLBAR_SIZE)
        {
            ClearHeldVisual();
            if (IsSpawned && IsOwner)
            {
                RequestSetHeldItemServerRpc(string.Empty);
            }
            return;
        }

        ItemData itemData = _inventory.GetHeldItemData();
        string itemId = itemData != null ? itemData.ItemId : string.Empty;

        // 로컬 플레이어 손 메쉬 교체 적용
        if (itemData != null)
        {
            ApplyLocalHeldVisual(itemData);
        }
        else
        {
            ClearHeldVisual();
        }

        if (IsSpawned && IsOwner)
        {
            RequestSetHeldItemServerRpc(itemId);
        }
    }

    private void ApplyLocalHeldVisual(ItemData itemData)
    {
        EnsureComponents();
        EnsureHeldVisualSockets();

        _currentHeldItemData = itemData;

        Mesh mesh = itemData.HeldMesh;
        Material mat = itemData.HeldMaterial;

        // Fallback: HeldMesh나 HeldMaterial이 비어있다면 WorldPrefab에서 탐색
        if (mesh == null && itemData.WorldPrefab != null)
        {
            var mf = itemData.WorldPrefab.GetComponentInChildren<MeshFilter>(true);
            if (mf != null) mesh = mf.sharedMesh;
        }
        if (mat == null && itemData.WorldPrefab != null)
        {
            var mr = itemData.WorldPrefab.GetComponentInChildren<MeshRenderer>(true);
            if (mr != null) mat = mr.sharedMaterial;
        }

        if (mesh != null)
        {
            // 1. 1인칭 뷰모델 (카메라 앞 소켓 - 로컬 화면 표시, 그림자 투영 안 함)
            if (_heldMeshFilter1P != null && _heldMeshRenderer1P != null && _heldVisual1P != null)
            {
                _heldMeshFilter1P.sharedMesh = mesh;
                _heldMeshRenderer1P.sharedMaterial = mat;
                _heldMeshRenderer1P.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _heldMeshRenderer1P.enabled = true;

                _heldVisual1P.transform.localPosition = itemData.HeldLocalPosition;
                _heldVisual1P.transform.localRotation = Quaternion.Euler(itemData.HeldLocalRotation);
                _heldVisual1P.transform.localScale = itemData.HeldLocalScale;
                _heldVisual1P.SetActive(true);
            }

            // 2. 3인칭 월드모델 (오른손 소켓 - 로컬에서는 ShadowsOnly로 전신 그림자 투영 및 왼손 IK 기준 제공)
            if (_heldMeshFilter3P != null && _heldMeshRenderer3P != null && _heldVisual3P != null)
            {
                _heldMeshFilter3P.sharedMesh = mesh;
                _heldMeshRenderer3P.sharedMaterial = mat;
                _heldMeshRenderer3P.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                _heldMeshRenderer3P.enabled = true;

                _heldVisual3P.transform.localPosition = itemData.HeldLocalPosition3P;
                _heldVisual3P.transform.localRotation = Quaternion.Euler(itemData.HeldLocalRotation3P);
                _heldVisual3P.transform.localScale = itemData.HeldLocalScale3P;
                _heldVisual3P.SetActive(true);
            }
        }
        else
        {
            if (_heldVisual1P != null) _heldVisual1P.SetActive(false);
            if (_heldVisual3P != null) _heldVisual3P.SetActive(false);
        }

        if (_interaction != null)
        {
            _interaction.OnHeldItemChanged(_heldVisual1P ?? _heldVisual3P, itemData);
        }

        UpdateAnimatorOverride(itemData);
        NotifyCombatHeldItemChanged(itemData);
    }

    /// <summary>
    /// 원격 클라이언트 화면에서 다른 플레이어의 손에 장착될 메쉬와 머티리얼을 표시합니다.
    /// </summary>
    public void ApplyRemoteHeldItem(string itemId)
    {
        if (IsOwner) return;

        EnsureComponents();
        EnsureHeldVisualSockets();

        if (string.IsNullOrEmpty(itemId))
        {
            ClearHeldVisual();
            return;
        }

        ItemData itemData = ItemDatabase.GetItem(itemId);
        if (itemData == null)
        {
            ClearHeldVisual();
            return;
        }

        Mesh mesh = itemData.HeldMesh;
        Material mat = itemData.HeldMaterial;
        if (mesh == null && itemData.WorldPrefab != null)
        {
            var mf = itemData.WorldPrefab.GetComponentInChildren<MeshFilter>(true);
            if (mf != null) mesh = mf.sharedMesh;
        }
        if (mat == null && itemData.WorldPrefab != null)
        {
            var mr = itemData.WorldPrefab.GetComponentInChildren<MeshRenderer>(true);
            if (mr != null) mat = mr.sharedMaterial;
        }

        if (mesh == null)
        {
            ClearHeldVisual();
            return;
        }

        _currentHeldItemData = itemData;

        // 원격 플레이어 화면에서는 1인칭 뷰모델 비활성화
        if (_heldVisual1P != null)
        {
            _heldVisual1P.SetActive(false);
        }

        // 3인칭 월드모델 활성화 및 표시 (그림자 On)
        if (_heldMeshFilter3P != null && _heldMeshRenderer3P != null && _heldVisual3P != null)
        {
            _heldMeshFilter3P.sharedMesh = mesh;
            _heldMeshRenderer3P.sharedMaterial = mat;
            _heldMeshRenderer3P.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _heldMeshRenderer3P.enabled = true;

            _heldVisual3P.transform.localPosition = itemData.HeldLocalPosition3P;
            _heldVisual3P.transform.localRotation = Quaternion.Euler(itemData.HeldLocalRotation3P);
            _heldVisual3P.transform.localScale = itemData.HeldLocalScale3P;
            _heldVisual3P.SetActive(true);
        }

        UpdateAnimatorOverride(itemData);
        NotifyCombatHeldItemChanged(itemData);
    }

    private void ClearHeldVisual()
    {
        EnsureComponents();
        EnsureHeldVisualSockets();

        _currentHeldItemData = null;

        if (_heldMeshFilter1P != null) _heldMeshFilter1P.sharedMesh = null;
        if (_heldMeshRenderer1P != null) _heldMeshRenderer1P.sharedMaterial = null;
        if (_heldVisual1P != null) _heldVisual1P.SetActive(false);

        if (_heldMeshFilter3P != null) _heldMeshFilter3P.sharedMesh = null;
        if (_heldMeshRenderer3P != null) _heldMeshRenderer3P.sharedMaterial = null;
        if (_heldVisual3P != null) _heldVisual3P.SetActive(false);

        if (_interaction != null)
        {
            _interaction.OnHeldItemChanged(null, null);
        }

        UpdateAnimatorOverride(null);
        NotifyCombatHeldItemChanged(null);
    }

    private void UpdateAnimatorOverride(ItemData itemData)
    {
        EnsureComponents();
        if (_animator == null) return;

        if (_baseRuntimeController == null && _animator.runtimeAnimatorController != null && !(_animator.runtimeAnimatorController is AnimatorOverrideController))
        {
            _baseRuntimeController = _animator.runtimeAnimatorController;
        }

        if (itemData != null && itemData.AnimatorOverride != null)
        {
            if (_animator.runtimeAnimatorController != itemData.AnimatorOverride)
            {
                _animator.runtimeAnimatorController = itemData.AnimatorOverride;
            }
        }
        else
        {
            if (_baseRuntimeController != null && _animator.runtimeAnimatorController != _baseRuntimeController)
            {
                _animator.runtimeAnimatorController = _baseRuntimeController;
            }
        }

        // 스왑 시 이전 액션 애니메이션 즉시 캔슬 (UpperBody 레이어 리셋)
        _animator.ResetTrigger("Fire");
        _animator.ResetTrigger("Reload");
        _animator.ResetTrigger("Attack");

        // 무기 분류 파라미터 업데이트 (0: 맨손, 1: 라이플/총기, 2: 샷건, 3: 근접)
        int weaponType = 0;
        if (itemData != null)
        {
            if (itemData is GunItemData gun)
            {
                weaponType = gun.IsShotgun ? 2 : 1;
            }
            else if (itemData is MeleeItemData || itemData.ActionType == ItemActionType.MeleeWeapon)
            {
                weaponType = 3;
            }
            else
            {
                weaponType = 1;
            }
        }
        _animator.SetInteger("WeaponType", weaponType);
    }

    private void NotifyCombatHeldItemChanged(ItemData itemData)
    {
        if (TryGetComponent<PlayerGunCombat>(out var gunCombat))
        {
            gunCombat.SetEquippedGun(itemData as GunItemData);
        }
        if (TryGetComponent<PlayerMeleeCombat>(out var meleeCombat))
        {
            meleeCombat.SetEquippedMelee(itemData as MeleeItemData);
        }
    }

    public void ClearHeldItem()
    {
        _pendingHeldItemData = null;
        ClearHeldVisual();
    }

    /// <summary>
    /// 바닥에 있는 물리 아이템(PickableItem)을 주워 손에 듭니다 (번호키 입력 대기 상태).
    /// 서버에서 해당 월드 오브젝트를 Despawn시키며, 손에 비주얼이 장착됩니다.
    /// </summary>
    public bool PickupWorldItem(PickableItem worldItem)
    {
        EnsureComponents();

        if (worldItem == null || _inventory == null) return false;

        // 1. 이미 바닥에서 주운 미수납 아이템을 손에 쥐고 있다면 추가로 주울 수 없음
        if (_pendingHeldItemData != null)
        {
            Debug.Log("[PlayerItemHolder] 이미 손에 주운 아이템을 들고 있어 추가로 주울 수 없습니다. 먼저 툴바에 보관하거나 내려놓으세요.");
            return false;
        }

        ItemData itemData = worldItem.ItemData != null
            ? worldItem.ItemData
            : ItemDatabase.FindItemByObject(worldItem.gameObject);

        if (itemData == null)
        {
            Debug.LogWarning($"[PlayerItemHolder] '{worldItem.name}'에 해당하는 ItemData를 찾을 수 없습니다.");
            return false;
        }

        // 2. 만약 툴바 슬롯에서 꺼내 쥐고 있던 아이템이 있다면:
        //    해당 아이템은 툴바 슬롯으로 다시 들어가고(선택 해제), 툴바 하이라이트가 꺼집니다.
        if (_inventory.SelectedToolbarIndex >= 0)
        {
            _inventory.SetSelectedToolbarSlot(-1);
            ClearHeldVisual();
        }

        // 3. 툴바 슬롯에 즉시 넣지 않고, 땅에서 주운 새 아이템을 손에 보관
        _pendingHeldItemData = itemData;
        _currentHeldItemData = itemData;

        // 4. 로컬 손 메쉬 적용
        ApplyLocalHeldVisual(itemData);

        // 5. 원격 플레이어에게 손 아이템 동기화
        if (IsSpawned && IsOwner)
        {
            RequestSetHeldItemServerRpc(itemData.ItemId);
        }

        // 5. 월드 물리 오브젝트 제거
        DespawnWorldItem(worldItem);
        return true;
    }

    private void DespawnWorldItem(PickableItem worldItem)
    {
        var netObj = worldItem.GetComponent<NetworkObject>();
        if (IsSpawned && netObj != null && netObj.IsSpawned)
        {
            RequestDespawnWorldItemServerRpc(netObj.NetworkObjectId);
        }
        else
        {
            if (Application.isPlaying)
            {
                Destroy(worldItem.gameObject);
            }
            else
            {
                DestroyImmediate(worldItem.gameObject);
            }
        }
    }

    /// <summary>
    /// 현재 손에 들고 있는 아이템을 플레이어가 바라보는 방향으로 물리 투척합니다.
    /// 툴바에 등록된 아이템인 경우 툴바에서 제거하고, 미할당 아이템인 경우 미할당 상태를 비우며
    /// 서버에 WorldPrefab 스폰 및 물리 속도를 요청합니다.
    /// </summary>
    public void DropCurrentHeldItem(Vector3 dropPosition, Quaternion dropRotation, Vector3 throwVelocity)
    {
        if (!IsHoldingItem || _currentHeldItemData == null)
        {
            return;
        }

        ItemData heldItem = _currentHeldItemData;

        // 1. 툴바에 이미 할당된 아이템인지, 픽업 후 미할당된 아이템인지에 따른 처리
        if (_pendingHeldItemData != null)
        {
            _pendingHeldItemData = null;
        }
        else
        {
            int currentSlot = _inventory != null ? _inventory.SelectedToolbarIndex : -1;
            if (currentSlot >= 0 && currentSlot < PlayerInventory.TOOLBAR_SIZE && _inventory != null)
            {
                _inventory.ToolbarSlots[currentSlot].Clear();
                _inventory.NotifyInventoryChanged();
                _inventory.SetSelectedToolbarSlot(-1);
            }
        }

        // 2. 손 비우기 (메쉬 비활성화)
        ClearHeldVisual();

        if (IsSpawned && IsOwner)
        {
            RequestSetHeldItemServerRpc(string.Empty);
        }

        // 3. 서버에 월드 오브젝트 스폰 및 물리 투척 요청
        if (IsSpawned)
        {
            RequestDropItemServerRpc(heldItem.ItemId, dropPosition, dropRotation, throwVelocity);
        }
        else
        {
            // 오프라인 / 테스트 환경: 로컬에서 WorldPrefab 생성 및 물리 속도 부여
            GameObject worldPrefab = heldItem.WorldPrefab;
            if (worldPrefab != null)
            {
                var dropped = Instantiate(worldPrefab, dropPosition, dropRotation);
                var pickable = dropped.GetComponent<PickableItem>();
                if (pickable != null)
                {
                    pickable.ItemData = heldItem;
                    pickable.SetInitialDropPhysics(throwVelocity);
                }
                else
                {
                    var rb = dropped.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.linearVelocity = throwVelocity;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 블록 설치 시 현재 손에 쥐고 있는 블록 아이템을 1개 소모합니다.
    /// 툴바 슬롯 아이템이면 해당 슬롯의 수량을 1 차감하고, 수량이 0개가 되면 슬롯을 비웁니다.
    /// 바닥에서 주워 든 미수납(Pending) 아이템이면 즉시 손을 비웁니다.
    /// 잔여 수량이 0이 되면 손 비주얼을 비우고 건설 모드를 즉시 종료합니다.
    /// </summary>
    public bool ConsumeCurrentHeldPlaceableItem()
    {
        if (!IsHoldingItem || _currentHeldItemData == null)
        {
            return false;
        }

        // 1. 바닥에서 주워 든 미수납 아이템인 경우: 즉시 손 비우기
        if (_pendingHeldItemData != null)
        {
            _pendingHeldItemData = null;
            ClearHeldVisual();
            if (IsSpawned && IsOwner)
            {
                RequestSetHeldItemServerRpc(string.Empty);
            }
            if (GridBuildingController.Instance != null)
            {
                GridBuildingController.Instance.StopBuilding();
            }
            return true;
        }

        // 2. 툴바 슬롯에서 꺼내 든 아이템인 경우: 수량 1 차감
        int currentSlot = _inventory != null ? _inventory.SelectedToolbarIndex : -1;
        if (_inventory != null && currentSlot >= 0 && currentSlot < PlayerInventory.TOOLBAR_SIZE)
        {
            var slot = _inventory.ToolbarSlots[currentSlot];
            if (!slot.IsEmpty)
            {
                slot.RemoveQuantity(1);
                _inventory.NotifyInventoryChanged();

                if (slot.IsEmpty)
                {
                    ClearHeldVisual();
                    if (IsSpawned && IsOwner)
                    {
                        RequestSetHeldItemServerRpc(string.Empty);
                    }
                    if (GridBuildingController.Instance != null)
                    {
                        GridBuildingController.Instance.StopBuilding();
                    }
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 손에 든 미할당 아이템을 특정 번호키(toolbarIndex)의 툴바 슬롯에 넣습니다.
    /// 해당 번호 슬롯이 차 있다면 다음 빈 슬롯으로 순환하여 삽입하고,
    /// 모든 슬롯이 꽉 차 있다면 손에 든 아이템을 바닥에 그대로 떨어뜨립니다.
    /// </summary>
    public bool TryPutHeldWorldItemToToolbar(int toolbarIndex)
    {
        EnsureComponents();

        if (_pendingHeldItemData == null || _inventory == null)
        {
            return false;
        }

        ItemData itemToPut = _pendingHeldItemData;
        int targetSlot = -1;

        if (toolbarIndex >= 0 && toolbarIndex < PlayerInventory.TOOLBAR_SIZE)
        {
            if (_inventory.ToolbarSlots[toolbarIndex].IsEmpty)
            {
                targetSlot = toolbarIndex;
            }
            else
            {
                // 누른 슬롯이 차 있다면 다음 비어있는 슬롯 순환 탐색
                for (int offset = 1; offset < PlayerInventory.TOOLBAR_SIZE; offset++)
                {
                    int candidate = (toolbarIndex + offset) % PlayerInventory.TOOLBAR_SIZE;
                    if (_inventory.ToolbarSlots[candidate].IsEmpty)
                    {
                        targetSlot = candidate;
                        break;
                    }
                }
            }
        }
        else
        {
            targetSlot = _inventory.FindEmptyToolbarSlot();
        }

        // 1. 빈 슬롯을 찾은 경우: 툴바 슬롯(수납공간)에 넣고, 손은 비웁니다.
        if (targetSlot != -1)
        {
            _inventory.ToolbarSlots[targetSlot].Set(itemToPut, 1);
            _inventory.NotifyInventoryChanged();

            _pendingHeldItemData = null;
            _currentHeldItemData = null;

            // 손 비우기 (메쉬 비활성화 및 툴바 선택 해제)
            ClearHeldVisual();
            _inventory.SetSelectedToolbarSlot(-1);

            if (IsSpawned && IsOwner)
            {
                RequestSetHeldItemServerRpc(string.Empty);
            }
            return true;
        }

        // 2. 모든 슬롯이 가득 찬 경우: 바닥에 그대로 드롭
        Debug.Log("[PlayerItemHolder] 툴바 슬롯이 모두 가득 차 있어 아이템을 바닥에 떨어뜨립니다.");
        DropPendingHeldItem();
        return true;
    }

    /// <summary>
    /// 툴바에 미할당된 손 아이템을 바닥에 투척합니다.
    /// </summary>
    public void DropPendingHeldItem()
    {
        if (_interaction != null)
        {
            _interaction.DropHeldItem();
        }
        else
        {
            Vector3 lookDir = transform.forward;
            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            Vector3 throwOrigin = eyePos + lookDir * 0.5f;
            Quaternion throwRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 throwVelocity = lookDir * 6.0f + Vector3.up * 1.5f;
            DropCurrentHeldItem(throwOrigin, throwRotation, throwVelocity);
        }
    }

    public void DropCurrentItem(Vector3 dropPosition, Quaternion dropRotation, Vector3 throwVelocity)
    {
        DropCurrentHeldItem(dropPosition, dropRotation, throwVelocity);
    }

    public Transform Get1PHoldTarget()
    {
        if (_firstPersonHoldPoint != null)
        {
            return _firstPersonHoldPoint;
        }

        Transform found = transform.Find("CameraTarget/HoldPoint");
        if (found != null)
        {
            _firstPersonHoldPoint = found;
            return _firstPersonHoldPoint;
        }

        if (_interaction != null && _interaction.HoldPoint != null)
        {
            _firstPersonHoldPoint = _interaction.HoldPoint;
            return _firstPersonHoldPoint;
        }

        found = FindDeepChild(transform, "HoldPoint");
        if (found != null)
        {
            _firstPersonHoldPoint = found;
            return _firstPersonHoldPoint;
        }

        return transform;
    }

    public Transform Get3PHoldTarget()
    {
        if (_thirdPersonHoldPoint != null)
        {
            return _thirdPersonHoldPoint;
        }

        Transform found3P = FindDeepChild(transform, "HoldPoint3P");
        if (found3P != null)
        {
            _thirdPersonHoldPoint = found3P;
            return _thirdPersonHoldPoint;
        }

        Transform rightHand = FindDeepChild(transform, "RightHand");
        if (rightHand != null)
        {
            _thirdPersonHoldPoint = rightHand;
            return _thirdPersonHoldPoint;
        }

        return Get1PHoldTarget();
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    #region RPCs

    [ServerRpc]
    private void RequestDespawnWorldItemServerRpc(ulong networkObjectId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            {
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
            }
        }
    }

    [ServerRpc]
    private void RequestSetHeldItemServerRpc(string itemId)
    {
        _networkHeldItemId.Value = itemId;
        ItemData itemData = string.IsNullOrEmpty(itemId) ? null : ItemDatabase.GetItem(itemId);
        _currentHeldItemData = itemData;
        NotifyCombatHeldItemChanged(itemData);
    }

    [ServerRpc]
    private void RequestDropItemServerRpc(string itemId, Vector3 dropPosition, Quaternion dropRotation, Vector3 throwVelocity)
    {
        ItemData itemData = ItemDatabase.GetItem(itemId);
        if (itemData != null && itemData.WorldPrefab != null)
        {
            GameObject droppedObj = Instantiate(itemData.WorldPrefab, dropPosition, dropRotation);
            var pickable = droppedObj.GetComponent<PickableItem>();
            if (pickable != null)
            {
                pickable.ItemData = itemData;
                pickable.SetInitialDropPhysics(throwVelocity);
            }
            else
            {
                var rb = droppedObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = throwVelocity;
                }
            }

            var netObj = droppedObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true);
            }
        }

        _networkHeldItemId.Value = string.Empty;
    }

    /// <summary>
    /// 클라이언트에서 그리드 상에 건축물 설치를 서버에 요청합니다.
    /// </summary>
    public void RequestPlaceBuilding(string itemId, Vector2Int gridCoord, int rotationAngle)
    {
        if (IsSpawned)
        {
            RequestPlaceBuildingServerRpc(itemId, gridCoord, rotationAngle);
        }
        else
        {
            ItemData itemData = ItemDatabase.GetItem(itemId);
            if (itemData != null && itemData.PlaceableBuildingPrefab != null && WorldGridManager.Instance != null)
            {
                WorldGridManager.Instance.TryPlaceObject(itemData.PlaceableBuildingPrefab, gridCoord, rotationAngle, out _);
            }
        }
    }

    [ServerRpc]
    private void RequestPlaceBuildingServerRpc(string itemId, Vector2Int gridCoord, int rotationAngle)
    {
        ItemData itemData = ItemDatabase.GetItem(itemId);
        if (itemData == null || itemData.PlaceableBuildingPrefab == null)
        {
            return;
        }

        if (WorldGridManager.Instance == null) return;

        if (WorldGridManager.Instance.TryPlaceObject(itemData.PlaceableBuildingPrefab, gridCoord, rotationAngle, out PlaceableObject placedInstance))
        {
            Vector3 spawnPos = placedInstance.transform.position;
            NotifyBuildingPlacedClientRpc(spawnPos);
        }
    }

    [ClientRpc]
    private void NotifyBuildingPlacedClientRpc(Vector3 spawnPos)
    {
        if (Coop.VFX.VfxPoolManager.Instance != null)
        {
            Coop.VFX.VfxPoolManager.Instance.SpawnImpact(Coop.VFX.SurfaceType.Stone, spawnPos + Vector3.up * 0.1f, Vector3.up);
        }
    }

    #endregion
}
