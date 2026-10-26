using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class UI_BagManager : MonoBehaviour {
    [Header("Capacity Settings")]
    [SerializeField] private TextMeshProUGUI totalCapacityText;

    [Header("Nearby Pickup Scan")]
    [SerializeField] private float pickupScanRadius = 6f;
    [SerializeField] private LayerMask pickupScanLayerMask = ~0;
    [SerializeField] public List<Pickupable> nearbyPickupables = new List<Pickupable>();
    private readonly Dictionary<Pickupable, int> nearbyPickupableCounts = new Dictionary<Pickupable, int>();
    [SerializeField] private GameObject pickupableUI;

    [Header("UI Spawn Setup")]
    [SerializeField] private Transform bagContentRoot;
    [SerializeField] private Transform pickupableContentRoot;
    [SerializeField] private UI_BagItemSlot itemSlotPrefab;

    // Display cache rebuilt from the local player's server-owned inventory.
    public List<BagSlotData> bagDataList = new List<BagSlotData>();
    public event Action BagChanged;

    private PlayerInventory playerInventory;
    private Coroutine pendingInventoryRefresh;
    public int MaxCapacity => playerInventory != null
        ? playerInventory.MaxCapacity
        : PlayerInventory.DefaultMaxCapacity;

    public event Action InventoryDisplayChanged;

    private static readonly AttachmentType[] DefaultWeaponAttachmentTypes = {
        AttachmentType.AttackSpeed,
        AttachmentType.DamageBonus,
        AttachmentType.CriticalRate
    };

    private IEnumerator Start() {
        while (!TryBindLocalInventory()) {
            yield return null;
        }
    }

    private void OnDestroy() {
        if (playerInventory != null) {
            playerInventory.InventoryChanged -= HandleInventoryChanged;
        }
    }

    private bool TryBindLocalInventory() {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsClient || networkManager.LocalClient == null) {
            return false;
        }

        NetworkObject localPlayer = networkManager.LocalClient.PlayerObject;
        if (localPlayer == null || !localPlayer.TryGetComponent(out PlayerInventory inventory)) {
            return false;
        }

        playerInventory = inventory;
        playerInventory.InventoryChanged += HandleInventoryChanged;
        RebuildFromPlayerInventory();

        Debug.Log($"[UI_BagManager] Bound to PlayerInventory for client {playerInventory.OwnerClientId}.", this);
        return true;
    }

    private void HandleInventoryChanged() {
        if (pendingInventoryRefresh == null) {
            pendingInventoryRefresh = StartCoroutine(RebuildFromPlayerInventoryNextFrame());
        }
    }

    private IEnumerator RebuildFromPlayerInventoryNextFrame() {
        yield return null;
        pendingInventoryRefresh = null;

        if (playerInventory != null) {
            RebuildFromPlayerInventory();
        }
    }

    private void RebuildFromPlayerInventory() {
        bagDataList.Clear();

        for (int slotIndex = 0; slotIndex < playerInventory.SlotCount; slotIndex++) {
            if (!playerInventory.TryGetSlot(slotIndex, out InventorySlotState networkSlot)) {
                Debug.LogWarning($"[UI_BagManager] Server inventory is missing slot {slotIndex}.", this);
                continue;
            }

            if (TryCreateDisplaySlot(networkSlot.Item, out BagSlotData displaySlot)) {
                bagDataList.Add(displaySlot);
            }
        }

        RebuildSlots();
        InventoryDisplayChanged?.Invoke();
        Debug.Log($"[UI_BagManager] Rendered {bagDataList.Count} server inventory slot(s).", this);
    }

    private bool TryCreateDisplaySlot(InventoryItemState networkItem, out BagSlotData displaySlot) {
        displaySlot = null;

        string itemId = networkItem.ItemId.ToString();
        if (!networkItem.IsValid || !ItemManager.TryGetItemById(itemId, out ItemData itemData)) {
            Debug.LogError($"[UI_BagManager] Cannot display unknown or invalid item ID '{itemId}'.", this);
            return false;
        }

        displaySlot = new BagSlotData {
            instanceId = networkItem.InstanceId,
            itemData = itemData,
            count = networkItem.Count
        };

        if (!displaySlot.IsWeapon) {
            return true;
        }

        displaySlot.EnsureAttachmentSlots(DefaultWeaponAttachmentTypes);
        ApplyDisplayAttachment(displaySlot, AttachmentType.AttackSpeed, networkItem.AttackSpeedAttachmentId.ToString());
        ApplyDisplayAttachment(displaySlot, AttachmentType.DamageBonus, networkItem.DamageBonusAttachmentId.ToString());
        ApplyDisplayAttachment(displaySlot, AttachmentType.CriticalRate, networkItem.CriticalRateAttachmentId.ToString());
        return true;
    }

    private void ApplyDisplayAttachment(BagSlotData displaySlot, AttachmentType attachmentType, string attachmentItemId) {
        if (string.IsNullOrWhiteSpace(attachmentItemId)) {
            return;
        }

        if (!ItemManager.TryGetItemById(attachmentItemId, out ItemData attachmentItem) ||
            attachmentItem.attachmentType != attachmentType) {
            Debug.LogError($"[UI_BagManager] Cannot display attachment '{attachmentItemId}' as {attachmentType}.", this);
            return;
        }

        foreach (AttachmentSlotData attachmentSlot in displaySlot.attachments) {
            if (attachmentSlot.requiredType == attachmentType) {
                attachmentSlot.attachedItem = attachmentItem;
                return;
            }
        }
    }

    public bool RefreshNearbyPickupables() {
        if (playerInventory == null) {
            return false;
        }

        List<Pickupable> foundPickupables = new List<Pickupable>();
        Collider[] hits = Physics.OverlapSphere(
            playerInventory.transform.position,
            pickupScanRadius,
            pickupScanLayerMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits.Length; i++) {
            if (hits[i] == null) {
                continue;
            }

            Pickupable pickupable = hits[i].GetComponentInParent<Pickupable>();
            if (pickupable == null || !pickupable.HasItem) {
                continue;
            }

            if (!foundPickupables.Contains(pickupable)) {
                foundPickupables.Add(pickupable);
            }
        }

        bool changed = foundPickupables.Count != nearbyPickupables.Count;
        if (!changed) {
            for (int i = 0; i < nearbyPickupables.Count; i++) {
                if (!foundPickupables.Contains(nearbyPickupables[i])) {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed) {
            foreach (Pickupable pickupable in foundPickupables) {
                if (!nearbyPickupableCounts.TryGetValue(pickupable, out int previousCount) ||
                    previousCount != pickupable.Count) {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed) {
            return false;
        }

        nearbyPickupables.Clear();
        nearbyPickupables.AddRange(foundPickupables);
        nearbyPickupableCounts.Clear();
        foreach (Pickupable pickupable in foundPickupables) {
            nearbyPickupableCounts[pickupable] = pickupable.Count;
        }
        return true;
    }

    public IReadOnlyList<Pickupable> GetNearbyPickupables() {
        return nearbyPickupables;
    }

    public void RefreshNearbyPickupablesUI() {
        ClearSpawnedSlots(pickupableContentRoot);
        if (nearbyPickupables.Count == 0) {
            pickupableUI.SetActive(false);
            return;
        }
        
        foreach (var pickupable in nearbyPickupables) {
            SpawnSlot(pickupable);
        }
        pickupableUI.SetActive(true);
    }

    // Refresh the entire bag UI and total capacity
    public void RefreshBagUI(List<BagSlotData> currentBagData) {
        bagDataList = currentBagData;
        RebuildSlots();
    }

    public void AddItem(ItemObject itemObject, int count = 1) {
        AddItem(ItemData.FromItemObject(itemObject), count);
    }

    public void AddItem(ItemData itemData, int count = 1, int insert_index = -1, params AttachmentType[] requiredAttachmentTypes) {
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

        if (insert_index == -1) {
            bagDataList.Add(slotData);
            UpdateCapacityLabel();
            SpawnSlot(slotData);
            NotifyBagChanged();
        }
        else {
            bagDataList.Insert(insert_index, slotData);
            UpdateCapacityLabel();
            RebuildSlots();
        }
    }

    public bool SetAttachmentItem(int index, AttachmentType attachmentType, ItemData itemData) {
        if (bagDataList == null || index < 0 || index >= bagDataList.Count) {
            return false;
        }

        BagSlotData slotData = bagDataList[index];
        if (slotData == null || slotData.attachments == null) {
            return false;
        }

        if (!slotData.IsWeapon || slotData.attachments.Exists(slot => slot != null && !slot.IsEmpty)) {
            return false;
        }

        for (int i = 0; i < slotData.attachments.Count; i++) {
            AttachmentSlotData attachmentSlot = slotData.attachments[i];
            if (attachmentSlot == null || attachmentSlot.requiredType != attachmentType) {
                continue;
            }

            if (!attachmentSlot.CanAccept(itemData)) {
                return false;
            }

            attachmentSlot.attachedItem = itemData;
            RebuildSlots();
            return true;
        }
        return false;
    }

    public bool ClearAttachmentItem(int bagIndex, AttachmentType attachmentType) {
        if (bagDataList == null || bagIndex < 0 || bagIndex >= bagDataList.Count) {
            return false;
        }

        BagSlotData slotData = bagDataList[bagIndex];
        if (slotData == null || slotData.attachments == null) {
            return false;
        }

        foreach (var attachmentSlot in slotData.attachments) {
            if (attachmentSlot != null && attachmentSlot.requiredType == attachmentType) {
                attachmentSlot.Clear();
                RebuildSlots();
                return true;
            }
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

    public void RequestMoveSlot(int fromIndex, int insertionIndex) {
        if (playerInventory == null || fromIndex < 0 || fromIndex >= bagDataList.Count) {
            return;
        }

        ulong instanceId = bagDataList[fromIndex].instanceId;
        if (instanceId == 0) {
            Debug.LogWarning($"[UI_BagManager] Slot {fromIndex} has no server item instance ID.", this);
            return;
        }

        playerInventory.RequestMoveItem(instanceId, insertionIndex);
    }

    public void RequestInstallAttachment(int attachmentIndex, int weaponIndex, AttachmentType attachmentType) {
        if (playerInventory == null ||
            attachmentIndex < 0 || attachmentIndex >= bagDataList.Count ||
            weaponIndex < 0 || weaponIndex >= bagDataList.Count) {
            return;
        }

        ulong attachmentInstanceId = bagDataList[attachmentIndex].instanceId;
        ulong weaponInstanceId = bagDataList[weaponIndex].instanceId;
        if (attachmentInstanceId == 0 || weaponInstanceId == 0) {
            Debug.LogWarning("[UI_BagManager] Attachment request is missing a server item instance ID.", this);
            return;
        }

        playerInventory.RequestInstallAttachment(
            attachmentInstanceId,
            weaponInstanceId,
            attachmentType
        );
    }

    public void RequestDetachAttachment(int weaponIndex, AttachmentType attachmentType, int insertionIndex = -1) {
        if (playerInventory == null || weaponIndex < 0 || weaponIndex >= bagDataList.Count) {
            return;
        }

        ulong weaponInstanceId = bagDataList[weaponIndex].instanceId;
        if (weaponInstanceId == 0) {
            Debug.LogWarning("[UI_BagManager] Detachment request is missing a server weapon instance ID.", this);
            return;
        }

        playerInventory.RequestDetachAttachment(weaponInstanceId, attachmentType, insertionIndex);
    }

    public void RequestTransferAttachment(int sourceWeaponIndex, int targetWeaponIndex, AttachmentType attachmentType) {
        if (playerInventory == null || sourceWeaponIndex < 0 || sourceWeaponIndex >= bagDataList.Count ||
            targetWeaponIndex < 0 || targetWeaponIndex >= bagDataList.Count) {
            return;
        }

        ulong sourceId = bagDataList[sourceWeaponIndex].instanceId;
        ulong targetId = bagDataList[targetWeaponIndex].instanceId;
        if (sourceId != 0 && targetId != 0 && sourceId != targetId) {
            playerInventory.RequestTransferAttachment(sourceId, targetId, attachmentType);
        }
    }

    public void RequestDropAttachment(int weaponIndex, AttachmentType attachmentType) {
        if (playerInventory == null || weaponIndex < 0 || weaponIndex >= bagDataList.Count) {
            return;
        }

        ulong weaponInstanceId = bagDataList[weaponIndex].instanceId;
        if (weaponInstanceId != 0) {
            playerInventory.RequestDropAttachment(weaponInstanceId, attachmentType);
        }
    }

    public void RequestDropSlot(int slotIndex) {
        if (playerInventory == null || slotIndex < 0 || slotIndex >= bagDataList.Count) {
            return;
        }

        ulong instanceId = bagDataList[slotIndex].instanceId;
        if (instanceId == 0) {
            Debug.LogWarning($"[UI_BagManager] Slot {slotIndex} has no server item instance ID.", this);
            return;
        }

        playerInventory.RequestDropItem(instanceId);
    }

    public void RequestPickup(Pickupable pickupable, int insertionIndex) {
        if (playerInventory == null || pickupable == null || !pickupable.HasItem) {
            return;
        }

        NetworkObject droppedObject = pickupable.GetComponentInParent<NetworkObject>();
        if (droppedObject == null || !droppedObject.IsSpawned) {
            Debug.LogWarning("[UI_BagManager] Nearby item is not a spawned network object.", this);
            return;
        }

        playerInventory.RequestPickupItem(droppedObject, insertionIndex);
    }

    public void RequestPickupAttachment(Pickupable pickupable, int weaponIndex) {
        if (playerInventory == null || pickupable == null || !pickupable.HasItem ||
            weaponIndex < 0 || weaponIndex >= bagDataList.Count) {
            return;
        }

        NetworkObject droppedObject = pickupable.GetComponentInParent<NetworkObject>();
        ulong weaponInstanceId = bagDataList[weaponIndex].instanceId;
        if (droppedObject != null && droppedObject.IsSpawned && weaponInstanceId != 0) {
            playerInventory.RequestPickupAttachment(droppedObject, weaponInstanceId);
        }
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
        return itemWeight >= 0 && (long)CalculateTotalWeight() + itemWeight <= MaxCapacity;
    }

    public void ClearBag() {
        bagDataList.Clear();
        UpdateCapacityLabel();
        NotifyBagChanged();
    }

    public void RebuildSlots() {
        ClearSpawnedSlots();

        for (int i = 0; i < bagDataList.Count; i++) {
            SpawnSlot(bagDataList[i]);
        }

        UpdateCapacityLabel();
        NotifyBagChanged();
    }

    private void NotifyBagChanged() {
        BagChanged?.Invoke();
    }

    private void UpdateCapacityLabel() {
        if (totalCapacityText != null) {
            totalCapacityText.text = $"{CalculateTotalWeight()} / {MaxCapacity}";
        }
    }

    private void SpawnSlot(BagSlotData slotData, Transform contentroot=null) {
        contentroot ??= bagContentRoot;
        if (slotData == null) {
            return;
        }

        if (itemSlotPrefab == null) {
            Debug.LogWarning("[UI_BagManager] itemSlotPrefab is not assigned.");
            return;
        }

        GameObject spawnedObject = PrefabSpawn.SpawnInit(itemSlotPrefab.gameObject, contentroot);
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

    public void SpawnSlot(Pickupable pickupable) {
        if (pickupable == null || !pickupable.HasItem) {
            return;
        }

        BagSlotData slotData = new BagSlotData {
            itemData = pickupable.ItemData,
            count = pickupable.Count,
            attachments = pickupable.Attachments,
        };

        if (slotData.IsWeapon) {
            slotData.EnsureAttachmentSlots(DefaultWeaponAttachmentTypes);
        }

        SpawnSlot(slotData, pickupableContentRoot);
    }

    public void ClearSpawnedSlots(Transform contentroot = null) {
        contentroot ??= bagContentRoot;
        ObjectControll.ClearChildren(contentroot, "Bag_Item_Slot(Clone)");
    }

    public int FindSlotIndex(UI_BagItemSlot slot) {
        if (slot == null) {
            return -1;
        }

        return ObjectControll.GetIndex(slot);
    }

    public bool IsPlayerBagSlot(UI_BagItemSlot slot) {
        return slot != null && bagContentRoot != null && slot.transform.IsChildOf(bagContentRoot);
    }

    public bool IsNearbyPickupSlot(UI_BagItemSlot slot) {
        return slot != null && pickupableContentRoot != null && slot.transform.IsChildOf(pickupableContentRoot);
    }
}
