using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerInventory : NetworkBehaviour {
    private const string ArrowId = "arrow";
    private const string AxeId = "axe";
    private const string SickleId = "sickle";
    private const string SwordId = "sword";
    private const string AttackSpeedId = "attack_speed";

    // Match-scoped IDs. Only the server calls AllocateInstanceId.
    private static ulong nextInstanceId = 1;

    private NetworkList<InventorySlotState> slots = new NetworkList<InventorySlotState>(
        readPerm: NetworkVariableReadPermission.Owner,
        writePerm: NetworkVariableWritePermission.Server
    );

    public event Action InventoryChanged;

    public int SlotCount => slots.Count;

    public override void OnNetworkSpawn() {
        slots.OnListChanged += HandleSlotsChanged;

        if (IsServer && slots.Count == 0) {
            InitializeStartingInventory();
        }

        if (IsOwner) {
            Debug.Log($"[PlayerInventory] Local inventory ready for client {OwnerClientId}: {BuildInventorySummary()}", this);
        }
    }

    public override void OnNetworkDespawn() {
        slots.OnListChanged -= HandleSlotsChanged;
    }

    public bool TryGetSlot(int slotIndex, out InventorySlotState slot) {
        for (int i = 0; i < slots.Count; i++) {
            if (slots[i].SlotIndex == slotIndex) {
                slot = slots[i];
                return true;
            }
        }

        slot = default;
        return false;
    }

    public bool TryGetItem(ulong instanceId, out InventoryItemState item) {
        for (int i = 0; i < slots.Count; i++) {
            if (slots[i].Item.InstanceId == instanceId) {
                item = slots[i].Item;
                return true;
            }
        }

        item = InventoryItemState.Empty;
        return false;
    }

    private void InitializeStartingInventory() {
        AddStartingItem(slotIndex: 0, itemId: ArrowId, count: 3);
        AddStartingItem(slotIndex: 1, itemId: AxeId, count: 1, attackSpeedAttachmentId: AttackSpeedId);
        AddStartingItem(slotIndex: 2, itemId: SickleId, count: 1);
        AddStartingItem(slotIndex: 3, itemId: SwordId, count: 1);

        Debug.Log($"[PlayerInventory] Server initialized client {OwnerClientId}: {BuildInventorySummary()}", this);
    }

    private void AddStartingItem(int slotIndex, string itemId, int count, string attackSpeedAttachmentId = null) {
        if (!ItemManager.TryGetItemById(itemId, out ItemData itemData)) {
            Debug.LogError($"[PlayerInventory] Cannot add unknown starting item ID '{itemId}'.", this);
            return;
        }

        int maxCount = Mathf.Max(1, itemData.itemObject.maxCount);
        if (count <= 0 || count > maxCount) {
            Debug.LogError($"[PlayerInventory] Invalid starting count {count} for '{itemId}' (maximum {maxCount}).", this);
            return;
        }

        InventoryItemState item = new InventoryItemState {
            InstanceId = AllocateInstanceId(),
            ItemId = new FixedString64Bytes(itemId),
            Count = count
        };

        if (!string.IsNullOrWhiteSpace(attackSpeedAttachmentId)) {
            if (!TryResolveAttachment(attackSpeedAttachmentId, AttachmentType.AttackSpeed)) {
                return;
            }

            item.SetAttachmentId(AttachmentType.AttackSpeed, new FixedString64Bytes(attackSpeedAttachmentId));
        }

        slots.Add(new InventorySlotState {
            SlotIndex = slotIndex,
            Item = item
        });
    }

    private bool TryResolveAttachment(string itemId, AttachmentType expectedType) {
        if (!ItemManager.TryGetItemById(itemId, out ItemData itemData)) {
            Debug.LogError($"[PlayerInventory] Cannot add unknown starting attachment ID '{itemId}'.", this);
            return false;
        }

        if (itemData.attachmentType != expectedType) {
            Debug.LogError($"[PlayerInventory] Starting attachment '{itemId}' is not of type {expectedType}.", this);
            return false;
        }

        return true;
    }

    private static ulong AllocateInstanceId() {
        return nextInstanceId++;
    }

    private void HandleSlotsChanged(NetworkListEvent<InventorySlotState> changeEvent) {
        InventoryChanged?.Invoke();
    }

    private string BuildInventorySummary() {
        if (slots.Count == 0) {
            return "empty";
        }

        string summary = string.Empty;
        for (int i = 0; i < slots.Count; i++) {
            InventorySlotState slot = slots[i];
            if (i > 0) {
                summary += ", ";
            }

            summary += $"slot {slot.SlotIndex}={slot.Item.ItemId} x{slot.Item.Count} (instance {slot.Item.InstanceId})";
            if (slot.Item.AttackSpeedAttachmentId.Length > 0) {
                summary += $" +{slot.Item.AttackSpeedAttachmentId}";
            }
        }

        return summary;
    }
}
