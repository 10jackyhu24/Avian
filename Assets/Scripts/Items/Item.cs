using UnityEngine;

public enum ItemType {
    Empty,
    Bullet
}

[System.Serializable]
public struct ItemData {
    public ItemType itemType;
    public string itemName;
    public Sprite icon;
    public int maxCount;
}
