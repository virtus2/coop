using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 창(Tab 키 토글) 및 화면 하단 툴바(HUD)를 관리하고,
/// 마우스 클릭/드래그를 통한 슬롯 간 아이템 이동 및 스왑을 제어하는 UI 컨트롤러입니다.
/// </summary>
public class InventoryUIController : MonoBehaviour
{
    public static InventoryUIController Instance { get; private set; }

    [Header("Window & Panels")]
    [SerializeField] private GameObject _inventoryWindow;
    [SerializeField] private GameObject _toolbarPanel; // 화면 하단 상시 HUD 툴바
    [SerializeField] private Button _closeButton;

    [Header("Containers")]
    [SerializeField] private Transform _gridSlotsParent; // 인벤토리 창 내부 그리드 슬롯 부모
    [SerializeField] private Transform _windowToolbarSlotsParent; // 인벤토리 창 내부 툴바 슬롯 부모
    [SerializeField] private Transform _hudToolbarSlotsParent; // 화면 하단 HUD 툴바 슬롯 부모

    [Header("Drag & Drop Ghost")]
    [SerializeField] private GameObject _dragGhostObject;
    [SerializeField] private Image _dragGhostImage;

    [Header("Prefab References (Optional for Auto-spawn)")]
    [SerializeField] private GameObject _slotPrefab;

    private readonly List<InventorySlotUI> _gridSlotUIList = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> _windowToolbarSlotUIList = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> _hudToolbarSlotUIList = new List<InventorySlotUI>();

    private bool _isOpen = false;
    private InventorySlotUI _selectedSourceSlot = null; // 클릭-클릭 이동용 선택 슬롯
    private InventorySlotUI _draggedSlot = null; // 드래그 앤 드롭용 소스 슬롯
    private Canvas _parentCanvas;

