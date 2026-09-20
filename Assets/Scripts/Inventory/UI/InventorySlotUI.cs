using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 또는 툴바의 개별 슬롯 UI를 표현하고,
/// 클릭 및 드래그 앤 드롭 입력을 감지하여 InventoryUIController에 전달하는 컴포넌트입니다.
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Text _quantityText;
    [SerializeField] private Text _keyNumberText;
    [SerializeField] private Image _highlightOutline;

    [Header("Slot Identity")]
    [SerializeField] private SlotLocation _location;

    private InventorySlot _currentSlotData;
    private bool _isSelected;

    public SlotLocation Location => _location;
    public InventorySlot CurrentSlotData => _currentSlotData;
    public bool IsEmpty => _currentSlotData == null || _currentSlotData.IsEmpty;

    public void Setup(SlotType type, int index)
    {
        _location = new SlotLocation(type, index);

        if (_keyNumberText != null)
        {
            if (type == SlotType.Toolbar)
            {
                // 1, 2, ..., 9, 0 번호 표시
                int displayNum = (index + 1) % 10;
                _keyNumberText.text = displayNum.ToString();
                _keyNumberText.gameObject.SetActive(true);
            }
            else
            {
                _keyNumberText.gameObject.SetActive(false);
            }
        }

        SetSelected(false);
        UpdateDisplay(null);
    }

    /// <summary>
    /// 슬롯의 아이템 데이터와 수량에 따라 UI를 갱신합니다.
    /// </summary>
    public void UpdateDisplay(InventorySlot slotData)
    {
        _currentSlotData = slotData;

        if (slotData == null || slotData.IsEmpty)
        {
            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(false);
                _iconImage.sprite = null;
            }

            if (_quantityText != null)
            {
                _quantityText.text = "";
                _quantityText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = slotData.Item.Icon;
                _iconImage.color = slotData.Item.Icon != null ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.5f);
                _iconImage.gameObject.SetActive(true);
            }

            if (_quantityText != null)
            {
                if (slotData.Quantity > 1)
                {
                    _quantityText.text = slotData.Quantity.ToString();
                    _quantityText.gameObject.SetActive(true);
                }
                else
                {
                    _quantityText.text = "";
                    _quantityText.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 현재 슬롯이 선택된 상태(툴바 활성 슬롯 또는 이동 대상 선택)인지 표시
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        if (_highlightOutline != null)
        {
            _highlightOutline.gameObject.SetActive(isSelected);
        }
    }

    #region Pointer & Drag Events

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InventoryUIController.Instance != null)
        {
            InventoryUIController.Instance.HandleSlotClicked(this, eventData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (InventoryUIController.Instance != null && !IsEmpty)
        {
            InventoryUIController.Instance.HandleBeginDrag(this, eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (InventoryUIController.Instance != null)
        {
            InventoryUIController.Instance.HandleDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (InventoryUIController.Instance != null)
        {
            InventoryUIController.Instance.HandleEndDrag(eventData);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (InventoryUIController.Instance != null)
        {
            InventoryUIController.Instance.HandleDrop(this);
        }
    }

    #endregion
}
