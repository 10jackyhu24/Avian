using System.Collections.Generic;
using UnityEngine;

public sealed class DroppedItemRecord {
    public InventoryItemState ItemState { get; private set; }
    public Vector3 Position { get; private set; }
    public NetworkDroppedItem SpawnedItem { get; private set; }

    public DroppedItemRecord(
        InventoryItemState itemState,
        Vector3 position,
        NetworkDroppedItem spawnedItem) {
        ItemState = itemState;
        Position = position;
        SpawnedItem = spawnedItem;
    }

    public void SetSpawnedItem(NetworkDroppedItem spawnedItem) {
        SpawnedItem = spawnedItem;
    }

    public void SetItemState(InventoryItemState itemState) {
        ItemState = itemState;
    }

    public void SetPosition(Vector3 position) {
        Position = position;
    }
}

public static class DroppedItemRegistry {
    private static readonly Dictionary<ulong, DroppedItemRecord> records =
        new Dictionary<ulong, DroppedItemRecord>();

    public static int Count => records.Count;
    public static uint WorldGeneration { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() {
        records.Clear();
        unchecked { WorldGeneration++; }
    }

    public static bool TryRegister(
        InventoryItemState itemState,
        Vector3 position,
        NetworkDroppedItem spawnedItem) {
        if (!itemState.IsValid || spawnedItem == null || records.ContainsKey(itemState.InstanceId)) {
            return false;
        }

        records.Add(
            itemState.InstanceId,
            new DroppedItemRecord(itemState, position, spawnedItem)
        );

        Debug.Log(
            $"[DroppedItemRegistry] Registered {itemState.ItemId} " +
            $"(instance {itemState.InstanceId}). Records={records.Count}."
        );
        return true;
    }

    public static bool TryGet(ulong instanceId, out DroppedItemRecord record) {
        return records.TryGetValue(instanceId, out record);
    }

    public static List<DroppedItemRecord> GetRecordsSnapshot() {
        return new List<DroppedItemRecord>(records.Values);
    }

    public static bool TrySetSpawnedItem(
        ulong instanceId,
        NetworkDroppedItem expectedCurrentItem,
        NetworkDroppedItem newSpawnedItem) {
        if (!records.TryGetValue(instanceId, out DroppedItemRecord record) ||
            record.SpawnedItem != expectedCurrentItem) {
            return false;
        }

        record.SetSpawnedItem(newSpawnedItem);
        return true;
    }

    public static bool TryUpdatePosition(
        ulong instanceId,
        NetworkDroppedItem expectedSpawnedItem,
        Vector3 position) {
        if (!records.TryGetValue(instanceId, out DroppedItemRecord record) ||
            record.SpawnedItem != expectedSpawnedItem) {
            return false;
        }

        record.SetPosition(position);
        return true;
    }

    public static bool TryUpdateItemState(
        ulong instanceId,
        NetworkDroppedItem expectedSpawnedItem,
        InventoryItemState itemState) {
        if (!itemState.IsValid || itemState.InstanceId != instanceId ||
            !records.TryGetValue(instanceId, out DroppedItemRecord record) ||
            record.SpawnedItem != expectedSpawnedItem ||
            !record.ItemState.ItemId.Equals(itemState.ItemId)) {
            return false;
        }

        record.SetItemState(itemState);
        return true;
    }

    public static bool TryRemove(
        ulong instanceId,
        NetworkDroppedItem expectedSpawnedItem,
        out DroppedItemRecord record) {
        if (!records.TryGetValue(instanceId, out record) ||
            record.SpawnedItem != expectedSpawnedItem) {
            return false;
        }

        records.Remove(instanceId);
        Debug.Log(
            $"[DroppedItemRegistry] Removed {record.ItemState.ItemId} " +
            $"(instance {instanceId}). Records={records.Count}."
        );
        return true;
    }
}
