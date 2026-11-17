using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour {
    public static readonly Vector3 DefaultSpawnPosition = new Vector3(0f, 5f, 10f);
    private static readonly Vector3[] PlayerSpawnPositions = {
        new Vector3(0f, 5f, 400f),
        new Vector3(400f, 5f, 0f),
        new Vector3(0f, 5f, -400f),
        new Vector3(-400f, 5f, 0f)
    };

    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("World position where the player will spawn when the world is created.")]
    [SerializeField] private Vector3 spawnPosition = DefaultSpawnPosition;

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    private const int SinglePlayerEnemyCountPerBiome = 1500;
    private const int SinglePlayerGraceActiveEnemies = 5;
    private const int SinglePlayerWaveActiveEnemies = 20;
    private const float SinglePlayerEnemyActivationRadius = 42f;
    private const float SinglePlayerEnemyDeactivationRadius = 45f;

    private bool serverSpawnInitialized;
    private static readonly Dictionary<NetworkManager, System.Action<ulong>> playerSpawnCallbacks =
        new Dictionary<NetworkManager, System.Action<ulong>>();
    private static readonly Dictionary<NetworkManager, Queue<Vector3>> playerSpawnPointPools =
        new Dictionary<NetworkManager, Queue<Vector3>>();

    private void Start() {
        if (NetworkManager.Singleton == null) {
            Debug.LogWarning("[PlayerSpawner] NetworkManager.Singleton not found.", this);
            return;
        }

        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

        TryInitializeServerSpawns();
    }

    private void HandleServerStarted() {
        TryInitializeServerSpawns();
    }

    private void TryInitializeServerSpawns() {
        if (serverSpawnInitialized || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) {
            return;
        }

        serverSpawnInitialized = true;
        DroppedItemRegistry.Reset();
        DroppedItemStreamer.EnsureExists(NetworkManager.Singleton);
        RegisterPlayerSpawning(NetworkManager.Singleton, playerPrefab, spawnPosition, this);
        SpawnEnemies();
        MerchantSpawner.TrySpawnOriginMerchant(NetworkManager.Singleton, this);
        Debug.Log("[PlayerSpawner] Server spawn callback registered.", this);
    }

    private void OnDestroy() {
        if (NetworkManager.Singleton != null) {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
            UnregisterPlayerSpawning(NetworkManager.Singleton);
        }
    }

    public static void RegisterPlayerSpawning(NetworkManager networkManager, GameObject playerPrefab, Vector3 spawnPosition, MonoBehaviour context = null) {
        if (networkManager == null || !networkManager.IsServer) {
            return;
        }

        UnregisterPlayerSpawning(networkManager);
        playerSpawnPointPools[networkManager] = CreateShuffledSpawnPointPool();

        System.Action<ulong> callback = clientId => SpawnPlayerForClient(networkManager, playerPrefab, spawnPosition, clientId, context);
        playerSpawnCallbacks[networkManager] = callback;
        networkManager.OnClientConnectedCallback += callback;

        foreach (ulong clientId in networkManager.ConnectedClientsIds) {
            callback(clientId);
        }
    }

    public static void UnregisterPlayerSpawning(NetworkManager networkManager) {
        if (networkManager == null) {
            return;
        }

        if (playerSpawnCallbacks.TryGetValue(networkManager, out System.Action<ulong> callback)) {
            networkManager.OnClientConnectedCallback -= callback;
            playerSpawnCallbacks.Remove(networkManager);
        }

        playerSpawnPointPools.Remove(networkManager);
    }

    public static void SpawnPlayerForClient(NetworkManager networkManager, GameObject playerPrefab, Vector3 spawnPosition, ulong clientId, MonoBehaviour context = null) {
        if (networkManager == null || !networkManager.IsServer) {
            return;
        }

        if (networkManager.ConnectedClients.TryGetValue(clientId, out var connectedClient) &&
            connectedClient.PlayerObject != null) {
            Vector3 assignedSpawnPosition = GetNextSpawnPosition(networkManager, spawnPosition);
            PlayerController playerController = connectedClient.PlayerObject.GetComponent<PlayerController>();
            if (playerController != null) {
                playerController.SetSpawnPositionOnServer(assignedSpawnPosition);
            }
            else {
                connectedClient.PlayerObject.transform.position = assignedSpawnPosition;
            }

            ZoneManager.EnsureServerZone(networkManager);
            if (context != null) {
                Debug.Log($"[PlayerSpawner] Placed existing player for client {clientId} at {assignedSpawnPosition}.", context);
            }

            return;
        }

        if (playerPrefab == null) {
            Debug.LogError("[PlayerSpawner] Player prefab is missing.", context);
            return;
        }

        Vector3 selectedSpawnPosition = GetNextSpawnPosition(networkManager, spawnPosition);
        GameObject spawnedPlayerInstance = Object.Instantiate(playerPrefab, selectedSpawnPosition, Quaternion.identity);
        NetworkObject networkObject = spawnedPlayerInstance.GetComponent<NetworkObject>();
        if (networkObject == null) {
            Debug.LogError("[PlayerSpawner] Player prefab is missing NetworkObject.", context);
            Object.Destroy(spawnedPlayerInstance);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId);
        ZoneManager.EnsureServerZone(networkManager);

        if (context != null) {
            Debug.Log($"[PlayerSpawner] Spawned player for client {clientId} at {selectedSpawnPosition}.", context);
        }

        if (clientId == networkManager.LocalClientId) {
            CameraController cameraController = Object.FindFirstObjectByType<CameraController>();
            if (cameraController != null) {
                cameraController.SetTarget(spawnedPlayerInstance.transform);
            }
        }
    }

    private static Vector3 GetNextSpawnPosition(NetworkManager networkManager, Vector3 fallbackPosition) {
        if (!playerSpawnPointPools.TryGetValue(networkManager, out Queue<Vector3> spawnPoints) ||
            spawnPoints.Count == 0) {
            spawnPoints = CreateShuffledSpawnPointPool();
            playerSpawnPointPools[networkManager] = spawnPoints;
        }

        return spawnPoints.Count > 0 ? spawnPoints.Dequeue() : fallbackPosition;
    }

    private static Queue<Vector3> CreateShuffledSpawnPointPool() {
        List<Vector3> spawnPoints = new List<Vector3>(PlayerSpawnPositions);
        for (int i = spawnPoints.Count - 1; i > 0; i--) {
            int swapIndex = Random.Range(0, i + 1);
            (spawnPoints[i], spawnPoints[swapIndex]) = (spawnPoints[swapIndex], spawnPoints[i]);
        }

        return new Queue<Vector3>(spawnPoints);
    }

    private void SpawnEnemies() {
        if (enemyPrefab == null || NetworkManager.Singleton == null) {
            return;
        }

        SeededEnemySpawner seededSpawner = GetComponent<SeededEnemySpawner>();
        if (seededSpawner == null) {
            seededSpawner = gameObject.AddComponent<SeededEnemySpawner>();
        }

        seededSpawner.ConfigurePopulation(
            enemyPrefab,
            SinglePlayerEnemyCountPerBiome,
            SinglePlayerEnemyActivationRadius,
            SinglePlayerEnemyDeactivationRadius,
            SinglePlayerGraceActiveEnemies
        );

        int seed = TerrainController.Instance != null
            ? TerrainController.Instance.WorldSeed
            : GameState.currentSeed;
        seededSpawner.SpawnEnemies(NetworkManager.Singleton, seed);
        SinglePlayerMatchController.EnsureExists(
            gameObject,
            seededSpawner,
            enemyPrefab,
            SinglePlayerGraceActiveEnemies,
            SinglePlayerWaveActiveEnemies
        );
    }
}
