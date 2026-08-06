using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Net.Mail;

public class UI_BagManager : MonoBehaviour {
    [Header("Capacity Settings")]
    [SerializeField] private int maxCapacity = 100;
    [SerializeField] private TextMeshProUGUI totalCapacityText;

    [Header("UI Spawn Setup")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private UI_BagItemSlot itemSlotPrefab;

    // Data source for the inventory
    public List<BagSlotData> bagDataList = new List<BagSlotData>();

    private static readonly AttachmentType[] DefaultWeaponAttachmentTypes = {
        AttachmentType.AttackSpeed,
        AttachmentType.DamageBonus,
        AttachmentType.CriticalRate
    };

    private void Awake() {
        Dictionary<string, ItemData> items = ItemManager.items;
        AddItem(items["Gun"]);
        AddItem(items["Gun2"]);
        AddItem(items["Gun3"]);
        SetAttachmentItem(1, AttachmentType.AttackSpeed, items["Gun3"]);
    }

    // Refresh the entire bag UI and total capacity
    public void RefreshBagUI(List<BagSlotData> currentBagData) {
        bagDataList = currentBagData;
        RebuildSlots();
    }

    public void AddItem(ItemObject itemObject, int count = 1) {
        AddItem(ItemData.FromItemObject(itemObject), count);
    }

    public void AddItem(ItemData itemData, int count = 1, params AttachmentType[] requiredAttachmentTypes) {
        if (itemData.itemObject == null || itemData.itemType == ItemType.Empty) {
            Debug.LogWarning("[UI_BagManager] AddItem failed because the item data is empty.");
            return;
        }

        count = Mathf.Clamp(count, 1, Mathf.Max(1, itemData.itemObject.maxCount));

        BagSlotData slotData = new BagSlotData {
            itemData = itemData,
            count = count
        };

        if (slotData.IsWeapon) {
            if (requiredAttachmentTypes != null && requiredAttachmentTypes.Length > 0) {
                slotData.EnsureAttachmentSlots(requiredAttachmentTypes);
            } else {
                slotData.EnsureAttachmentSlots(DefaultWeaponAttachmentTypes);
            }
        }

        int itemWeight = slotData.GetTotalWeight();
        if (!CanAddItem(itemWeight)) {
            Debug.LogWarning($"[UI_BagManager] AddItem blocked. '{itemData.itemObject.itemName}' would exceed capacity.");
            return;
        }

        bagDataList.Add(slotData);
        UpdateCapacityLabel();
        SpawnSlot(slotData);
    }

    public bool SetAttachmentItem(int index, AttachmentType attachmentType, ItemData itemData) {
        if (bagDataList == null || index < 0 || index >= bagDataList.Count) {
            return false;
        }

        BagSlotData slotData = bagDataList[index];
        if (slotData == null || slotData.attachments == null) {
            return false;
        }

        for (int i = 0; i < slotData.attachments.Count; i++) {
            AttachmentSlotData attachmentSlot = slotData.attachments[i];
            if (attachmentSlot == null || attachmentSlot.requiredType != attachmentType) {
                continue;
            }

            if (!attachmentSlot.CanAccept(itemData)) {
                continue;
            }

            attachmentSlot.attachedItem = itemData;
            RebuildSlots();
            return true;
        }

        return false;
    }

    public void MoveSlot(int fromIndex, int toIndex) {
        if (fromIndex < 0 || fromIndex >= bagDataList.Count || 
            toIndex < 0 || toIndex > bagDataList.Count) {
            return;
        }

        if (fromIndex == toIndex) return;

        BagSlotData slot = bagDataList[fromIndex];
        bagDataList.RemoveAt(fromIndex);
        
        // Adjust toIndex if we removed from before it
        if (fromIndex < toIndex) {
            toIndex--;
        }

        bagDataList.Insert(Mathf.Clamp(toIndex, 0, bagDataList.Count), slot);
        RebuildSlots();
    }

    public void RemoveSlot(int index) {
        if (bagDataList == null || index < 0 || index >= bagDataList.Count) {
            return;
        }

        bagDataList.RemoveAt(index);
        RebuildSlots();
    }

    public void SetSlotAttachment() {
        
    }

    // Sum weight of all slots
    public int CalculateTotalWeight() {
        if (bagDataList == null) {
            return 0;
        }

        int totalWeight = 0;
        foreach (var slotData in bagDataList) {
            if (slotData == null) {
                continue;
            }
            totalWeight += slotData.GetTotalWeight();
        }
        return totalWeight;
    }

    // Check if adding an item exceeds maximum capacity
    public bool CanAddItem(int itemWeight) {
        return CalculateTotalWeight() + itemWeight <= maxCapacity;
    }

    public void ClearBag() {
        bagDataList.Clear();
        UpdateCapacityLabel();
    }

    public void RebuildSlots() {
        ClearSpawnedSlots();

        for (int i = 0; i < bagDataList.Count; i++) {
            SpawnSlot(bagDataList[i]);
        }

        UpdateCapacityLabel();
    }

    private void UpdateCapacityLabel() {
        if (totalCapacityText != null) {
            totalCapacityText.text = $"{CalculateTotalWeight()} / {maxCapacity}";
        }
    }

    private void SpawnSlot(BagSlotData slotData) {
        if (slotData == null) {
            return;
        }

        if (itemSlotPrefab == null) {
            Debug.LogWarning("[UI_BagManager] itemSlotPrefab is not assigned.");
            return;
        }

        GameObject spawnedObject = PrefabSpawn.SpawnInit(itemSlotPrefab.gameObject, contentRoot);
        if (spawnedObject == null) {
            return;
        }

        UI_BagItemSlot slotView = spawnedObject.GetComponent<UI_BagItemSlot>();
        if (slotView == null) {
            Debug.LogWarning("[UI_BagManager] Spawned prefab does not have UI_BagItemSlot.");
            return;
        }

        slotView.UpdateSlotUI(slotData);
    }

    private void ClearSpawnedSlots() {
        ObjectControll.ClearChildren(contentRoot, "Bag_Item_Slot(Clone)");
    }

    public int FindSlotIndex(UI_BagItemSlot slot) {
        if (slot == null) {
            return -1;
        }

        return ObjectControll.GetIndex(slot);
    }
}
