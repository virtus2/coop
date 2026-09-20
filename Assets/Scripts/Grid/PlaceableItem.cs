using UnityEngine;

/// <summary>
/// 플레이어가 손에 들었을 때 그리드 건설 모드를 활성화하고,
/// 우클릭 시 지정된 PlaceableObject를 그리드에 설치할 수 있도록 하는 블록 아이템 컴포넌트입니다.
/// PickableItem과 함께 부착되어 동작합니다.
/// </summary>
[DisallowMultipleComponent]
public class PlaceableItem : MonoBehaviour
{
    [Header("Building Target")]
    [Tooltip("그리드에 실제로 설치될 건축물 프리팹")]
    [SerializeField] private PlaceableObject _buildingPrefab;

    [Header("Inventory / Amount")]
    [Tooltip("설치 가능한 블록 수량")]
    [SerializeField] private int _amount = 1;

    [Tooltip("설치 시 수량을 소모할지 여부")]
    [SerializeField] private bool _consumeOnPlace = true;

    private PickableItem _pickableItem;

    public PlaceableObject BuildingPrefab => _buildingPrefab;
    public int Amount => _amount;
    public bool ConsumeOnPlace => _consumeOnPlace;
    public PickableItem Pickable => _pickableItem;

    private void Awake()
    {
        _pickableItem = GetComponent<PickableItem>();
    }

    /// <summary>
    /// 블록을 1개 소모합니다. 소모 후 수량이 0 이하가 되면 true를 반환합니다.
    /// </summary>
    public bool ConsumeOne()
    {
        if (!_consumeOnPlace)
        {
            return false;
        }

        _amount--;
        return _amount <= 0;
    }

    /// <summary>
    /// 수량을 직접 추가하거나 설정합니다.
    /// </summary>
    public void SetAmount(int amount)
    {
        _amount = Mathf.Max(0, amount);
    }
}
