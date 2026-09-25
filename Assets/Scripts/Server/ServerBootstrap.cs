using System;
using System.Collections;
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
    [SerializeField] private SeededEnemySpawner enemySpawner;

    [Header("Server Lifetime")]
    [SerializeField] private bool quitWhenEmpty = true;
    [SerializeField] private float emptyServerQuitDelaySeconds = 5f;

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float enemySpawnDistance = 18f;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private int activeSeed;
    private string activeRoomId = string.Empty;
    private bool hasAcceptedClient;
    private Coroutine emptyServerQuitCoroutine;

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

        LoadRoomSettings();
        ConfigureTransport();
        NetworkUtility.DisableSceneManagement(NetworkManager.Singleton);

        if (!NetworkManager.Singleton.IsListening) {
            bool started = NetworkManager.Singleton.StartServer();
            Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} StartServer={started}", this);
        }

        DroppedItemRegistry.Reset();
        DroppedItemStreamer.EnsureExists(NetworkManager.Singleton);

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
        hasAcceptedClient = true;

        if (emptyServerQuitCoroutine != null) {
            StopCoroutine(emptyServerQuitCoroutine);
            emptyServerQuitCoroutine = null;
        }

        Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Client connected: {clientId}", this);
    }

    private void OnClientDisconnected(ulong clientId) {
        Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Client disconnected: {clientId}", this);
        ScheduleQuitIfServerIsEmpty();
    }

    private void LoadRoomSettings() {
        activeRoomId = Environment.GetEnvironmentVariable("ROOM_ID");
        if (string.IsNullOrWhiteSpace(activeRoomId)) {
            activeRoomId = string.IsNullOrWhiteSpace(GameState.currentRoomId)
                ? "local-room"
                : GameState.currentRoomId;
        }

        string mapSeed = Environment.GetEnvironmentVariable("MAP_SEED");
        activeSeed = int.TryParse(mapSeed, out int parsedSeed)
            ? parsedSeed
            : fallbackSeed;

        GameState.SetWorld(activeRoomId, activeSeed);

        TerrainController targetTerrain = ResolveTerrainController();

        if (targetTerrain != null) {
            targetTerrain.GenerateWorld(activeSeed);
        } else {
            Debug.LogError("[ServerBootstrap] TerrainController not found. Dedicated server will have no collidable world.", this);
        }

        Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Loaded seed={activeSeed}", this);
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
        Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Listening on {listenAddress}:{port}", this);
    }

    private void SpawnEnemies() {
        SeededEnemySpawner targetEnemySpawner = ResolveEnemySpawner();
        if (targetEnemySpawner != null) {
            targetEnemySpawner.SpawnEnemies(NetworkManager.Singleton, activeSeed);
            return;
        }

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

        Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Spawned {spawnedEnemies.Count} enemy/enemies.", this);
    }

    private Vector3 GetEnemySpawnPosition(int index) {
        float angle = enemyCount <= 0 ? 0f : (Mathf.PI * 2f * index) / enemyCount;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * enemySpawnDistance;
        return PlayerSpawner.DefaultSpawnPosition + offset;
    }

    private SeededEnemySpawner ResolveEnemySpawner() {
        if (enemySpawner != null) {
            return enemySpawner;
        }

        enemySpawner = GetComponent<SeededEnemySpawner>();
        if (enemySpawner != null) {
            return enemySpawner;
        }

        enemySpawner = FindFirstObjectByType<SeededEnemySpawner>();
        return enemySpawner;
    }

    private void ScheduleQuitIfServerIsEmpty() {
        if (!quitWhenEmpty ||
            !hasAcceptedClient ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer ||
            NetworkManager.Singleton.ConnectedClientsIds.Count > 0 ||
            emptyServerQuitCoroutine != null) {
            return;
        }

        emptyServerQuitCoroutine = StartCoroutine(QuitWhenEmptyAfterDelay());
    }

    private IEnumerator QuitWhenEmptyAfterDelay() {
        float delaySeconds = Mathf.Max(0f, emptyServerQuitDelaySeconds);
        Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Server is empty. Quitting in {delaySeconds:0.##} second(s).", this);

        if (delaySeconds > 0f) {
            yield return new WaitForSeconds(delaySeconds);
        }

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer &&
            NetworkManager.Singleton.ConnectedClientsIds.Count > 0) {
            Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Quit cancelled because a client reconnected.", this);
            emptyServerQuitCoroutine = null;
            yield break;
        }

        Debug.Log($"[ServerBootstrap] room={ActiveRoomLabel} Empty server quitting.", this);
        Application.Quit();
    }

    private string ActiveRoomLabel => string.IsNullOrWhiteSpace(activeRoomId)
        ? "unassigned"
        : activeRoomId;
}
