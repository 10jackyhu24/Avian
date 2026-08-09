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
        List<ItemObject> objects = LoadAllItemObjects();
        Dictionary<string, ItemData> dataDict = new Dictionary<string, ItemData>();

        foreach (var itemObject in objects) {
            if (itemObject == null) continue;
            dataDict.Add(itemObject.itemName, ItemData.FromItemObject(itemObject));
        }

        return dataDict;
    }
}