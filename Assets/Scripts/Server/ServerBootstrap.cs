using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DisallowMultipleComponent]
public class ServerBootstrap : MonoBehaviour {
    [SerializeField] private bool startServerOnStart = true;
    [SerializeField] private ushort defaultPort = 7777;
    [SerializeField] private string listenAddress = "0.0.0.0";
    [SerializeField] private int fallbackSeed = 100000;
    [SerializeField] private TerrainController terrainController;

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float enemySpawnDistance = 18f;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();

    private void Start() {
        if (!startServerOnStart) {
            return;
        }

        if (NetworkManager.Singleton == null) {
            Debug.LogError("[ServerBootstrap] NetworkManager.Singleton not found.", this);
            return;
        }

        if (NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer) {
            Debug.Log("[ServerBootstrap] Client instance detected; server startup skipped.", this);
            return;
        }

        LoadSeed();
        ConfigureTransport();
        NetworkUtility.DisableSceneManagement(NetworkManager.Singleton);

        if (!NetworkManager.Singleton.IsListening) {
            bool started = NetworkManager.Singleton.StartServer();
            Debug.Log($"[ServerBootstrap] StartServer={started}", this);
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        PlayerSpawner.RegisterPlayerSpawning(
            NetworkManager.Singleton,
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab,
            PlayerSpawner.DefaultSpawnPosition,
            this
        );

        SpawnEnemies();
    }

    private void OnDestroy() {
        if (NetworkManager.Singleton == null) {
            return;
        }

        PlayerSpawner.UnregisterPlayerSpawning(NetworkManager.Singleton);
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId) {
        Debug.Log($"[ServerBootstrap] Client connected: {clientId}", this);
    }

    private void OnClientDisconnected(ulong clientId) {
        Debug.Log($"[ServerBootstrap] Client disconnected: {clientId}", this);
    }

    private void LoadSeed() {
        string mapSeed = Environment.GetEnvironmentVariable("MAP_SEED");
        int seed = int.TryParse(mapSeed, out int parsedSeed)
            ? parsedSeed
            : fallbackSeed;

        GameState.SetWorld(GameState.currentRoomId, seed);

        TerrainController targetTerrain = ResolveTerrainController();

        if (targetTerrain != null) {
            targetTerrain.GenerateWorld(seed);
        } else {
            Debug.LogError("[ServerBootstrap] TerrainController not found. Dedicated server will have no collidable world.", this);
        }

        Debug.Log($"[ServerBootstrap] Loaded room seed={seed}", this);
    }

    private TerrainController ResolveTerrainController() {
        if (terrainController != null) {
            return terrainController;
        }

        TerrainController[] terrainControllers = FindObjectsByType<TerrainController>(FindObjectsSortMode.None);
        if (terrainControllers.Length > 0) {
            terrainController = terrainControllers[0];
        }

        return terrainController;
    }

    private void ConfigureTransport() {
        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is not UnityTransport unityTransport) {
            Debug.LogWarning("[ServerBootstrap] NetworkManager transport is not UnityTransport.", this);
            return;
        }

        ushort port = defaultPort;
        string envPort = Environment.GetEnvironmentVariable("GAME_PORT");
        if (ushort.TryParse(envPort, out ushort parsedPort)) {
            port = parsedPort;
        }

        unityTransport.SetConnectionData(listenAddress, port, listenAddress);
        Debug.Log($"[ServerBootstrap] Listening on {listenAddress}:{port}", this);
    }

    private void SpawnEnemies() {
        if (!NetworkManager.Singleton.IsServer || enemyPrefab == null || enemyCount <= 0 || spawnedEnemies.Count > 0) {
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
                Debug.LogWarning("[ServerBootstrap] Enemy prefab is missing NetworkObject. Clients will not see this enemy.", this);
            }
        }

        Debug.Log($"[ServerBootstrap] Spawned {spawnedEnemies.Count} enemy/enemies.", this);
    }

    private Vector3 GetEnemySpawnPosition(int index) {
        float angle = enemyCount <= 0 ? 0f : (Mathf.PI * 2f * index) / enemyCount;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * enemySpawnDistance;
        return PlayerSpawner.DefaultSpawnPosition + offset;
    }
}
