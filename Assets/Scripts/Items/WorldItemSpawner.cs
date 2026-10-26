using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>Server-only, seeded loot cells. Each cell is processed once per world.</summary>
[DisallowMultipleComponent]
public class WorldItemSpawner : MonoBehaviour {
    [SerializeField] private WorldItemSpawnSettings settings;
    private readonly HashSet<Vector2Int> processedCells = new HashSet<Vector2Int>();
    private NetworkManager activeNetworkManager;
    private TerrainController terrain;
    private float nextTick;
    private int activeSeed = int.MinValue;
    private uint registryGeneration;

    public static void EnsureExists(NetworkManager manager) {
        if (manager == null || !manager.IsServer) return;
        WorldItemSpawner spawner = manager.GetComponent<WorldItemSpawner>();
        if (spawner == null) spawner = manager.gameObject.AddComponent<WorldItemSpawner>();
        spawner.activeNetworkManager = manager;
        if (spawner.settings == null) spawner.settings = Resources.Load<WorldItemSpawnSettings>("WorldItemSpawnSettings");
    }

    private void Update() {
        if (activeNetworkManager == null || !activeNetworkManager.IsServer ||
            settings == null || settings.items == null || settings.items.Length == 0 || Time.time < nextTick) return;
        nextTick = Time.time + Mathf.Max(0.1f, settings.tickInterval);
        if (terrain == null) terrain = TerrainController.Instance;
        if (terrain == null) return;
        if (activeSeed != terrain.WorldSeed || registryGeneration != DroppedItemRegistry.WorldGeneration) {
            processedCells.Clear();
            activeSeed = terrain.WorldSeed;
            registryGeneration = DroppedItemRegistry.WorldGeneration;
        }

        Physics.SyncTransforms();
        float cellSize = Mathf.Max(4f, settings.cellSize);
        int radius = Mathf.CeilToInt(settings.spawnRadius / cellSize) + 1;
        int spawned = 0;
        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) {
            if (!player.IsSpawned || !player.IsAlive) continue;
            Vector2Int center = new Vector2Int(Mathf.FloorToInt(player.transform.position.x / cellSize),
                Mathf.FloorToInt(player.transform.position.z / cellSize));
            // Check nearest rings first, so new players can find loot nearby immediately.
            for (int ring = 0; ring <= radius; ring++) {
                for (int x = -ring; x <= ring; x++) {
                    for (int z = -ring; z <= ring; z++) {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                        if (TrySpawnCell(center + new Vector2Int(x, z), player.transform.position, cellSize)) spawned++;
                        if (spawned >= Mathf.Max(1, settings.maxSpawnsPerTick)) return;
                    }
                }
            }
        }
    }

    private bool TrySpawnCell(Vector2Int cell, Vector3 playerPosition, float cellSize) {
        if (processedCells.Contains(cell)) return false;
        Vector3 center = new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
        Vector3 offset = center - playerPosition;
        offset.y = 0f;
        if (offset.sqrMagnitude > settings.spawnRadius * settings.spawnRadius || !terrain.IsWorldPositionLoaded(center)) return false;

        System.Random random = CreateRandom(activeSeed, cell);
        if (random.NextDouble() >= settings.spawnChance) {
            processedCells.Add(cell);
            return false;
        }
        ItemObject item = settings.items[random.Next(settings.items.Length)];
        if (item == null || !item.IsAttachment || string.IsNullOrWhiteSpace(item.itemId)) return false;

        bool hadLoadedCandidate = false;
        for (int attempt = 0; attempt < 8; attempt++) {
            Vector3 candidate = new Vector3((cell.x + 0.1f + 0.8f * (float)random.NextDouble()) * cellSize,
                0f, (cell.y + 0.1f + 0.8f * (float)random.NextDouble()) * cellSize);
            Vector3 fromSpawn = candidate - PlayerSpawner.DefaultSpawnPosition;
            fromSpawn.y = 0f;
            if (!terrain.IsWorldPositionLoaded(candidate)) continue;
            hadLoadedCandidate = true;
            if (fromSpawn.sqrMagnitude < settings.spawnClearRadius * settings.spawnClearRadius ||
                !terrain.TryGetClearGroundPosition(candidate, out Vector3 ground)) continue;

            InventoryItemState state = new InventoryItemState {
                InstanceId = PlayerInventory.AllocateWorldItemInstanceId(),
                ItemId = new FixedString64Bytes(item.itemId), Count = 1
            };
            if (!NetworkDroppedItem.TrySpawnOnServer(state, ground)) return false;
            processedCells.Add(cell);
            return true;
        }
        // Do not retry a blocked cell every frame. Loot already registered in the
        // dropped-item system streams out/in, while collected cells stay collected.
        if (hadLoadedCandidate) processedCells.Add(cell);
        return false;
    }

    private static System.Random CreateRandom(int seed, Vector2Int cell) {
        unchecked {
            int hash = seed * 486187739 ^ cell.x * 73856093 ^ cell.y * 19349663 ^ 0x6d2b79f5;
            return new System.Random(hash & int.MaxValue);
        }
    }
}
