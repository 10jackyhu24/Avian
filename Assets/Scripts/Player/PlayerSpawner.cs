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
        // Register callback to spawn a player when a client connects
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
    }

    private void OnDestroy() {
        // Unregister callback on destruction
        if (NetworkManager.Singleton != null) {
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
        }
    }

    private void SpawnPlayer(ulong clientId) {
        // Only the server should perform spawning
        if (!IsServer)
            return;

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

        // Spawn the networked player and assign ownership
        networkObject.SpawnAsPlayerObject(clientId);

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
