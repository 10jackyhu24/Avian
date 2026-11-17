using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class SeededEnemySpawner : MonoBehaviour {
    [Serializable]
    private class BiomeEnemySpawnRule {
        public string name = "Grassland";
        public TerrainBiomeMask biomeMask = TerrainBiomeMask.Grassland;
        [Min(0)] public int enemyCount = 3;
    }

    private class EnemySpawnRecord {
        public string ruleName;
        public TerrainBiomeMask biomeMask;
        public EnemyArchetype archetype;
        public Vector3 position;
        public GameObject activeEnemy;
        public bool isDefeated;
    }

    [Header("Enemy")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("World Query")]
    [SerializeField] private int worldSize = 1000;
    [SerializeField] private float biomeScale = 0.006f;
    [SerializeField] private Vector3 spawnCenter = new Vector3(0f, 5f, 10f);
    [SerializeField] private float minimumDistanceFromSpawnCenter = 15f;
    [SerializeField] private int attemptsPerEnemy = 256;

    [Header("Population")]
    [SerializeField] private float activationRadius = 80f;
    [SerializeField] private float deactivationRadius = 45f;
    [SerializeField] private float populationTickInterval = 0.75f;
    [Min(0)] [SerializeField] private int maxActiveEnemies = 20;

    [Header("Biome Counts")]
    [SerializeField] private BiomeEnemySpawnRule[] biomeSpawnRules = {
        new BiomeEnemySpawnRule {
            name = "Grassland",
            biomeMask = TerrainBiomeMask.Grassland,
            enemyCount = 3
        }
    };

    private readonly List<EnemySpawnRecord> spawnRecords = new List<EnemySpawnRecord>();
    private readonly Dictionary<EnemyHealthDrop, EnemySpawnRecord> recordsByHealth =
        new Dictionary<EnemyHealthDrop, EnemySpawnRecord>();
    private NetworkManager activeNetworkManager;
    private bool hasInitialized;
    private bool populationEnabled = true;
    private float nextPopulationTickTime;

    public void ConfigurePopulation(
        GameObject prefab,
        int enemyCountPerBiome,
        float activationDistance,
        float deactivationDistance,
        int activeLimit) {
        if (hasInitialized) {
            return;
        }

        enemyPrefab = prefab;
        activationRadius = Mathf.Max(0f, activationDistance);
        deactivationRadius = Mathf.Max(activationRadius, deactivationDistance);
        maxActiveEnemies = Mathf.Max(0, activeLimit);

        int count = Mathf.Max(0, enemyCountPerBiome);
        biomeSpawnRules = new[] {
            CreateSpawnRule("Grassland", TerrainBiomeMask.Grassland, count),
            CreateSpawnRule("Desert", TerrainBiomeMask.Desert, count),
            CreateSpawnRule("Rainforest", TerrainBiomeMask.Rainforest, count),
            CreateSpawnRule("Mountain", TerrainBiomeMask.Mountain, count)
        };
    }

    public void SetActiveLimit(int activeLimit) {
        maxActiveEnemies = Mathf.Max(0, activeLimit);
        if (hasInitialized) {
            RefreshPopulation();
        }
    }

    public void StopPopulation() {
        populationEnabled = false;
        if (hasInitialized) {
            RefreshPopulation();
        }
    }

    public void SpawnEnemies(NetworkManager networkManager, int mapSeed) {
        if (hasInitialized) {
            return;
        }

        if (networkManager == null || !networkManager.IsServer) {
            return;
        }

        if (enemyPrefab == null) {
            Debug.LogError("[SeededEnemySpawner] Enemy prefab is missing.", this);
            return;
        }

        activeNetworkManager = networkManager;
        hasInitialized = true;

        GenerateSpawnRecords(mapSeed);
        RefreshPopulation();

        Debug.Log(
            $"[SeededEnemySpawner] Generated {spawnRecords.Count} deterministic enemy spawn record(s) using seed={mapSeed}.",
            this
        );
    }

    private void Update() {
        if (!hasInitialized ||
            activeNetworkManager == null ||
            !activeNetworkManager.IsServer ||
            Time.time < nextPopulationTickTime) {
            return;
        }

        nextPopulationTickTime = Time.time + Mathf.Max(0.1f, populationTickInterval);
        RefreshPopulation();
    }

    private void GenerateSpawnRecords(int mapSeed) {
        spawnRecords.Clear();
        recordsByHealth.Clear();

        int spawnIndex = 0;
        foreach (BiomeEnemySpawnRule rule in biomeSpawnRules) {
            if (rule == null || rule.enemyCount <= 0 || rule.biomeMask == 0) {
                continue;
            }

            for (int i = 0; i < rule.enemyCount; i++) {
                if (TryFindSpawnPosition(mapSeed, rule, spawnIndex, out Vector3 spawnPosition)) {
                    spawnRecords.Add(new EnemySpawnRecord {
                        ruleName = rule.name,
                        biomeMask = rule.biomeMask,
                        archetype = EnemyArchetypes.ForBiomeSpawn(rule.biomeMask, i,
                            CreateRandom(mapSeed, spawnIndex, rule.biomeMask).Next(100)),
                        position = spawnPosition
                    });
                }
                else {
                    Debug.LogWarning(
                        $"[SeededEnemySpawner] Could not find a valid {rule.name} spawn for enemy index {spawnIndex}.",
                        this
                    );
                }

                spawnIndex++;
            }
        }
    }

    private void RefreshPopulation() {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        int activeEnemyCount = CountActiveEnemies();
        int activatedCount = 0;
        int deactivatedCount = 0;
        float effectiveDeactivationRadius = Mathf.Max(activationRadius, deactivationRadius);

        foreach (EnemySpawnRecord record in spawnRecords) {
            if (record.isDefeated) {
                continue;
            }

            if (record.activeEnemy != null) {
                if (!populationEnabled ||
                    activeEnemyCount > maxActiveEnemies ||
                    !IsTerrainLoaded(record.activeEnemy.transform.position) ||
                    !IsAnyAlivePlayerWithin(
                    players,
                    record.activeEnemy.transform.position,
                    effectiveDeactivationRadius)) {
                    DeactivateEnemy(record);
                    activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
                    deactivatedCount++;
                }

                continue;
            }

            if (!populationEnabled) {
                continue;
            }

            if (activeEnemyCount >= maxActiveEnemies) {
                continue;
            }

            if (!IsAnyAlivePlayerWithin(players, record.position, activationRadius)) {
                continue;
            }

            if (!TryGetLoadedSpawnPosition(record.position, out Vector3 spawnPosition)) {
                continue;
            }

            SpawnEnemy(record, spawnPosition);
            activeEnemyCount++;
            activatedCount++;
        }

        if (activatedCount > 0 || deactivatedCount > 0) {
            Debug.Log(
                $"[SeededEnemySpawner] Activated {activatedCount}, deactivated {deactivatedCount}. Active={activeEnemyCount}/{spawnRecords.Count}.",
                this
            );
        }
    }

    private static BiomeEnemySpawnRule CreateSpawnRule(
        string ruleName,
        TerrainBiomeMask biomeMask,
        int enemyCount) {
        return new BiomeEnemySpawnRule {
            name = ruleName,
            biomeMask = biomeMask,
            enemyCount = enemyCount
        };
    }

    private int CountActiveEnemies() {
        int activeCount = 0;
        foreach (EnemySpawnRecord record in spawnRecords) {
            if (record.activeEnemy != null) {
                activeCount++;
            }
        }

        return activeCount;
    }

    private bool TryFindSpawnPosition(
        int mapSeed,
        BiomeEnemySpawnRule rule,
        int spawnIndex,
        out Vector3 spawnPosition
    ) {
        System.Random rng = CreateRandom(mapSeed, spawnIndex, rule.biomeMask);
        int attempts = Mathf.Max(1, attemptsPerEnemy);

        for (int attempt = 0; attempt < attempts; attempt++) {
            Vector3 candidate = BuildCandidatePosition(rng);
            if (CanSpawnAt(mapSeed, candidate, rule.biomeMask)) {
                spawnPosition = candidate;
                return true;
            }
        }

        spawnPosition = default;
        return false;
    }

    private Vector3 BuildCandidatePosition(System.Random rng) {
        float halfWorldSize = worldSize * 0.5f - 1f;
        float x = Mathf.Lerp(-halfWorldSize, halfWorldSize, (float)rng.NextDouble());
        float z = Mathf.Lerp(-halfWorldSize, halfWorldSize, (float)rng.NextDouble());
        return new Vector3(x, spawnCenter.y, z);
    }

    private bool CanSpawnAt(int mapSeed, Vector3 candidate, TerrainBiomeMask allowedBiomes) {
        if (!WorldBiomeUtility.IsWorldPositionInBounds(candidate, worldSize)) {
            return false;
        }

        Vector3 fromSpawnCenter = candidate - spawnCenter;
        fromSpawnCenter.y = 0f;
        if (fromSpawnCenter.sqrMagnitude < minimumDistanceFromSpawnCenter * minimumDistanceFromSpawnCenter) {
            return false;
        }

        TerrainBiomeMask biome = WorldBiomeUtility.GetBiomeAtWorldPosition(
            mapSeed,
            candidate,
            worldSize,
            biomeScale
        );
        return (allowedBiomes & biome) != 0;
    }

    private bool TryGetLoadedSpawnPosition(Vector3 candidate, out Vector3 spawnPosition) {
        TerrainController terrain = TerrainController.Instance;
        if (terrain == null) {
            spawnPosition = candidate;
            return true;
        }

        if (!terrain.TryGetClearGroundPosition(candidate, out Vector3 groundPosition)) {
            spawnPosition = default;
            return false;
        }

        spawnPosition = groundPosition + Vector3.up * 1.05f;
        return true;
    }

    private static bool IsTerrainLoaded(Vector3 position) {
        TerrainController terrain = TerrainController.Instance;
        return terrain == null || terrain.IsWorldPositionLoaded(position);
    }

    private void SpawnEnemy(EnemySpawnRecord record, Vector3 spawnPosition) {
        record.position = spawnPosition;
        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        enemyInstance.GetComponent<EnemyController>()?.ConfigureArchetype(record.archetype);
        record.activeEnemy = enemyInstance;

        EnemyHealthDrop enemyHealth = enemyInstance.GetComponent<EnemyHealthDrop>();
        if (enemyHealth != null) {
            enemyHealth.Died -= OnEnemyDied;
            enemyHealth.Died += OnEnemyDied;
            recordsByHealth[enemyHealth] = record;
        }

        NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
        if (networkObject != null) {
            networkObject.Spawn(true);
            return;
        }

        Debug.LogWarning(
            "[SeededEnemySpawner] Enemy prefab is missing NetworkObject. Clients will not see this enemy.",
            enemyInstance
        );
    }

    private void DeactivateEnemy(EnemySpawnRecord record) {
        GameObject enemyInstance = record.activeEnemy;
        record.activeEnemy = null;

        if (enemyInstance == null) {
            return;
        }

        EnemyHealthDrop enemyHealth = enemyInstance.GetComponent<EnemyHealthDrop>();
        if (enemyHealth != null) {
            enemyHealth.Died -= OnEnemyDied;
            recordsByHealth.Remove(enemyHealth);
        }

        NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned) {
            networkObject.Despawn(true);
            return;
        }

        Destroy(enemyInstance);
    }

    private void OnEnemyDied(EnemyHealthDrop enemyHealth) {
        if (enemyHealth == null || !recordsByHealth.TryGetValue(enemyHealth, out EnemySpawnRecord record)) {
            return;
        }

        record.isDefeated = true;
        record.activeEnemy = null;
        enemyHealth.Died -= OnEnemyDied;
        recordsByHealth.Remove(enemyHealth);
    }

    private static bool IsAnyAlivePlayerWithin(PlayerController[] players, Vector3 position, float radius) {
        float radiusSquared = radius * radius;
        foreach (PlayerController player in players) {
            if (player == null || !player.IsSpawned || !player.IsAlive) {
                continue;
            }

            Vector3 offset = player.transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSquared) {
                return true;
            }
        }

        return false;
    }

    private static System.Random CreateRandom(int mapSeed, int spawnIndex, TerrainBiomeMask biomeMask) {
        unchecked {
            int hash = 17;
            hash = hash * 31 + mapSeed;
            hash = hash * 31 + spawnIndex;
            hash = hash * 31 + (int)biomeMask;
            hash = hash * 31 + 0x45d9f3b;
            return new System.Random(hash & int.MaxValue);
        }
    }
}
