using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프로젝트의 모든 ItemData 에셋을 ID 기반으로 빠르게 검색할 수 있는 중앙 정적 데이터베이스입니다.
/// Resources/ItemData 폴더의 에셋들을 로드하여 캐싱합니다.
/// </summary>
public static class ItemDatabase
{
    private static Dictionary<string, ItemData> _itemsById;
    private static bool _isLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _itemsById = null;
        _isLoaded = false;
    }

    public static void EnsureLoaded()
    {
        if (_isLoaded && _itemsById != null)
        {
            return;
        }

        _itemsById = new Dictionary<string, ItemData>();
        var loadedItems = Resources.LoadAll<ItemData>("ItemData");

        foreach (var item in loadedItems)
        {
            if (item != null && !string.IsNullOrEmpty(item.ItemId))
            {
                if (!_itemsById.ContainsKey(item.ItemId))
                {
                    _itemsById.Add(item.ItemId, item);
                }
                else
                {
                    Debug.LogWarning($"[ItemDatabase] 중복된 ItemId 감지: '{item.ItemId}' ({item.name})");
                }
            }
        }

        _isLoaded = true;
    }

    /// <summary>
    /// ItemId로 ItemData 에셋을 반환합니다. 없으면 null을 반환합니다.
    /// </summary>
    public static ItemData GetItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        EnsureLoaded();

        if (_itemsById != null && _itemsById.TryGetValue(itemId, out ItemData data))
        {
            return data;
        }

        return null;
    }

    /// <summary>
    /// 에디터 또는 수동 등록용
    /// </summary>
    public static void RegisterItem(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.ItemId))
        {
            return;
        }

        if (_itemsById == null)
        {
            _itemsById = new Dictionary<string, ItemData>();
        }

        _itemsById[item.ItemId] = item;
    }

    /// <summary>
    /// 지정된 GameObject 인스턴스 또는 프리팹을 분석하여 해당하는 ItemData를 찾습니다.
    /// PickableItem의 ItemData 참조를 우선 확인하고, 프리팹 이름 및 원본 프리팹 참조를 대조합니다.
    /// </summary>
    public static ItemData FindItemByObject(GameObject obj)
    {
        if (obj == null)
        {
            return null;
        }

        EnsureLoaded();

        // 1. PickableItem 컴포넌트에 직접 ItemData가 지정되어 있는지 확인
        var pickable = obj.GetComponent<PickableItem>();
        if (pickable != null && pickable.ItemData != null)
        {
            return pickable.ItemData;
        }

        // 2. 등록된 ItemData들의 WorldPrefab 및 HoldPrefab과 이름 및 원본 대조
        string objName = obj.name;
        if (objName.EndsWith("(Clone)"))
        {
            objName = objName.Substring(0, objName.Length - 7).Trim();
        }

        foreach (var kvp in _itemsById)
        {
            var data = kvp.Value;
            if (data == null) continue;

            if (data.WorldPrefab != null)
            {
                string prefabName = data.WorldPrefab.name;
                if (string.Equals(prefabName, objName, System.StringComparison.OrdinalIgnoreCase) ||
                    objName.StartsWith(prefabName, System.StringComparison.OrdinalIgnoreCase) ||
                    objName.IndexOf(prefabName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    prefabName.IndexOf(objName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (pickable != null) pickable.ItemData = data;
                    return data;
                }
            }

            if (!string.IsNullOrEmpty(data.ItemId) && objName.IndexOf(data.ItemId, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (pickable != null) pickable.ItemData = data;
                return data;
            }

            if (!string.IsNullOrEmpty(data.name) && objName.IndexOf(data.name, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (pickable != null) pickable.ItemData = data;
                return data;
            }
        }

        return null;
    }
}
