using UnityEngine;
using System.Collections.Generic;

public enum BagType {
    Small,
    Medium,
    Large
}

public struct BagItem {
    public ItemType itemType;
    public int count;

    public bool IsEmpty => itemType == ItemType.Empty || count <= 0;

    public static BagItem CreateEmpty() {
        return new BagItem {
            itemType = ItemType.Empty,
            count = 0
        };
    }

    public static BagItem CreateItem(ItemType type, int c) {
        return new BagItem {
            itemType = type,
            count = c
        };
    }
}

public class Bag : MonoBehaviour {
    public List<BagItem> bagItems = new List<BagItem>();

    private Dictionary<BagType, int> bagSizes = new Dictionary<BagType, int>() {
        { BagType.Small, 3 },
        { BagType.Medium, 6 },
        { BagType.Large, 10 }
    };

    public void BagInit() {
        bagItems.Clear();
        for (int i = 0; i < bagSizes[BagType.Small]; i++) {
            bagItems.Add(BagItem.CreateEmpty());
        }
    }
}