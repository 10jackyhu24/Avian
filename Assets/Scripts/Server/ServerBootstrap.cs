using System;
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

        TerrainController targetTerrain = terrainController != null
            ? terrainController
            : FindFirstObjectByType<TerrainController>();

        if (targetTerrain != null) {
            targetTerrain.GenerateWorld(seed);
        }

        Debug.Log($"[ServerBootstrap] Loaded room seed={seed}", this);
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
}
