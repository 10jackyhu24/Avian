using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Utils;

public class UI_BagManager : MonoBehaviour {
    [Header("Capacity Settings")]
    [SerializeField] private int maxCapacity = 100;
    [SerializeField] private TextMeshProUGUI totalCapacityText; // Top black text ("80/100")

    [Header("UI Spawn Setup")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private UI_BagItemSlot itemSlotPrefab;

    [Header("Runtime Slots")]
    [SerializeField] private List<UI_BagItemSlot> itemSlots = new List<UI_BagItemSlot>();

    // Data source for the inventory
    private List<BagSlotData> bagDataList = new List<BagSlotData>();

    private static readonly AttachmentType[] DefaultWeaponAttachmentTypes = {
        AttachmentType.AttackSpeed,
        AttachmentType.DamageBonus,
        AttachmentType.CriticalRate
    };

    private void Awake() {
        Dictionary<string, ItemData> items = ItemLoader.LoadAllItemData();
        AddItem(items["Gun"]);
        AddItem(items["Gun2"]);
    }

    // Refresh the entire bag UI and total capacity
    public void RefreshBagUI(List<BagSlotData> currentBagData) {
        bagDataList = currentBagData;
        RebuildSlots();
    }

    public UI_BagItemSlot AddItem(ItemObject itemObject, int count = 1) {
        return AddItem(ItemData.FromItemObject(itemObject), count);
    }

    public UI_BagItemSlot AddItem(ItemData itemData, int count = 1, params AttachmentType[] requiredAttachmentTypes) {
        if (itemData.itemObject == null || itemData.itemType == ItemType.Empty) {
            Debug.LogWarning("[UI_BagManager] AddItem failed because the item data is empty.");
            return null;
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
            return null;
        }

        bagDataList.Add(slotData);
        UpdateCapacityLabel();
        return SpawnSlot(slotData);
    }

    /// <summary>
    /// Inserts an item at a specific index in the bag
    /// </summary>
    public UI_BagItemSlot InsertItem(ItemData itemData, int count = 1, int insertIndex = 0, params AttachmentType[] requiredAttachmentTypes) {
        if (itemData.itemObject == null || itemData.itemType == ItemType.Empty) {
            Debug.LogWarning("[UI_BagManager] InsertItem failed because the item data is empty.");
            return null;
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
            Debug.LogWarning($"[UI_BagManager] InsertItem blocked. '{itemData.itemObject.itemName}' would exceed capacity.");
            return null;
        }

        insertIndex = Mathf.Clamp(insertIndex, 0, bagDataList.Count);
        bagDataList.Insert(insertIndex, slotData);
        UpdateCapacityLabel();
        RebuildSlots();
        
        return itemSlots.Count > insertIndex ? itemSlots[insertIndex] : null;
    }

    /// <summary>
    /// Gets the index of a bag item slot in the current list
    /// </summary>
    public int GetSlotIndex(UI_BagItemSlot slot) {
        return itemSlots.IndexOf(slot);
    }

    /// <summary>
    /// Moves a slot from one index to another and rebuilds UI
    /// </summary>
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
        ClearSpawnedSlots();
        UpdateCapacityLabel();
    }

    private void RebuildSlots() {
        ClearSpawnedSlots();

        for (int i = 0; i < bagDataList.Count; i++) {
            SpawnSlot(bagDataList[i]);
        }

        UpdateCapacityLabel();
    }

    private void UpdateCapacityLabel() {
        if (totalCapacityText != null) {
            totalCapacityText.text = $"{CalculateTotalWeight()}/{maxCapacity}";
        }
    }

    private UI_BagItemSlot SpawnSlot(BagSlotData slotData) {
        if (slotData == null) {
            return null;
        }

        if (itemSlotPrefab == null) {
            Debug.LogWarning("[UI_BagManager] itemSlotPrefab is not assigned.");
            return null;
        }

        GameObject spawnedObject = PrefabSpawn.SpawnInit(itemSlotPrefab.gameObject, contentRoot);
        if (spawnedObject == null) {
            return null;
        }

        UI_BagItemSlot slotView = spawnedObject.GetComponent<UI_BagItemSlot>();
        if (slotView == null) {
            Debug.LogWarning("[UI_BagManager] Spawned prefab does not have UI_BagItemSlot.");
            return null;
        }

        itemSlots.Add(slotView);
        slotView.UpdateSlotUI(slotData);
        return slotView;
    }

    private void ClearSpawnedSlots() {
        for (int i = 0; i < itemSlots.Count; i++) {
            if (itemSlots[i] != null) {
                Destroy(itemSlots[i].gameObject);
            }
        }
        itemSlots.Clear();
    }
}
