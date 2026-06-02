using UnityEngine;
using Unity.Netcode;

public class PlayerSpawnerNetwork : NetworkBehaviour {
    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("World position where the player will spawn when the world is created.")]
    [SerializeField]
    private Vector3 spawnPosition = new Vector3(0f, 5f, 10f);

    private GameObject spawnedPlayerInstance;

    private void Start() {
        // Register callback to spawn a player when a client connects
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
    }

    private void OnDestroy() {
        // Unregister callback when this object is destroyed
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

        // Spawn the networked player object and assign ownership to the client
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