using System.Collections.Generic;
using UnityEngine;

public static class ItemManager {
    // Kept for compatibility with the current bag code, which uses display names.
    public static Dictionary<string, ItemData> items = ItemLoader.LoadAllItemData();
    public static Dictionary<string, ItemData> itemsById = ItemLoader.LoadAllItemDataById();

    public static bool TryGetItemById(string itemId, out ItemData itemData) {
        if (string.IsNullOrWhiteSpace(itemId)) {
            itemData = ItemData.Empty;
            return false;
        }

        return itemsById.TryGetValue(itemId, out itemData);
    }

    public static GameObject AddItemInScene(BagSlotData bagSlotData, Vector3 position, Quaternion rotation) {
        if (bagSlotData == null || bagSlotData.IsEmpty || !bagSlotData.itemData.IsValid) {
            Debug.LogWarning("[ItemManager] AddItemInScene failed because bag slot data is empty.");
            return null;
        }

        if (bagSlotData.itemData.itemObject.gameObject == null) {
            Debug.LogWarning($"[ItemManager] AddItemInScene failed because '{bagSlotData.itemData.itemObject.itemName}' has no scene prefab assigned.");
            return null;
        }

        GameObject spawnedObject = Object.Instantiate(bagSlotData.itemData.itemObject.gameObject, position, rotation);
        Pickupable pickupable = spawnedObject.GetComponent<Pickupable>();
        if (pickupable != null) {
            pickupable.SetItemData(bagSlotData.itemData);
            pickupable.SetCount(bagSlotData.count);
            pickupable.SetAttachment(bagSlotData.attachments);
        }

        return spawnedObject;
    }

    public static bool ClearAttachmentItemInScene(Pickupable pickupable, AttachmentType attachmentType) {
        List<AttachmentSlotData> attachments = pickupable.Attachments;
        foreach (var attachmentSlot in attachments) {
            if (attachmentSlot != null && attachmentSlot.requiredType == attachmentType) {
                attachmentSlot.Clear();
                return true;
            }
        }
        return false;
    }
}
