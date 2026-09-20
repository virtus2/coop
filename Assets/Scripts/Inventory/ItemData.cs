using UnityEngine;

/// <summary>
/// 인벤토리에 보관될 수 있는 아이템의 기본 정보와 참조를 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private string _itemId;
    [SerializeField] private string _itemName;
    [SerializeField] private Sprite _icon;
    [TextArea(2, 4)]
    [SerializeField] private string _description;

    [Header("Stack Settings")]
    [SerializeField] private int _maxStackSize = 99;

    [Header("Prefabs")]
    [Tooltip("손에 들었을 때 HoldPoint에 부착될 프리팹 (IFireable, IUsable, PickableItem 등이 포함될 수 있음)")]
    [SerializeField] private GameObject _holdPrefab;
    [Tooltip("월드에 버려졌을 때 스폰될 프리팹 (PickableItem)")]
    [SerializeField] private GameObject _worldPrefab;

    public string ItemId => _itemId;
    public string ItemName => _itemName;
    public Sprite Icon => _icon;
    public string Description => _description;
    public int MaxStackSize => Mathf.Max(1, _maxStackSize);
    public GameObject HoldPrefab => _holdPrefab;
    public GameObject WorldPrefab => _worldPrefab;

    public void Initialize(string id, string name, Sprite icon, string desc, int maxStack, GameObject holdPrefab, GameObject worldPrefab)
    {
        _itemId = id;
        _itemName = name;
        _icon = icon;
        _description = desc;
        _maxStackSize = maxStack;
        _holdPrefab = holdPrefab;
        _worldPrefab = worldPrefab;
    }
}
