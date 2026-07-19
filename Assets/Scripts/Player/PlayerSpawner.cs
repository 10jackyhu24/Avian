using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour {
    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("World position where the player will spawn when the world is created.")]
    [SerializeField]
    private Vector3 spawnPosition = new Vector3(0f, 5f, 10f);

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float enemySpawnDistance = 18f;

    private GameObject spawnedPlayerInstance;
    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private readonly Color fallbackEnemyColor = new Color(0.85f, 0.2f, 0.2f);

    private void Start() {
        if (NetworkManager.Singleton == null) {
            Debug.LogWarning("[PlayerSpawner] NetworkManager.Singleton not found.", this);
            return;
        }

        if (!NetworkManager.Singleton.IsServer) {
            Debug.Log("[PlayerSpawner] Not server, skipping spawn callback registration.", this);
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
        Debug.Log("[PlayerSpawner] Server spawn callback registered.", this);

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds) {
            SpawnPlayer(clientId);
        }
    }

    private void OnDestroy() {
        // Unregister callback on destruction
        if (NetworkManager.Singleton != null) {
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
        }
    }

    private void SpawnPlayer(ulong clientId) {
        // Only the server should perform spawning
        if (!IsServer) {
            return;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var connectedClient) &&
            connectedClient.PlayerObject != null) {
            Debug.Log($"[PlayerSpawner] Client {clientId} already has a player object.", this);
            return;
        }

        if (playerPrefab == null) {
            Debug.LogError(
                "PlayerSpawner is missing a player prefab reference.",
                this
            );

            return;
        }

        // Instantiate the player GameObject
        spawnedPlayerInstance =
            Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

        // Get the NetworkObject component
        NetworkObject networkObject =
            spawnedPlayerInstance.GetComponent<NetworkObject>();

        if (networkObject == null) {
            Debug.LogError("[PlayerSpawner] Player prefab is missing NetworkObject.", this);
            Destroy(spawnedPlayerInstance);
            return;
        }

        // Spawn the networked player and assign ownership
        networkObject.SpawnAsPlayerObject(clientId);
        Debug.Log($"[PlayerSpawner] Spawned player for client {clientId}.", this);

        // If the spawned player belongs to the local host, bind the camera to it
        if (clientId == NetworkManager.Singleton.LocalClientId) {
            CameraController cameraController =
                FindFirstObjectByType<CameraController>();

            if (cameraController != null) {
                cameraController.SetTarget(
                    spawnedPlayerInstance.transform
                );
            }
        }
    }
}
