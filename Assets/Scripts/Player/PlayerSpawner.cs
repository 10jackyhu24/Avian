using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour {
    public static readonly Vector3 DefaultSpawnPosition = new Vector3(0f, 5f, 10f);

    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("World position where the player will spawn when the world is created.")]
    [SerializeField] private Vector3 spawnPosition = DefaultSpawnPosition;

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float enemySpawnDistance = 18f;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private bool serverSpawnInitialized;
    private readonly Color fallbackEnemyColor = new Color(0.85f, 0.2f, 0.2f);
    private static readonly Dictionary<NetworkManager, System.Action<ulong>> playerSpawnCallbacks =
        new Dictionary<NetworkManager, System.Action<ulong>>();

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

        if (!playerSpawnCallbacks.TryGetValue(networkManager, out System.Action<ulong> callback)) {
            return;
        }

        networkManager.OnClientConnectedCallback -= callback;
        playerSpawnCallbacks.Remove(networkManager);
    }

    public static void SpawnPlayerForClient(NetworkManager networkManager, GameObject playerPrefab, Vector3 spawnPosition, ulong clientId, MonoBehaviour context = null) {
        if (networkManager == null || !networkManager.IsServer) {
            return;
        }

        if (networkManager.ConnectedClients.TryGetValue(clientId, out var connectedClient) &&
            connectedClient.PlayerObject != null) {
            if (context != null) {
                Debug.Log($"[PlayerSpawner] Client {clientId} already has a player object.", context);
            }

            return;
        }

        if (playerPrefab == null) {
            Debug.LogError("[PlayerSpawner] Player prefab is missing.", context);
            return;
        }

        GameObject spawnedPlayerInstance = Object.Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = spawnedPlayerInstance.GetComponent<NetworkObject>();
        if (networkObject == null) {
            Debug.LogError("[PlayerSpawner] Player prefab is missing NetworkObject.", context);
            Object.Destroy(spawnedPlayerInstance);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId);

        if (context != null) {
            Debug.Log($"[PlayerSpawner] Spawned player for client {clientId}.", context);
        }

        if (clientId == networkManager.LocalClientId) {
            CameraController cameraController = Object.FindFirstObjectByType<CameraController>();
            if (cameraController != null) {
                cameraController.SetTarget(spawnedPlayerInstance.transform);
            }
        }
    }

    private void SpawnEnemies() {
        if (enemyPrefab == null || enemyCount <= 0) {
            return;
        }

        for (int i = 0; i < enemyCount; i++) {
            Vector3 enemySpawnPosition = GetEnemySpawnPosition(i);
            GameObject enemyInstance = Instantiate(enemyPrefab, enemySpawnPosition, Quaternion.identity);
            spawnedEnemies.Add(enemyInstance);

            NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
            if (networkObject != null) {
                networkObject.Spawn(true);
            }
            else {
                Debug.LogWarning("[PlayerSpawner] Enemy prefab is missing NetworkObject. Multiplayer clients will not see this enemy.", this);
            }
        }

        Debug.Log($"[PlayerSpawner] Spawned {spawnedEnemies.Count} enemy/enemies.", this);
    }

    private Vector3 GetEnemySpawnPosition(int index) {
        float angle = enemyCount <= 0 ? 0f : (Mathf.PI * 2f * index) / enemyCount;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * enemySpawnDistance;
        return spawnPosition + offset;
    }
}
