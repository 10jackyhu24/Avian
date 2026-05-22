using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [Tooltip("World position where the player will spawn when the world is created.")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 5f, 10f);

    private GameObject spawnedPlayerInstance;

    public GameObject SpawnPlayerAtConfiguredPosition()
    {
        return SpawnPlayer(spawnPosition);
    }

    public GameObject SpawnPlayer(Vector3 position)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner is missing a player prefab reference.", this);
            return null;
        }

        PlayerController existingPlayer = FindFirstObjectByType<PlayerController>();
        if (existingPlayer != null)
        {
            Destroy(existingPlayer.gameObject);
        }

        spawnedPlayerInstance = Instantiate(playerPrefab, position, Quaternion.identity);

        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null)
        {
            cameraController.SetTarget(spawnedPlayerInstance.transform);
        }

        return spawnedPlayerInstance;
    }
}
