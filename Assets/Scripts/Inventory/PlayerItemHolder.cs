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

    // 플레이어 캐릭터에 상시 부착된 단일 비주얼 소켓 오브젝트
    private GameObject _heldVisualGO;
    private MeshFilter _heldMeshFilter;
    private MeshRenderer _heldMeshRenderer;
    private ItemData _currentHeldItemData;
    private ItemData _pendingHeldItemData; // 바닥에서 주워 아직 툴바에 등록되지 않은 손 아이템

    // 네트워크 동기화용: 현재 손에 든 아이템의 고유 ID (비어있으면 string.Empty)
    private readonly NetworkVariable<FixedString64Bytes> _networkHeldItemId = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public GameObject CurrentHeldInstance => (_currentHeldItemData != null && _heldVisualGO != null && _heldVisualGO.activeSelf) ? _heldVisualGO : null;
    public ItemData CurrentHeldItemData => _currentHeldItemData;
    public ItemData PendingHeldItemData => _pendingHeldItemData;
    public bool IsHoldingItem => _currentHeldItemData != null;
    public bool IsHoldingWorldItem => _pendingHeldItemData != null;

    private void Awake()
    {
        EnsureComponents();
        EnsureHeldVisualSocket();
    }

    private void EnsureComponents()
    {
        if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
        if (_interaction == null) _interaction = GetComponent<PlayerInteraction>();
    }

    private void EnsureHeldVisualSocket()
    {
        if (_heldVisualGO != null && _heldMeshFilter != null && _heldMeshRenderer != null)
        {
            return;
        }

        Transform holdTarget = GetHoldTarget();
        Transform found = holdTarget.Find("HeldItemVisual");
        if (found != null)
        {
            _heldVisualGO = found.gameObject;
        }
        else
        {
            _heldVisualGO = new GameObject("HeldItemVisual");
            _heldVisualGO.transform.SetParent(holdTarget, false);
            _heldVisualGO.transform.localPosition = Vector3.zero;
            _heldVisualGO.transform.localRotation = Quaternion.identity;
            _heldVisualGO.transform.localScale = Vector3.one;
        }

        _heldMeshFilter = _heldVisualGO.GetComponent<MeshFilter>();
        if (_heldMeshFilter == null)
        {
            _heldMeshFilter = _heldVisualGO.AddComponent<MeshFilter>();
        }

        _heldMeshRenderer = _heldVisualGO.GetComponent<MeshRenderer>();
        if (_heldMeshRenderer == null)
        {
            _heldMeshRenderer = _heldVisualGO.AddComponent<MeshRenderer>();
        }

        // 초기에는 비활성화
        _heldVisualGO.SetActive(false);
    }

    private void Start()
    {
        EnsureComponents();
        EnsureHeldVisualSocket();

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
        EnsureHeldVisualSocket();

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
    /// 단일 소켓의 메쉬와 머티리얼을 교체하므로 프리팹 인스턴스화/파괴가 발생하지 않습니다.
    /// </summary>
    public void UpdateHeldItem(int slotIndex)
    {
        EnsureComponents();
        EnsureHeldVisualSocket();

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
        EnsureHeldVisualSocket();

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
            _heldMeshFilter.sharedMesh = mesh;
            _heldMeshRenderer.sharedMaterial = mat;
            _heldMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 1인칭 손은 그림자 끔
            _heldVisualGO.transform.localPosition = itemData.HeldLocalPosition;
            _heldVisualGO.transform.localRotation = Quaternion.Euler(itemData.HeldLocalRotation);
            _heldVisualGO.transform.localScale = itemData.HeldLocalScale;
            _heldVisualGO.SetActive(true);
        }
        else
        {
            _heldVisualGO.SetActive(false);
        }

        if (_interaction != null)
        {
            _interaction.OnHeldItemChanged(_heldVisualGO, itemData);
        }
    }

    /// <summary>
    /// 원격 클라이언트 화면에서 다른 플레이어의 손에 장착될 메쉬와 머티리얼을 표시합니다.
    /// </summary>
    public void ApplyRemoteHeldItem(string itemId)
    {
        if (IsOwner) return;

        EnsureComponents();
        EnsureHeldVisualSocket();

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
        _heldMeshFilter.sharedMesh = mesh;
        _heldMeshRenderer.sharedMaterial = mat;
        _heldMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // 3인칭은 그림자 켬
        _heldVisualGO.transform.localPosition = itemData.HeldLocalPosition;
        _heldVisualGO.transform.localRotation = Quaternion.Euler(itemData.HeldLocalRotation);
        _heldVisualGO.transform.localScale = itemData.HeldLocalScale;
        _heldVisualGO.SetActive(true);
    }

    private void ClearHeldVisual()
    {
        EnsureComponents();
        EnsureHeldVisualSocket();

        _currentHeldItemData = null;

        if (_heldMeshFilter != null) _heldMeshFilter.sharedMesh = null;
        if (_heldMeshRenderer != null) _heldMeshRenderer.sharedMaterial = null;
        if (_heldVisualGO != null) _heldVisualGO.SetActive(false);

        if (_interaction != null)
        {
            _interaction.OnHeldItemChanged(null, null);
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

    private Transform GetHoldTarget()
    {
        if (_interaction != null && _interaction.HoldPoint != null)
        {
            return _interaction.HoldPoint;
        }

        Transform found = transform.Find("CameraTarget/HoldPoint");
        if (found != null)
        {
            return found;
        }

        found = transform.Find("HoldPoint");
        if (found != null)
        {
            return found;
        }

        return transform;
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
        SyncHeldItemClientRpc(itemId);
    }

    [ClientRpc]
    private void SyncHeldItemClientRpc(string itemId)
    {
        if (!IsOwner)
        {
            ApplyRemoteHeldItem(itemId);
        }
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
        SyncHeldItemClientRpc(string.Empty);
    }

    #endregion
}
