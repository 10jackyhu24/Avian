using System.Collections.Generic;
using UnityEngine;

public enum ItemType {
    Empty,
    Weapon,
    AttackSpeed,
    DamageBonus,
    CriticalRate
}

// Types of attachment slots for weapons
public enum AttachmentType {
    None,
    AttackSpeed,
    DamageBonus,
    CriticalRate
}

[CreateAssetMenu(fileName = "NewItemObject", menuName = "Inventory/Item Object")]
public class ItemObject : ScriptableObject {
    public string itemName;
    public Sprite icon;
    public ItemType itemType;
    public ItemEffect itemEffect;
    public bool isWeapon;
    public int maxCount = 1;
    public int baseWeight = 1;

    public bool IsWeapon => isWeapon || itemType == ItemType.Weapon;
    public bool IsStackable => maxCount > 1 && !IsWeapon;
}

[System.Serializable]
public struct ItemData {
    public ItemType itemType;
    public ItemObject itemObject;
    public AttachmentType attachmentType; // What slot type this attachment corresponds to

    public static ItemData FromItemObject(ItemObject itemObject) {
        return new ItemData {
            itemType = itemObject != null ? itemObject.itemType : ItemType.Empty,
            itemObject = itemObject,
            attachmentType = AttachmentType.None
        };
    }

    public static ItemData Empty => new ItemData {
        itemType = ItemType.Empty,
        itemObject = null,
        attachmentType = AttachmentType.None
    };

    public bool IsValid => itemObject != null && itemType != ItemType.Empty;
    public bool IsWeapon => itemObject != null && itemObject.IsWeapon;
    // public bool IsAttachment => itemObject != null && itemType == ItemType.Attachment;
}

[System.Serializable]
public class AttachmentSlotData {
    public AttachmentType requiredType;
    public ItemData attachedItem; // Null if slot is empty

    public bool IsEmpty => attachedItem.itemObject == null || attachedItem.itemType == ItemType.Empty;

    public bool CanAccept(ItemData incomingItem) {
        if (incomingItem.itemObject == null) return false;
        if (requiredType == AttachmentType.None) return false;
        return incomingItem.attachmentType == requiredType;
    }

    public void Clear() {
        attachedItem = ItemData.Empty;
    }

    // Calculate total weight contributed by this attachment
    public int GetWeight() {
        if (attachedItem.itemObject == null) return 0;
        return attachedItem.itemObject.baseWeight;
    }
}

[System.Serializable]
public class BagSlotData {
    public ItemData itemData;
    public int count;
    public List<AttachmentSlotData> attachments = new List<AttachmentSlotData>();

    public bool IsWeapon => itemData.itemObject != null && itemData.itemObject.IsWeapon;
    public bool IsEmpty => itemData.itemObject == null || itemData.itemType == ItemType.Empty || count <= 0;
    public int MaxCount => itemData.itemObject != null ? Mathf.Max(1, itemData.itemObject.maxCount) : 1;

    public void EnsureAttachmentSlots(params AttachmentType[] requiredTypes) {
        if (requiredTypes == null) return;

        while (attachments.Count < requiredTypes.Length) {
            int index = attachments.Count;
            attachments.Add(new AttachmentSlotData {
                requiredType = requiredTypes[index],
                attachedItem = ItemData.Empty
            });
        }
    }

    // Calculate total weight of this slot (Weapon + All attachments OR item baseWeight * count)
    public int GetTotalWeight() {
        if (IsEmpty || itemData.itemObject == null) return 0;

        if (IsWeapon) {
            int total = itemData.itemObject.baseWeight;
            foreach (var slot in attachments) {
                total += slot.GetWeight();
            }
            return total;
        }

        // For stackable consumable items, total weight is count * baseWeight
        return itemData.itemObject.baseWeight * Mathf.Max(0, count);
    }

    public int GetDisplayCount() {
        return Mathf.Max(0, count);
    }
}