    public bool IsOpen => _isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
        _parentCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(CloseInventory);
        }

        if (_dragGhostObject != null)
        {
            _dragGhostObject.SetActive(false);
        }

        // 초기 상태: 인벤토리 창 닫힘, HUD 툴바 활성
        if (_inventoryWindow != null)
        {
            _inventoryWindow.SetActive(false);
        }
        _isOpen = false;

        // 슬롯 수집 및 초기화
        CollectExistingSlots();

        // 로컬 플레이어 인벤토리 구독
        TrySubscribeToPlayerInventory();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeFromPlayerInventory();
    }

    private void Update()
    {
        // 로컬 플레이어 인벤토리가 늦게 스폰된 경우 대비 자동 구독
        if (PlayerInventory.LocalInstance != null && !IsSubscribedToInventory)
        {
            TrySubscribeToPlayerInventory();
        }

        // Tab 키를 통한 인벤토리 열기/닫기 토글
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }

        // 클릭 선택 상태(고스트)가 활성화되어 있을 때 마우스 위치 추종
        if (_selectedSourceSlot != null && _dragGhostObject != null && _dragGhostObject.activeSelf)
        {
            UpdateGhostPosition(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
        }
    }

    #region Inventory Subscriptions

    private bool _isSubscribed = false;
    private bool IsSubscribedToInventory => _isSubscribed;

    private void TrySubscribeToPlayerInventory()
    {
        if (PlayerInventory.LocalInstance != null && !_isSubscribed)
        {
            PlayerInventory.LocalInstance.OnInventoryChanged += RefreshAllSlots;
            PlayerInventory.LocalInstance.OnSelectedToolbarSlotChanged += HandleSelectedToolbarChanged;
            _isSubscribed = true;

            // 그리드 크기에 맞춰 슬롯 생성/확장
            EnsureGridSlotsCount(PlayerInventory.LocalInstance.GridTotalSize);
            RefreshAllSlots();
            HandleSelectedToolbarChanged(PlayerInventory.LocalInstance.SelectedToolbarIndex);
        }
    }

    private void UnsubscribeFromPlayerInventory()
    {
        if (PlayerInventory.LocalInstance != null && _isSubscribed)
        {
            PlayerInventory.LocalInstance.OnInventoryChanged -= RefreshAllSlots;
            PlayerInventory.LocalInstance.OnSelectedToolbarSlotChanged -= HandleSelectedToolbarChanged;
            _isSubscribed = false;
        }
    }

    #endregion

    #region Slot Initialization & Management

    public void CollectExistingSlots()
    {
        _hudToolbarSlotUIList.Clear();
        if (_hudToolbarSlotsParent != null)
        {
            var slots = _hudToolbarSlotsParent.GetComponentsInChildren<InventorySlotUI>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Setup(SlotType.Toolbar, i);
                _hudToolbarSlotUIList.Add(slots[i]);
            }
        }

        _windowToolbarSlotUIList.Clear();
        if (_windowToolbarSlotsParent != null)
        {
            var slots = _windowToolbarSlotsParent.GetComponentsInChildren<InventorySlotUI>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Setup(SlotType.Toolbar, i);
                _windowToolbarSlotUIList.Add(slots[i]);
            }
        }

        _gridSlotUIList.Clear();
        if (_gridSlotsParent != null)
        {
            var slots = _gridSlotsParent.GetComponentsInChildren<InventorySlotUI>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Setup(SlotType.Grid, i);
                _gridSlotUIList.Add(slots[i]);
            }
        }
    }

    public void EnsureGridSlotsCount(int count)
    {
        if (_gridSlotsParent == null || _slotPrefab == null)
        {
            return;
        }

        while (_gridSlotUIList.Count < count)
        {
            int nextIndex = _gridSlotUIList.Count;
            var go = Instantiate(_slotPrefab, _gridSlotsParent);
            go.name = $"GridSlot_{nextIndex}";
            var slotUI = go.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                slotUI = go.AddComponent<InventorySlotUI>();
            }
            slotUI.Setup(SlotType.Grid, nextIndex);
            _gridSlotUIList.Add(slotUI);
        }
    }

    #endregion

    #region Toggle & State

    public void ToggleInventory()
    {
        if (_isOpen)
        {
            CloseInventory();
        }
        else
        {
            OpenInventory();
        }
    }

    public void OpenInventory()
    {
        Debug.Log("[InventoryUIController] OpenInventory called!");
        _isOpen = true;

        if (_inventoryWindow != null)
        {
            _inventoryWindow.SetActive(true);
        }

        // 마우스 커서 잠금 해제 및 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 로컬 플레이어 시점 회전 및 이동 입력 일시 중지
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.SetInputEnabled(false);
        }

        ClearSelectionAndGhost();
        RefreshAllSlots();
    }

    public void CloseInventory()
    {
        Debug.Log("[InventoryUIController] CloseInventory called! Stack: " + System.Environment.StackTrace);
        _isOpen = false;

        ClearSelectionAndGhost();

        if (_inventoryWindow != null)
        {
            _inventoryWindow.SetActive(false);
        }

        // 마우스 커서 다시 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 로컬 플레이어 조작 재개
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.SetInputEnabled(true);
        }

        RefreshAllSlots();
    }

    #endregion

    #region Refresh UI

    public void RefreshAllSlots()
    {
        var inventory = PlayerInventory.LocalInstance;
        if (inventory == null)
        {
            return;
        }

        // 1. 그리드 슬롯 갱신
        for (int i = 0; i < _gridSlotUIList.Count && i < inventory.GridSlots.Count; i++)
        {
            _gridSlotUIList[i].UpdateDisplay(inventory.GridSlots[i]);
            _gridSlotUIList[i].SetSelected(_selectedSourceSlot == _gridSlotUIList[i]);
        }

        // 2. HUD 툴바 슬롯 갱신
        for (int i = 0; i < _hudToolbarSlotUIList.Count && i < inventory.ToolbarSlots.Count; i++)
        {
            _hudToolbarSlotUIList[i].UpdateDisplay(inventory.ToolbarSlots[i]);
            bool isCurrentEquipped = inventory.SelectedToolbarIndex == i;
            bool isClickSelected = _selectedSourceSlot == _hudToolbarSlotUIList[i];
            _hudToolbarSlotUIList[i].SetSelected(isCurrentEquipped || isClickSelected);
        }

        // 3. 인벤토리 창 툴바 슬롯 갱신
        for (int i = 0; i < _windowToolbarSlotUIList.Count && i < inventory.ToolbarSlots.Count; i++)
        {
            _windowToolbarSlotUIList[i].UpdateDisplay(inventory.ToolbarSlots[i]);
            bool isCurrentEquipped = inventory.SelectedToolbarIndex == i;
            bool isClickSelected = _selectedSourceSlot == _windowToolbarSlotUIList[i];
            _windowToolbarSlotUIList[i].SetSelected(isCurrentEquipped || isClickSelected);
        }
    }

    private void HandleSelectedToolbarChanged(int selectedIndex)
    {
        // HUD 툴바 및 인벤토리 창 툴바 하이라이트 동기화
        for (int i = 0; i < _hudToolbarSlotUIList.Count; i++)
        {
            bool isEquipped = selectedIndex == i;
            bool isClickSelected = _selectedSourceSlot == _hudToolbarSlotUIList[i];
            _hudToolbarSlotUIList[i].SetSelected(isEquipped || isClickSelected);
        }

        for (int i = 0; i < _windowToolbarSlotUIList.Count; i++)
        {
            bool isEquipped = selectedIndex == i;
            bool isClickSelected = _selectedSourceSlot == _windowToolbarSlotUIList[i];
            _windowToolbarSlotUIList[i].SetSelected(isEquipped || isClickSelected);
        }
    }

    #endregion

    #region Interaction Handling (Click & Drag)

    /// <summary>
    /// 슬롯 클릭 시 처리 (닫혀있을 때는 툴바 슬롯 선택, 열려있을 때는 클릭-클릭 이동)
    /// </summary>
    public void HandleSlotClicked(InventorySlotUI slotUI, PointerEventData eventData)
    {
        var inventory = PlayerInventory.LocalInstance;
        if (inventory == null)
        {
            return;
        }

        // 1. 인벤토리 창이 닫혀있는 일반 플레이 상태:
        // HUD 툴바를 클릭하면 해당 번호 슬롯을 손에 듬
        if (!_isOpen)
        {
            if (slotUI.Location.Type == SlotType.Toolbar)
            {
                inventory.SelectToolbarSlot(slotUI.Location.Index);
            }
            return;
        }

        // 2. 인벤토리 창이 열려있는 상태: 클릭-클릭(선택 후 이동) 지원
        if (_selectedSourceSlot == null)
        {
            // 선택된 소스가 없는 경우: 비어있지 않은 슬롯을 소스로 선택
            if (!slotUI.IsEmpty)
            {
                _selectedSourceSlot = slotUI;
                slotUI.SetSelected(true);
                ShowGhost(slotUI.CurrentSlotData.Item.Icon, eventData.position);
            }
        }
        else
        {
            // 이미 소스가 선택된 상태에서 클릭한 경우:
            if (_selectedSourceSlot == slotUI)
            {
                // 동일 슬롯 재클릭 시 선택 취소
                ClearSelectionAndGhost();
                RefreshAllSlots();
            }
            else
            {
                // 다른 슬롯 클릭 시 이동 또는 스왑 실행!
                inventory.MoveOrSwap(_selectedSourceSlot.Location, slotUI.Location);
                ClearSelectionAndGhost();
                RefreshAllSlots();
            }
        }
    }

    public void HandleBeginDrag(InventorySlotUI slotUI, PointerEventData eventData)
    {
        if (!_isOpen || slotUI.IsEmpty)
        {
            return;
        }

        ClearSelectionAndGhost();

        _draggedSlot = slotUI;
        ShowGhost(slotUI.CurrentSlotData.Item.Icon, eventData.position);
    }

    public void HandleDrag(PointerEventData eventData)
    {
        if (_dragGhostObject != null && _dragGhostObject.activeSelf)
        {
            UpdateGhostPosition(eventData.position);
        }
    }

    public void HandleEndDrag(PointerEventData eventData)
    {
        _draggedSlot = null;
        if (_selectedSourceSlot == null)
        {
            HideGhost();
        }
    }

    public void HandleDrop(InventorySlotUI targetSlotUI)
    {
        if (!_isOpen)
        {
            return;
        }

        var inventory = PlayerInventory.LocalInstance;
        if (inventory == null)
        {
            return;
        }

        // 드래그 중인 슬롯이 있는 경우 드롭 처리
        if (_draggedSlot != null && _draggedSlot != targetSlotUI)
        {
            inventory.MoveOrSwap(_draggedSlot.Location, targetSlotUI.Location);
            _draggedSlot = null;
            ClearSelectionAndGhost();
            RefreshAllSlots();
        }
    }

    private void ShowGhost(Sprite icon, Vector2 screenPos)
    {
        if (_dragGhostObject != null)
        {
            if (_dragGhostImage != null)
            {
                _dragGhostImage.sprite = icon;
                _dragGhostImage.color = icon != null ? new Color(1f, 1f, 1f, 0.8f) : new Color(0.8f, 0.8f, 0.8f, 0.4f);
            }
            _dragGhostObject.SetActive(true);
            UpdateGhostPosition(screenPos);
        }
    }

    private void UpdateGhostPosition(Vector2 screenPos)
    {
        if (_dragGhostObject != null)
        {
            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentCanvas.transform as RectTransform,
                    screenPos,
                    _parentCanvas.worldCamera,
                    out Vector2 localPoint
                );
                _dragGhostObject.transform.localPosition = localPoint;
            }
            else
            {
                _dragGhostObject.transform.position = screenPos;
            }
        }
    }

    private void HideGhost()
    {
        if (_dragGhostObject != null)
        {
            _dragGhostObject.SetActive(false);
        }
    }

    private void ClearSelectionAndGhost()
    {
        if (_selectedSourceSlot != null)
        {
            _selectedSourceSlot = null;
        }
        _draggedSlot = null;
        HideGhost();
    }

    #endregion
}
