using System;
using UnityEngine;

/// <summary>
/// 인벤토리 내의 슬롯 위치 유형 (그리드 vs 툴바)
/// </summary>
public enum SlotType
{
    Grid,
    Toolbar
}

/// <summary>
/// 인벤토리 슬롯의 위치 식별자
/// </summary>
[Serializable]
public struct SlotLocation : IEquatable<SlotLocation>
{
    public SlotType Type;
    public int Index;

    public SlotLocation(SlotType type, int index)
    {
        Type = type;
        Index = index;
    }

    public bool Equals(SlotLocation other)
    {
        return Type == other.Type && Index == other.Index;
    }

    public override bool Equals(object obj)
    {
        return obj is SlotLocation other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Type, Index);
    }

    public override string ToString()
    {
        return $"{Type}[{Index}]";
    }

    public static bool operator ==(SlotLocation left, SlotLocation right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SlotLocation left, SlotLocation right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// 인벤토리 슬롯 한 칸의 상태를 나타내는 데이터 클래스입니다.
/// </summary>
[Serializable]
public class InventorySlot
{
    [SerializeField] private ItemData _item;
    [SerializeField] private int _quantity;
    [SerializeField] private int _currentAmmo = -1;

    public ItemData Item => _item;
    public int Quantity => _quantity;
    public int CurrentAmmo
    {
        get => _currentAmmo;
        set => _currentAmmo = value;
    }
    public bool IsEmpty => _item == null || _quantity <= 0;

    public InventorySlot()
    {
        _item = null;
        _quantity = 0;
        _currentAmmo = -1;
    }

    public InventorySlot(ItemData item, int quantity, int currentAmmo = -1)
    {
        Set(item, quantity, currentAmmo);
    }

    public void Set(ItemData item, int quantity, int currentAmmo = -1)
    {
        if (item == null || quantity <= 0)
        {
            Clear();
            return;
        }

        _item = item;
        _quantity = Mathf.Clamp(quantity, 1, item.MaxStackSize);

        if (currentAmmo >= 0)
        {
            _currentAmmo = currentAmmo;
        }
        else if (item is GunItemData gunData)
        {
            // 총기 아이템이 새로 인벤토리에 들어올 때 탄창 완충 상태로 초기화
            _currentAmmo = gunData.MagazineCapacity;
        }
        else
        {
            _currentAmmo = -1;
        }
    }

    public void CopyFrom(InventorySlot other)
    {
        if (other == null || other.IsEmpty)
        {
            Clear();
            return;
        }

        _item = other.Item;
        _quantity = other.Quantity;
        _currentAmmo = other.CurrentAmmo;
    }

    public void Clear()
    {
        _item = null;
        _quantity = 0;
        _currentAmmo = -1;
    }

    public int AddQuantity(int amount)
    {
        if (_item == null)
        {
            return amount;
        }

        int max = _item.MaxStackSize;
        int space = max - _quantity;
        int toAdd = Mathf.Min(space, amount);

        _quantity += toAdd;
        return amount - toAdd; // 남아있는 양 반환
    }

    public int RemoveQuantity(int amount)
    {
        if (_item == null || _quantity <= 0)
        {
            return 0;
        }

        int toRemove = Mathf.Min(_quantity, amount);
        _quantity -= toRemove;

        if (_quantity <= 0)
        {
            Clear();
        }

        return toRemove;
    }
}
