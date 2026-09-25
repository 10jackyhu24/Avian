using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerInventory : NetworkBehaviour {
    private const float PickupRange = 6f;
    private const string ArrowId = "arrow";
    private const string AxeId = "axe";
    private const string SickleId = "sickle";
    private const string SwordId = "sword";
    private const string AttackSpeedId = "attack_speed";

    [SerializeField] private int maxCapacity = 100;

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

    public void RequestMoveItem(ulong instanceId, int insertionIndex) {
        if (!IsSpawned || !IsClient || !IsOwner) {
            return;
        }

        RequestMoveItemServerRpc(instanceId, insertionIndex);
    }

    public void RequestInstallAttachment(
        ulong attachmentInstanceId,
        ulong weaponInstanceId,
        AttachmentType attachmentType) {
        if (!IsSpawned || !IsClient || !IsOwner) {
            return;
        }

        RequestInstallAttachmentServerRpc(attachmentInstanceId, weaponInstanceId, attachmentType);
    }

    public void RequestDetachAttachment(ulong weaponInstanceId, AttachmentType attachmentType) {
        if (!IsSpawned || !IsClient || !IsOwner) {
            return;
        }

        RequestDetachAttachmentServerRpc(weaponInstanceId, attachmentType);
    }

    public void RequestDropItem(ulong instanceId) {
        if (!IsSpawned || !IsClient || !IsOwner) {
            return;
        }

        RequestDropItemServerRpc(instanceId);
    }

    public void RequestPickupItem(NetworkObject droppedObject) {
        if (!IsSpawned || !IsClient || !IsOwner || droppedObject == null || !droppedObject.IsSpawned) {
            return;
        }

        RequestPickupItemServerRpc(new NetworkObjectReference(droppedObject));
    }

    [ServerRpc]
    private void RequestMoveItemServerRpc(ulong instanceId, int insertionIndex) {
        if (instanceId == 0 || insertionIndex < 0 || insertionIndex > slots.Count) {
            Debug.LogWarning($"[PlayerInventory] Rejected invalid move request from client {OwnerClientId}.", this);
            return;
        }

        int sourceIndex = FindItemIndex(instanceId);
        if (sourceIndex < 0) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} does not own item instance {instanceId}.", this);
            return;
        }

        int destinationIndex = insertionIndex;
        if (sourceIndex < destinationIndex) {
            destinationIndex--;
        }

        destinationIndex = Mathf.Clamp(destinationIndex, 0, slots.Count - 1);
        if (sourceIndex == destinationIndex) {
            return;
        }

        MoveItemOnServer(sourceIndex, destinationIndex);
        Debug.Log($"[PlayerInventory] Client {OwnerClientId} moved instance {instanceId} from slot {sourceIndex} to slot {destinationIndex}.", this);
    }

    [ServerRpc]
    private void RequestInstallAttachmentServerRpc(
        ulong attachmentInstanceId,
        ulong weaponInstanceId,
        AttachmentType attachmentType) {
        if (attachmentInstanceId == 0 ||
            weaponInstanceId == 0 ||
            attachmentInstanceId == weaponInstanceId ||
            attachmentType == AttachmentType.None) {
            Debug.LogWarning($"[PlayerInventory] Rejected invalid attachment request from client {OwnerClientId}.", this);
            return;
        }

        int attachmentIndex = FindItemIndex(attachmentInstanceId);
        int weaponIndex = FindItemIndex(weaponInstanceId);
        if (attachmentIndex < 0 || weaponIndex < 0) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} does not own both requested item instances.", this);
            return;
        }

        InventoryItemState attachmentItem = slots[attachmentIndex].Item;
        InventoryItemState weaponItem = slots[weaponIndex].Item;

        if (!ItemManager.TryGetItemById(attachmentItem.ItemId.ToString(), out ItemData attachmentData) ||
            attachmentData.attachmentType != attachmentType) {
            Debug.LogWarning($"[PlayerInventory] Rejected incompatible attachment from client {OwnerClientId}.", this);
            return;
        }

        if (!ItemManager.TryGetItemById(weaponItem.ItemId.ToString(), out ItemData weaponData) ||
            !weaponData.IsWeapon) {
            Debug.LogWarning($"[PlayerInventory] Rejected attachment target from client {OwnerClientId}: target is not a weapon.", this);
            return;
        }

        if (weaponItem.GetAttachmentId(attachmentType).Length > 0) {
            Debug.LogWarning($"[PlayerInventory] Rejected attachment from client {OwnerClientId}: {attachmentType} slot is occupied.", this);
            return;
        }

        weaponItem.SetAttachmentId(attachmentType, attachmentItem.ItemId);
        SetSlotItem(weaponIndex, weaponItem);
        ConsumeOneItemOnServer(attachmentIndex);

        Debug.Log($"[PlayerInventory] Client {OwnerClientId} installed {attachmentItem.ItemId} on {weaponItem.ItemId}.", this);
    }

    [ServerRpc]
    private void RequestDetachAttachmentServerRpc(
        ulong weaponInstanceId,
        AttachmentType attachmentType) {
        if (weaponInstanceId == 0 || attachmentType == AttachmentType.None) {
            Debug.LogWarning($"[PlayerInventory] Rejected invalid detachment request from client {OwnerClientId}.", this);
            return;
        }

        int weaponIndex = FindItemIndex(weaponInstanceId);
        if (weaponIndex < 0) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} does not own weapon instance {weaponInstanceId}.", this);
            return;
        }

        InventoryItemState weaponItem = slots[weaponIndex].Item;
        if (!ItemManager.TryGetItemById(weaponItem.ItemId.ToString(), out ItemData weaponData) ||
            !weaponData.IsWeapon) {
            Debug.LogWarning($"[PlayerInventory] Rejected detachment target from client {OwnerClientId}: target is not a weapon.", this);
            return;
        }

        FixedString64Bytes attachmentItemId = weaponItem.GetAttachmentId(attachmentType);
        if (attachmentItemId.Length == 0) {
            Debug.LogWarning($"[PlayerInventory] Rejected detachment from client {OwnerClientId}: {attachmentType} slot is empty.", this);
            return;
        }

        if (!ItemManager.TryGetItemById(attachmentItemId.ToString(), out ItemData attachmentData) ||
            attachmentData.attachmentType != attachmentType) {
            Debug.LogWarning($"[PlayerInventory] Rejected invalid {attachmentType} attachment on {weaponItem.ItemId}.", this);
            return;
        }

        weaponItem.SetAttachmentId(attachmentType, default);
        SetSlotItem(weaponIndex, weaponItem);

        slots.Add(new InventorySlotState {
            SlotIndex = slots.Count,
            Item = new InventoryItemState {
                InstanceId = AllocateInstanceId(),
                ItemId = attachmentItemId,
                Count = 1
            }
        });

        Debug.Log($"[PlayerInventory] Client {OwnerClientId} detached {attachmentItemId} from {weaponItem.ItemId}.", this);
    }

    [ServerRpc]
    private void RequestDropItemServerRpc(ulong instanceId) {
        int slotIndex = FindItemIndex(instanceId);
        if (instanceId == 0 || slotIndex < 0) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} cannot drop item instance {instanceId}.", this);
            return;
        }

        InventoryItemState item = slots[slotIndex].Item;
        if (!NetworkDroppedItem.TrySpawnOnServer(item, transform.position)) {
            Debug.LogWarning($"[PlayerInventory] Failed to spawn dropped item {item.ItemId} for client {OwnerClientId}.", this);
            return;
        }

        RemoveSlotOnServer(slotIndex);
        GetComponent<PlayerController>()?.UnequipDroppedWeaponOnServer(item.ItemId.ToString());
        Debug.Log($"[PlayerInventory] Client {OwnerClientId} dropped {item.ItemId} x{item.Count} (instance {item.InstanceId}).", this);
    }

    [ServerRpc]
    private void RequestPickupItemServerRpc(NetworkObjectReference droppedObjectReference) {
        if (!droppedObjectReference.TryGet(out NetworkObject droppedObject) ||
            droppedObject == null ||
            !droppedObject.IsSpawned ||
            !droppedObject.TryGetComponent(out NetworkDroppedItem droppedItem)) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} requested an unavailable dropped item.", this);
            return;
        }

        float distanceSquared = (droppedObject.transform.position - transform.position).sqrMagnitude;
        if (distanceSquared > PickupRange * PickupRange) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} is too far away to pick up the item.", this);
            return;
        }

        InventoryItemState item = droppedItem.ItemState;
        if (!item.IsValid || FindItemIndex(item.InstanceId) >= 0) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} requested an invalid or duplicate item.", this);
            return;
        }

        if (!CanAddItemOnServer(item)) {
            Debug.LogWarning($"[PlayerInventory] Client {OwnerClientId} does not have enough capacity for {item.ItemId}.", this);
            return;
        }

        slots.Add(new InventorySlotState {
            SlotIndex = slots.Count,
            Item = item
        });
        droppedObject.Despawn(true);

        Debug.Log($"[PlayerInventory] Client {OwnerClientId} picked up {item.ItemId} x{item.Count} (instance {item.InstanceId}).", this);
    }

    private int FindItemIndex(ulong instanceId) {
        for (int i = 0; i < slots.Count; i++) {
            if (slots[i].Item.InstanceId == instanceId) {
                return i;
            }
        }

        return -1;
    }

    private void MoveItemOnServer(int sourceIndex, int destinationIndex) {
        InventoryItemState movingItem = slots[sourceIndex].Item;

        if (sourceIndex < destinationIndex) {
            for (int i = sourceIndex; i < destinationIndex; i++) {
                SetSlotItem(i, slots[i + 1].Item);
            }
        }
        else {
            for (int i = sourceIndex; i > destinationIndex; i--) {
                SetSlotItem(i, slots[i - 1].Item);
            }
        }

        SetSlotItem(destinationIndex, movingItem);
    }

    private void SetSlotItem(int slotIndex, InventoryItemState item) {
        InventorySlotState slot = slots[slotIndex];
        slot.Item = item;
        slots[slotIndex] = slot;
    }

    private void ConsumeOneItemOnServer(int slotIndex) {
        InventoryItemState item = slots[slotIndex].Item;
        if (item.Count > 1) {
            item.Count--;
            SetSlotItem(slotIndex, item);
            return;
        }

        RemoveSlotOnServer(slotIndex);
    }

    private void RemoveSlotOnServer(int slotIndex) {
        slots.RemoveAt(slotIndex);
        for (int i = slotIndex; i < slots.Count; i++) {
            InventorySlotState slot = slots[i];
            slot.SlotIndex = i;
            slots[i] = slot;
        }
    }

    private bool CanAddItemOnServer(InventoryItemState incomingItem) {
        if (!TryCalculateWeight(incomingItem, out int incomingWeight)) {
            return false;
        }

        int currentWeight = 0;
        for (int i = 0; i < slots.Count; i++) {
            if (!TryCalculateWeight(slots[i].Item, out int slotWeight)) {
                return false;
            }

            currentWeight += slotWeight;
        }

        return currentWeight + incomingWeight <= maxCapacity;
    }

    private static bool TryCalculateWeight(InventoryItemState item, out int weight) {
        weight = 0;
        if (!item.IsValid ||
            !ItemManager.TryGetItemById(item.ItemId.ToString(), out ItemData itemData)) {
            return false;
        }

        weight = itemData.IsWeapon
            ? itemData.itemObject.baseWeight
            : itemData.itemObject.baseWeight * item.Count;

        return TryAddAttachmentWeight(item.AttackSpeedAttachmentId, AttachmentType.AttackSpeed, ref weight) &&
               TryAddAttachmentWeight(item.DamageBonusAttachmentId, AttachmentType.DamageBonus, ref weight) &&
               TryAddAttachmentWeight(item.CriticalRateAttachmentId, AttachmentType.CriticalRate, ref weight);
    }

    private static bool TryAddAttachmentWeight(
        FixedString64Bytes attachmentItemId,
        AttachmentType expectedType,
        ref int weight) {
        if (attachmentItemId.Length == 0) {
            return true;
        }

        if (!ItemManager.TryGetItemById(attachmentItemId.ToString(), out ItemData attachmentData) ||
            attachmentData.attachmentType != expectedType) {
            return false;
        }

        weight += attachmentData.itemObject.baseWeight;
        return true;
    }

    private void InitializeStartingInventory() {
        AddStartingItem(slotIndex: 0, itemId: ArrowId, count: 3);
        AddStartingItem(slotIndex: 1, itemId: AxeId, count: 1, attackSpeedAttachmentId: AttackSpeedId);
        AddStartingItem(slotIndex: 2, itemId: SickleId, count: 1);
        AddStartingItem(slotIndex: 3, itemId: SwordId, count: 1);
        AddStartingItem(slotIndex: 4, itemId: AttackSpeedId, count: 1);

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
