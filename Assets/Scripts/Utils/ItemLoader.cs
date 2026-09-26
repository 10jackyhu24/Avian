using System.Collections.Generic;
using UnityEngine;

public static class ItemLoader {
    private const string ITEMS_RESOURCE_PATH = "Items";

    private static List<ItemObject> LoadAllItemObjects() {
        ItemObject[] loadedArray = Resources.LoadAll<ItemObject>(ITEMS_RESOURCE_PATH);
        
        if (loadedArray == null || loadedArray.Length == 0) {
            Debug.LogWarning($"[ItemLoader] No ItemObjects found at path: Resources/{ITEMS_RESOURCE_PATH}");
            return new List<ItemObject>();
        }

        return new List<ItemObject>(loadedArray);
    }

    public static Dictionary<string, ItemData> LoadAllItemData() {
        return BuildItemLookup(useStableId: false);
    }

    public static Dictionary<string, ItemData> LoadAllItemDataById() {
        return BuildItemLookup(useStableId: true);
    }

    private static Dictionary<string, ItemData> BuildItemLookup(bool useStableId) {
        List<ItemObject> objects = LoadAllItemObjects();
        Dictionary<string, ItemData> dataDict = new Dictionary<string, ItemData>();

        foreach (var itemObject in objects) {
            if (itemObject == null) continue;

            string key = useStableId ? itemObject.itemId : itemObject.itemName;
            string keyType = useStableId ? "item ID" : "item name";

            if (string.IsNullOrWhiteSpace(key)) {
                Debug.LogError($"[ItemLoader] ItemObject '{itemObject.name}' has no {keyType} and was skipped.", itemObject);
                continue;
            }

            if (dataDict.ContainsKey(key)) {
                Debug.LogError($"[ItemLoader] Duplicate {keyType} '{key}' on ItemObject '{itemObject.name}'. The duplicate was skipped.", itemObject);
                continue;
            }

            dataDict.Add(key, ItemData.FromItemObject(itemObject));
        }

        return dataDict;
    }
}
