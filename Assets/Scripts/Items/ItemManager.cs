using System.Collections.Generic;
using UnityEngine;

public static class ItemManager {
    public static Dictionary<string, ItemData> items = ItemLoader.LoadAllItemData();

    public static GameObject AddItemInScene(string item_name, Vector3 position, Quaternion rotation, int count = 1) {
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

        GameObject spawnedObject = Object.Instantiate(itemData.itemObject.gameObject, position, rotation);
        Pickupable pickupable = spawnedObject.GetComponent<Pickupable>();
        if (pickupable != null) {
            pickupable.SetCount(count);
        }

        return spawnedObject;
    }
}
