using Unity.Netcode;
using UnityEngine;

/// <summary>
/// PlayerInventory의 툴바 슬롯 선택 이벤트(OnSelectedToolbarSlotChanged)를 수신하여,
/// 선택된 아이템의 모델을 플레이어의 HoldPoint에 장착/해제하고 PlayerInteraction과 연동하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class PlayerItemHolder : NetworkBehaviour
{
    private PlayerInventory _inventory;
    private PlayerInteraction _interaction;
    private GameObject _currentHeldInstance;
    private PickableItem _currentPickable;

    public GameObject CurrentHeldInstance => _currentHeldInstance;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _interaction = GetComponent<PlayerInteraction>();
    }

    private void Start()
    {
        if (!IsSpawned || IsOwner)
        {
            _inventory.OnSelectedToolbarSlotChanged += HandleSelectedSlotChanged;
            UpdateHeldItem(_inventory.SelectedToolbarIndex);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            _inventory.OnSelectedToolbarSlotChanged += HandleSelectedSlotChanged;
            UpdateHeldItem(_inventory.SelectedToolbarIndex);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && _inventory != null)
        {
            _inventory.OnSelectedToolbarSlotChanged -= HandleSelectedSlotChanged;
        }
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

    private void HandleSelectedSlotChanged(int slotIndex)
    {
        UpdateHeldItem(slotIndex);
    }

    /// <summary>
    /// 현재 선택된 툴바 슬롯 인덱스에 따라 손에 든 아이템을 갱신합니다.
    /// </summary>
    public void UpdateHeldItem(int slotIndex)
    {
        ClearHeldItem();

        if (slotIndex < 0 || slotIndex >= PlayerInventory.TOOLBAR_SIZE)
        {
            return;
        }

        ItemData itemData = _inventory.GetHeldItemData();
        if (itemData == null || itemData.HoldPrefab == null)
        {
            return;
        }

        Transform holdTarget = null;
        if (_interaction != null && _interaction.HoldPoint != null)
        {
            holdTarget = _interaction.HoldPoint;
        }
        else
        {
            Transform found = transform.Find("CameraTarget/HoldPoint");
            if (found != null)
            {
                holdTarget = found;
            }
            else
            {
                holdTarget = transform;
            }
        }

        // 새 아이템 인스턴스 생성
        _currentHeldInstance = Instantiate(itemData.HoldPrefab, holdTarget.position, holdTarget.rotation);
        _currentHeldInstance.name = $"Held_{itemData.ItemName}";

        // PickableItem 컴포넌트가 있는 경우 상호작용 시스템과 직접 연동
        _currentPickable = _currentHeldInstance.GetComponent<PickableItem>();
        if (_currentPickable != null && _interaction != null)
        {
            _currentPickable.Pickup(_interaction);
        }
        else
        {
            // 단순 장착 모델인 경우 트랜스폼을 HoldTarget에 부착
            _currentHeldInstance.transform.SetParent(holdTarget, true);
            _currentHeldInstance.transform.localPosition = Vector3.zero;
            _currentHeldInstance.transform.localRotation = Quaternion.identity;

            // 콜라이더 비활성화
            var colliders = _currentHeldInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            var rb = _currentHeldInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }

    /// <summary>
    /// 현재 손에 들고 있는 아이템 인스턴스를 파괴하고 상태를 정리합니다.
    /// </summary>
    public void ClearHeldItem()
    {
        if (_currentPickable != null && _interaction != null)
        {
            _interaction.OnItemDropped(_currentPickable);
            _currentPickable = null;
        }

        if (_currentHeldInstance != null)
        {
            Destroy(_currentHeldInstance);
            _currentHeldInstance = null;
        }
    }
}
