using System.Collections.Generic;
using UnityEngine;

public static class ItemManager {
    public static Dictionary<string, ItemData> items = ItemLoader.LoadAllItemData();

    public static GameObject AddItemInScene(string item_name, Vector3 position, Quaternion rotation) {
        if (string.IsNullOrWhiteSpace(item_name)) {
            Debug.LogWarning("[ItemManager] AddItemInScene failed because item_name is empty.");
            return null;
        }

        if (items == null || !items.TryGetValue(item_name, out ItemData itemData) || !itemData.IsValid) {
            Debug.LogWarning($"[ItemManager] AddItemInScene failed because '{item_name}' was not found.");
            return null;
        }

        if (itemData.itemObject.gameObject == null) {
            Debug.LogWarning($"[ItemManager] AddItemInScene failed because '{item_name}' has no scene prefab assigned.");
            return null;
        }

        return Object.Instantiate(itemData.itemObject.gameObject, position, rotation);
    }
}
