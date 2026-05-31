using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryItemData
{
    public string id;
    public string name;
    public string description;
    public string type;
    public bool usable;
    public bool consumable;
    public int cultivationGain;
    public int hpGain;
    public int attackBonus;
    public int defenseBonus;
    public int maxHpBonus;
    public bool sellable;
    public int sellPrice;
}

[Serializable]
public class InventoryItemDataList
{
    public List<InventoryItemData> items = new List<InventoryItemData>();
}

/// <summary>
/// 新背包系统专用物品数据库。
/// 避免和旧 LightweightDataManagers.cs 里的 ItemData / ItemDataList 重名。
/// </summary>
public static class InventoryItemDatabase
{
    private const string ItemDataResourcePath = "Data/items";
    private static readonly Dictionary<string, InventoryItemData> itemById = new Dictionary<string, InventoryItemData>();
    private static bool loaded;

    public static void Reload()
    {
        loaded = false;
        itemById.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        itemById.Clear();

        TextAsset jsonAsset = Resources.Load<TextAsset>(ItemDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到物品数据：Resources/" + ItemDataResourcePath + ".json");
            return;
        }

        InventoryItemDataList dataList = JsonUtility.FromJson<InventoryItemDataList>(jsonAsset.text);
        if (dataList == null || dataList.items == null)
        {
            Debug.LogError("物品数据格式不正确：" + ItemDataResourcePath);
            return;
        }

        foreach (InventoryItemData item in dataList.items)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            if (string.IsNullOrEmpty(item.name)) item.name = item.id;
            if (string.IsNullOrEmpty(item.description)) item.description = "暂无描述。";
            if (string.IsNullOrEmpty(item.type)) item.type = "tool";
            itemById[item.id] = item;
        }
    }

    public static InventoryItemData GetItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        EnsureLoaded();
        InventoryItemData item;
        return itemById.TryGetValue(itemId, out item) ? item : null;
    }

    public static string GetItemName(string itemId)
    {
        InventoryItemData item = GetItem(itemId);
        return item != null ? item.name : itemId;
    }

    public static string GetTypeName(string type)
    {
        if (type == "tool") return "工具";
        if (type == "clue") return "线索";
        if (type == "scroll") return "卷轴";
        if (type == "consumable") return "消耗品";
        if (type == "weapon") return "武器";
        return string.IsNullOrEmpty(type) ? "未知" : type;
    }
}
