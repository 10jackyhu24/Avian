using UnityEngine;

[CreateAssetMenu(fileName = "WorldItemSpawnSettings", menuName = "Inventory/World Item Spawn Settings")]
public class WorldItemSpawnSettings : ScriptableObject {
    public ItemObject[] items;
    [Min(4f)] public float cellSize = 12f;
    [Range(0f, 1f)] public float spawnChance = 0.4f;
    [Min(1f)] public float spawnRadius = 36f;
    [Min(0f)] public float spawnClearRadius = 5f;
    [Min(1)] public int maxSpawnsPerTick = 6;
    [Min(0.1f)] public float tickInterval = 1f;
}
