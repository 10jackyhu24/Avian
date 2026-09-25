using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class DroppedItemStreamer : MonoBehaviour {
    private const float TickInterval = 0.75f;

    private NetworkManager networkManager;
    private TerrainController terrainController;
    private float nextTickTime;

    public static void EnsureExists(NetworkManager networkManager) {
        if (networkManager == null || !networkManager.IsServer) {
            return;
        }

        DroppedItemStreamer streamer = networkManager.GetComponent<DroppedItemStreamer>();
        if (streamer == null) {
            streamer = networkManager.gameObject.AddComponent<DroppedItemStreamer>();
        }

        streamer.Initialize(networkManager);
    }

    private void Initialize(NetworkManager value) {
        networkManager = value;
        terrainController = FindFirstObjectByType<TerrainController>();
        nextTickTime = 0f;
    }

    private void Update() {
        if (networkManager == null || !networkManager.IsServer || Time.time < nextTickTime) {
            return;
        }

        nextTickTime = Time.time + TickInterval;
        if (terrainController == null) {
            terrainController = FindFirstObjectByType<TerrainController>();
        }

        if (terrainController != null) {
            RefreshDroppedItems();
        }
    }

    private void RefreshDroppedItems() {
        List<DroppedItemRecord> records = DroppedItemRegistry.GetRecordsSnapshot();
        foreach (DroppedItemRecord record in records) {
            bool shouldBeSpawned = terrainController.IsWorldPositionLoaded(record.Position);
            bool isSpawned = record.SpawnedItem != null && record.SpawnedItem.IsSpawned;

            if (isSpawned && !shouldBeSpawned) {
                DespawnWorldObject(record);
            }
            else if (!isSpawned && shouldBeSpawned) {
                RespawnWorldObject(record);
            }
        }
    }

    private static void DespawnWorldObject(DroppedItemRecord record) {
        NetworkDroppedItem spawnedItem = record.SpawnedItem;
        if (!DroppedItemRegistry.TrySetSpawnedItem(
                record.ItemState.InstanceId,
                spawnedItem,
                newSpawnedItem: null)) {
            return;
        }

        spawnedItem.NetworkObject.Despawn(true);
        Debug.Log(
            $"[DroppedItemStreamer] Despawned {record.ItemState.ItemId} " +
            $"(instance {record.ItemState.InstanceId}); record retained."
        );
    }

    private static void RespawnWorldObject(DroppedItemRecord record) {
        if (!NetworkDroppedItem.TryRespawnOnServer(record)) {
            return;
        }

        Debug.Log(
            $"[DroppedItemStreamer] Respawned {record.ItemState.ItemId} " +
            $"(instance {record.ItemState.InstanceId}) at {record.Position}."
        );
    }
}
