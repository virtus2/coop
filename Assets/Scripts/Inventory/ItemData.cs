using UnityEngine;

/// <summary>
/// 아이템 사용(클릭 / 홀드) 시 실행될 액션 분류입니다.
/// </summary>
public enum ItemActionType
{
    None = 0,
    Gun = 1,
    Medkit = 2,
    ChargedWeapon = 3,
    Placeable = 4,
    MeleeWeapon = 5
}

/// <summary>
/// 인벤토리에 보관될 수 있는 아이템의 기본 정보와 뷰모델/월드 프리팹 참조를 정의하는 ScriptableObject입니다.
/// 단일 손 소켓 방식에서 HeldMesh와 HeldMaterial을 제공하여 런타임 인스턴스화 없이 비주얼을 교체합니다.
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

    [Header("Held Visual Settings (단일 소켓 Mesh/Material 교체)")]
    [Tooltip("손에 들었을 때 표시할 3D 메쉬")]
    [SerializeField] private Mesh _heldMesh;
    [Tooltip("손에 들었을 때 적용할 머티리얼")]
    [SerializeField] private Material _heldMaterial;
    [Tooltip("손 소켓(HoldPoint) 기준 로컬 위치 오프셋")]
    [SerializeField] private Vector3 _heldLocalPosition = Vector3.zero;
    [Tooltip("손 소켓(HoldPoint) 기준 로컬 회전 오프셋 (오일러 각)")]
    [SerializeField] private Vector3 _heldLocalRotation = Vector3.zero;
    [Tooltip("손 소켓(HoldPoint) 기준 로컬 스케일")]
    [SerializeField] private Vector3 _heldLocalScale = Vector3.one;

    [Header("Action Settings")]
    [Tooltip("손에 들었을 때 좌클릭/홀드로 실행할 액션 유형")]
    [SerializeField] private ItemActionType _actionType = ItemActionType.None;
    [Tooltip("블록 설치 아이템인 경우 설치할 건축물 프리팹")]
    [SerializeField] private PlaceableObject _placeableBuildingPrefab;

    [Header("Prefabs")]
    [Tooltip("월드에 버려졌을 때 스폰될 물리 프리팹 (PickableItem)")]
    [SerializeField] private GameObject _worldPrefab;
    [Tooltip("하위 호환성 지원용 (단일 소켓 대신 복합 프리팹이 필요한 특수 케이스용)")]
    [SerializeField] private GameObject _holdPrefab;

    public string ItemId => _itemId;
    public string ItemName => _itemName;
    public Sprite Icon => _icon;
    public string Description => _description;
    public int MaxStackSize => Mathf.Max(1, _maxStackSize);

    public Mesh HeldMesh => _heldMesh;
    public Material HeldMaterial => _heldMaterial;
    public Vector3 HeldLocalPosition => _heldLocalPosition;
    public Vector3 HeldLocalRotation => _heldLocalRotation;
    public Vector3 HeldLocalScale => _heldLocalScale == Vector3.zero ? Vector3.one : _heldLocalScale;
    public ItemActionType ActionType => _actionType;
    public PlaceableObject PlaceableBuildingPrefab => _placeableBuildingPrefab;

    public GameObject WorldPrefab => _worldPrefab;
    public GameObject HoldPrefab => _holdPrefab;

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

    public void SetHeldVisual(Mesh mesh, Material mat, Vector3 pos = default, Vector3 rot = default, Vector3 scale = default)
    {
        _heldMesh = mesh;
        _heldMaterial = mat;
        _heldLocalPosition = pos;
        _heldLocalRotation = rot;
        _heldLocalScale = scale == default ? Vector3.one : scale;
    }

    public void SetActionType(ItemActionType actionType, PlaceableObject placeable = null)
    {
        _actionType = actionType;
        _placeableBuildingPrefab = placeable;
    }
}
