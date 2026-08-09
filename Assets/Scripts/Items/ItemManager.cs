using System.Collections.Generic;

public static class ItemManager {
    public static Dictionary<string, ItemData> items = ItemLoader.LoadAllItemData();
}