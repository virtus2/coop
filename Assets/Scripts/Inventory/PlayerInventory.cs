using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 캐릭터의 인벤토리(그리드 X*Y 및 툴바 10*1)를 관리하고,
/// 숫자키 1~0 입력을 통한 툴바 슬롯 선택 및 슬롯 간 아이템 이동/스왑을 처리하는 핵심 컴포넌트입니다.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    public static PlayerInventory LocalInstance { get; private set; }

    public const int TOOLBAR_SIZE = 10;

    [Header("Grid Dimensions")]
    [SerializeField] private int _gridWidth = 5;
    [SerializeField] private int _gridHeight = 4;

    [Header("Starting Items (Test/Debug)")]
    [SerializeField] private List<ItemData> _initialItems = new List<ItemData>();

    [Header("State")]
    [SerializeField] private int _selectedToolbarIndex = 0; // 기본 1번 슬롯(인덱스 0)

    private InventorySlot[] _gridSlots;
    private InventorySlot[] _toolbarSlots;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public int GridTotalSize => _gridWidth * _gridHeight;
    public int SelectedToolbarIndex => _selectedToolbarIndex;

    public IReadOnlyList<InventorySlot> GridSlots
    {
        get
        {
            EnsureInitialized();
            return _gridSlots;
        }
    }

    public IReadOnlyList<InventorySlot> ToolbarSlots
    {
        get
        {
            EnsureInitialized();
            return _toolbarSlots;
        }
    }

    // 인벤토리 상태 변경 이벤트
    public event Action OnInventoryChanged;
    public event Action<int> OnSelectedToolbarSlotChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        LocalInstance = null;
    }

    private PlayerItemHolder _itemHolder;

    private void Awake()
    {
        EnsureInitialized();
        _itemHolder = GetComponent<PlayerItemHolder>();
    }

    public void EnsureInitialized()
    {
        InitializeSlots();
    }

    private void Start()
    {
        // 로컬 플레이어 인스턴스 등록
        if (!IsSpawned || IsOwner)
        {
            LocalInstance = this;
            GiveInitialItems();
            NotifySelectedToolbarChanged();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
            GiveInitialItems();
            NotifySelectedToolbarChanged();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && LocalInstance == this)
        {
            LocalInstance = null;
        }
    }

    public override void OnDestroy()
    {
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }
        base.OnDestroy();
    }

    private void InitializeSlots()
    {
        int totalGrid = GridTotalSize;
        if (_gridSlots == null || _gridSlots.Length != totalGrid)
        {
            _gridSlots = new InventorySlot[totalGrid];
            for (int i = 0; i < totalGrid; i++)
            {
                _gridSlots[i] = new InventorySlot();
            }
        }

        if (_toolbarSlots == null || _toolbarSlots.Length != TOOLBAR_SIZE)
        {
            _toolbarSlots = new InventorySlot[TOOLBAR_SIZE];
            for (int i = 0; i < TOOLBAR_SIZE; i++)
            {
                _toolbarSlots[i] = new InventorySlot();
            }
        }
    }

    private void GiveInitialItems()
    {
        if (_initialItems == null || _initialItems.Count == 0)
        {
            return;
        }

        // 이미 아이템이 있는지 확인
        bool hasAnyItem = false;
        for (int i = 0; i < _toolbarSlots.Length; i++)
        {
            if (!_toolbarSlots[i].IsEmpty)
            {
                hasAnyItem = true;
                break;
            }
        }

        if (!hasAnyItem)
        {
            for (int i = 0; i < _initialItems.Count && i < _toolbarSlots.Length; i++)
            {
                if (_initialItems[i] != null)
                {
                    _toolbarSlots[i].Set(_initialItems[i], 1);
                }
            }
            OnInventoryChanged?.Invoke();
        }
    }

    private void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        // 숫자키 1~9, 0 입력 감지 (1=인덱스0, 2=인덱스1, ..., 9=인덱스8, 0=인덱스9)
        CheckToolbarNumberInput();
    }

    /// <summary>
    /// New Input System의 Keyboard 입력을 검사하여 숫자키 1~0 입력 시 툴바 슬롯을 변경합니다.
    /// 캐릭터가 월드에 존재하지 않거나 입력이 비활성화된 경우 입력을 무시합니다.
    /// </summary>
    private void CheckToolbarNumberInput()
    {
        // 1. 캐릭터 부재 시 툴바 조작 차단
        if (NetworkPlayer.LocalInstance != null && !NetworkPlayer.LocalInstance.HasCharacter)
        {
            return;
        }
        if (PlayerCharacter.LocalInstance == null || !PlayerCharacter.LocalInstance.IsInputEnabled)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // 1번 (인덱스 0)
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(0);
        }
        // 2번 (인덱스 1)
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(1);
        }
        // 3번 (인덱스 2)
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(2);
        }
        // 4번 (인덱스 3)
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(3);
        }
        // 5번 (인덱스 4)
        else if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(4);
        }
        // 6번 (인덱스 5)
        else if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(5);
        }
        // 7번 (인덱스 6)
        else if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(6);
        }
        // 8번 (인덱스 7)
        else if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(7);
        }
        // 9번 (인덱스 8)
        else if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(8);
        }
        // 0번 (인덱스 9)
        else if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
        {
            SelectToolbarSlot(9);
        }
    }

    /// <summary>
    /// 지정된 인덱스의 툴바 슬롯을 선택합니다.
    /// 1. 손에 바닥에서 주운 아이템이 있는 경우: 툴바 슬롯(또는 남은 칸)으로 삽입하고 손을 비웁니다.
    /// 2. 손에 들고 있는 아이템이 없을 때: 해당 번호 아이템을 손에 들고 하이라이트합니다.
    /// 3. 해당 번호 아이템을 손에 든 상태에서 다시 그 번호를 누르면: 손을 비우고 하이라이트를 취소합니다.
    /// </summary>
    public void SelectToolbarSlot(int index)
    {
        // 근접 공격 모션 진행 중에는 핫바 교체 불가 (E-04)
        if (PlayerMeleeCombat.LocalInstance != null && PlayerMeleeCombat.LocalInstance.IsAttacking)
        {
            return;
        }

        if (index < 0 || index >= TOOLBAR_SIZE)
        {
            index = -1;
        }

        // 1. 바닥에서 주워 손에 든 아이템이 있는 경우
        if (_itemHolder == null)
        {
            _itemHolder = GetComponent<PlayerItemHolder>();
            if (_itemHolder == null && NetworkPlayer.LocalInstance != null && NetworkPlayer.LocalInstance.CurrentCharacter != null)
            {
                _itemHolder = NetworkPlayer.LocalInstance.CurrentCharacter.GetComponent<PlayerItemHolder>();
            }
        }

        if (_itemHolder != null && _itemHolder.IsHoldingWorldItem)
        {
            if (_itemHolder.TryPutHeldWorldItemToToolbar(index))
            {
                return;
            }
        }

        // 2. 이미 해당 슬롯을 손에 들고 있는 상태에서 다시 그 번호를 누른 경우:
        // 손에 있던 해당 번호 아이템을 비우고 하이라이트를 취소함
        if (_selectedToolbarIndex == index)
        {
            _selectedToolbarIndex = -1;
            NotifySelectedToolbarChanged();
            return;
        }

        // 3. 해당 번호 슬롯을 손에 들고 하이라이트
        _selectedToolbarIndex = index;
        NotifySelectedToolbarChanged();
    }

    /// <summary>
    /// 특정 ItemData를 지정된 툴바 슬롯에 삽입합니다.
    /// 만약 해당 슬롯에 이미 다른 아이템이 있다면, 기존 아이템을 그리드 인벤토리의 빈 칸으로 이동시킵니다.
    /// </summary>
    public bool PutItemIntoToolbarSlot(int toolbarIndex, ItemData itemData, int quantity = 1)
    {
        if (itemData == null || toolbarIndex < 0 || toolbarIndex >= TOOLBAR_SIZE)
        {
            return false;
        }

        EnsureInitialized();
        var targetSlot = _toolbarSlots[toolbarIndex];

        // 1. 해당 툴바 슬롯이 비어있는 경우
        if (targetSlot.IsEmpty)
        {
            targetSlot.Set(itemData, quantity);
            OnInventoryChanged?.Invoke();
            return true;
        }

        // 2. 같은 아이템이고 스택이 가능한 경우
        if (targetSlot.Item == itemData && targetSlot.Quantity + quantity <= itemData.MaxStackSize)
        {
            targetSlot.AddQuantity(quantity);
            OnInventoryChanged?.Invoke();
            return true;
        }

        // 3. 이미 다른 아이템이 들어있는 경우:
        // 기존 아이템을 그리드 인벤토리의 빈 공간으로 이동 시도
        ItemData existingItem = targetSlot.Item;
        int existingQuantity = targetSlot.Quantity;

        bool addedToGrid = TryAddExistingItemToGrid(existingItem, existingQuantity);
        if (addedToGrid)
        {
            targetSlot.Set(itemData, quantity);
            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    private bool TryAddExistingItemToGrid(ItemData item, int quantity)
    {
        int remaining = quantity;
        remaining = TryStackItem(_gridSlots, item, remaining);
        if (remaining > 0)
        {
            remaining = TryPutEmptySlot(_gridSlots, item, remaining);
        }
        return remaining == 0;
    }

    /// <summary>
    /// 강제로 특정 슬롯을 선택
    /// </summary>
    public void SetSelectedToolbarSlot(int index)
    {
        if (index < 0 || index >= TOOLBAR_SIZE)
        {
            index = -1;
        }

        _selectedToolbarIndex = index;
        NotifySelectedToolbarChanged();
    }

    /// <summary>
    /// 현재 선택된 툴바 슬롯의 아이템을 지정 수량만큼 제거합니다 (땅에 내려놓을 때 호출).
    /// </summary>
    public bool RemoveCurrentHeldItem(int quantity = 1)
    {
        if (_selectedToolbarIndex < 0 || _selectedToolbarIndex >= TOOLBAR_SIZE)
        {
            return false;
        }

        return RemoveItem(new SlotLocation(SlotType.Toolbar, _selectedToolbarIndex), quantity);
    }

    /// <summary>
    /// 인벤토리 내용 변경 이벤트를 외부에 알립니다.
    /// </summary>
    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private void NotifySelectedToolbarChanged()
    {
        OnSelectedToolbarSlotChanged?.Invoke(_selectedToolbarIndex);
    }

    /// <summary>
    /// 현재 선택된 툴바 슬롯의 아이템 데이터를 반환합니다. (없거나 빈 슬롯이면 null)
    /// </summary>
    public ItemData GetHeldItemData()
    {
        if (_selectedToolbarIndex < 0 || _selectedToolbarIndex >= TOOLBAR_SIZE)
        {
            return null;
        }

        var slot = _toolbarSlots[_selectedToolbarIndex];
        return slot != null && !slot.IsEmpty ? slot.Item : null;
    }

    /// <summary>
    /// 빈 툴바 슬롯의 인덱스를 반환합니다. 빈 슬롯이 없으면 -1을 반환합니다.
    /// </summary>
    public int FindEmptyToolbarSlot()
    {
        for (int i = 0; i < _toolbarSlots.Length; i++)
        {
            if (_toolbarSlots[i].IsEmpty)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 위치(그리드 또는 툴바)에 따른 InventorySlot 객체를 반환합니다.
    /// </summary>
    public InventorySlot GetSlot(SlotLocation location)
    {
        if (location.Type == SlotType.Toolbar)
        {
            if (location.Index >= 0 && location.Index < TOOLBAR_SIZE)
            {
                return _toolbarSlots[location.Index];
            }
        }
        else if (location.Type == SlotType.Grid)
        {
            if (location.Index >= 0 && location.Index < GridTotalSize)
            {
                return _gridSlots[location.Index];
            }
        }

        return null;
    }

    /// <summary>
    /// 두 슬롯 간 아이템을 이동하거나 스왑(맞교환)합니다.
    /// 동일한 아이템이고 스택 가능한 경우 합칩니다.
    /// </summary>
    public bool MoveOrSwap(SlotLocation from, SlotLocation to)
    {
        if (from == to)
        {
            return false;
        }

        InventorySlot sourceSlot = GetSlot(from);
        InventorySlot targetSlot = GetSlot(to);

        if (sourceSlot == null || targetSlot == null || sourceSlot.IsEmpty)
        {
            return false;
        }

        // 대상 슬롯이 비어있는 경우: 이동
        if (targetSlot.IsEmpty)
        {
            targetSlot.Set(sourceSlot.Item, sourceSlot.Quantity);
            sourceSlot.Clear();
        }
        // 대상 슬롯에 동일한 아이템이 있고 스택 여유가 있는 경우: 스택 합치기
        else if (targetSlot.Item == sourceSlot.Item && targetSlot.Quantity < targetSlot.Item.MaxStackSize)
        {
            int remaining = targetSlot.AddQuantity(sourceSlot.Quantity);
            if (remaining > 0)
            {
                sourceSlot.Set(sourceSlot.Item, remaining);
            }
            else
            {
                sourceSlot.Clear();
            }
        }
        // 다른 아이템이거나 가득 찬 경우: 맞교환(스왑)
        else
        {
            ItemData tempItem = targetSlot.Item;
            int tempQty = targetSlot.Quantity;

            targetSlot.Set(sourceSlot.Item, sourceSlot.Quantity);
            sourceSlot.Set(tempItem, tempQty);
        }

        OnInventoryChanged?.Invoke();

        // 만약 이동/스왑된 슬롯 중 현재 선택된 툴바 슬롯이 포함되어 있다면 손 아이템 갱신
        if ((from.Type == SlotType.Toolbar && from.Index == _selectedToolbarIndex) ||
            (to.Type == SlotType.Toolbar && to.Index == _selectedToolbarIndex))
        {
            NotifySelectedToolbarChanged();
        }

        return true;
    }

    /// <summary>
    /// 인벤토리에 새 아이템을 추가합니다. (툴바 빈 슬롯 우선 또는 그리드 우선)
    /// </summary>
    public bool AddItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        int remaining = quantity;

        // 1. 기존 동일 아이템 스택 채우기 (툴바 먼저 -> 그리드)
        remaining = TryStackItem(_toolbarSlots, item, remaining);
        if (remaining > 0)
        {
            remaining = TryStackItem(_gridSlots, item, remaining);
        }

        // 2. 빈 슬롯에 넣기 (툴바 먼저 -> 그리드)
        if (remaining > 0)
        {
            remaining = TryPutEmptySlot(_toolbarSlots, item, remaining);
        }
        if (remaining > 0)
        {
            remaining = TryPutEmptySlot(_gridSlots, item, remaining);
        }

        OnInventoryChanged?.Invoke();

        // 툴바 슬롯이 변경되었을 가능성이 있으므로 손 아이템 통지
        NotifySelectedToolbarChanged();

        return remaining < quantity;
    }

    private int TryStackItem(InventorySlot[] slots, ItemData item, int amount)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (amount <= 0) break;

            var slot = slots[i];
            if (!slot.IsEmpty && slot.Item == item && slot.Quantity < item.MaxStackSize)
            {
                amount = slot.AddQuantity(amount);
            }
        }
        return amount;
    }

    private int TryPutEmptySlot(InventorySlot[] slots, ItemData item, int amount)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (amount <= 0) break;

            var slot = slots[i];
            if (slot.IsEmpty)
            {
                int toPut = Mathf.Min(amount, item.MaxStackSize);
                slot.Set(item, toPut);
                amount -= toPut;
            }
        }
        return amount;
    }

    /// <summary>
    /// 슬롯의 아이템을 지정 수량만큼 제거합니다.
    /// </summary>
    public bool RemoveItem(SlotLocation location, int quantity = 1)
    {
        var slot = GetSlot(location);
        if (slot == null || slot.IsEmpty)
        {
            return false;
        }

        slot.RemoveQuantity(quantity);
        OnInventoryChanged?.Invoke();

        if (location.Type == SlotType.Toolbar && location.Index == _selectedToolbarIndex)
        {
            NotifySelectedToolbarChanged();
        }

        return true;
    }

    /// <summary>
    /// 현재 선택된 툴바 슬롯을 반환합니다.
    /// </summary>
    public InventorySlot GetSelectedToolbarSlot()
    {
        EnsureInitialized();
        if (_selectedToolbarIndex >= 0 && _selectedToolbarIndex < _toolbarSlots.Length)
        {
            return _toolbarSlots[_selectedToolbarIndex];
        }
        return null;
    }

    /// <summary>
    /// 인벤토리(그리드 + 툴바) 전체에서 특정 아이템 ID를 가진 총 탄약 수량을 조회합니다.
    /// </summary>
    public int GetTotalAmmoCount(string ammoItemId)
    {
        if (string.IsNullOrEmpty(ammoItemId)) return 0;
        EnsureInitialized();

        int total = 0;
        foreach (var slot in _gridSlots)
        {
            if (!slot.IsEmpty && slot.Item != null && slot.Item.ItemId == ammoItemId)
            {
                total += slot.Quantity;
            }
        }
        foreach (var slot in _toolbarSlots)
        {
            if (!slot.IsEmpty && slot.Item != null && slot.Item.ItemId == ammoItemId)
            {
                total += slot.Quantity;
            }
        }
        return total;
    }

    /// <summary>
    /// 특정 탄약 아이템을 지정 수량만큼 인벤토리에서 소모합니다.
    /// 실제로 소모된 탄약 수량을 반환합니다.
    /// </summary>
    public int ConsumeAmmo(string ammoItemId, int amountToConsume)
    {
        if (string.IsNullOrEmpty(ammoItemId) || amountToConsume <= 0) return 0;
        EnsureInitialized();

        int remainingToConsume = amountToConsume;

        // 1. 그리드 슬롯 먼저 소모
        for (int i = 0; i < _gridSlots.Length; i++)
        {
            var slot = _gridSlots[i];
            if (!slot.IsEmpty && slot.Item != null && slot.Item.ItemId == ammoItemId)
            {
                int removed = slot.RemoveQuantity(remainingToConsume);
                remainingToConsume -= removed;
                if (remainingToConsume <= 0) break;
            }
        }

        // 2. 툴바 슬롯 소모
        if (remainingToConsume > 0)
        {
            for (int i = 0; i < _toolbarSlots.Length; i++)
            {
                var slot = _toolbarSlots[i];
                if (!slot.IsEmpty && slot.Item != null && slot.Item.ItemId == ammoItemId)
                {
                    int removed = slot.RemoveQuantity(remainingToConsume);
                    remainingToConsume -= removed;
                    if (remainingToConsume <= 0) break;
                }
            }
        }

        int totalConsumed = amountToConsume - remainingToConsume;
        if (totalConsumed > 0)
        {
            OnInventoryChanged?.Invoke();
        }

        return totalConsumed;
    }

    /// <summary>
    /// 초기 테스트 아이템 목록을 인스펙터나 에디터 스크립트에서 설정할 수 있도록 지원
    /// </summary>
    public void SetInitialItems(List<ItemData> items)
    {
        _initialItems = items;
    }
}
