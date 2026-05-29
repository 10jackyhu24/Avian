using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour {
    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [Tooltip("World position where the player will spawn when the world is created.")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 5f, 10f);

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float enemySpawnDistance = 18f;

    private GameObject spawnedPlayerInstance;
    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private readonly Color fallbackEnemyColor = new Color(0.85f, 0.2f, 0.2f);

    public GameObject SpawnPlayerAtConfiguredPosition() {
        return SpawnPlayer(spawnPosition);
    }

    public GameObject SpawnPlayer(Vector3 position) {
        if (playerPrefab == null) {
            Debug.LogError("PlayerSpawner is missing a player prefab reference.", this);
            return null;
        }

        PlayerController existingPlayer = FindFirstObjectByType<PlayerController>();
        if (existingPlayer != null) {
            Destroy(existingPlayer.gameObject);
        }

        spawnedPlayerInstance = Instantiate(playerPrefab, position, Quaternion.identity);
        spawnedPlayerInstance.tag = "Player";

        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null) {
            cameraController.SetTarget(spawnedPlayerInstance.transform);
        }

        return spawnedPlayerInstance;
    }

    public void SpawnSinglePlayerEnemies() {
        ClearSpawnedEnemies();

        if (enemyCount <= 0) {
            return;
        }

        for (int i = 0; i < enemyCount; i++) {
            float angle = (Mathf.PI * 2f * i) / enemyCount;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * enemySpawnDistance;
            Vector3 enemyPosition = spawnPosition + offset;

            GameObject enemyInstance = SpawnEnemy(enemyPosition, i);
            if (enemyInstance != null) {
                spawnedEnemies.Add(enemyInstance);
            }
        }
    }

    private GameObject SpawnEnemy(Vector3 position, int enemyIndex) {
        GameObject enemyInstance = enemyPrefab != null
            ? Instantiate(enemyPrefab, position, Quaternion.identity)
            : CreateFallbackEnemy(position, enemyIndex);

        if (enemyInstance == null) {
            return null;
        }

        enemyInstance.name = $"Enemy_{enemyIndex + 1}";

        CharacterController characterController = enemyInstance.GetComponent<CharacterController>();
        if (characterController == null) {
            characterController = enemyInstance.AddComponent<CharacterController>();
        }

        characterController.height = 2f;
        characterController.radius = 0.5f;
        characterController.center = Vector3.zero;
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;

        if (enemyInstance.GetComponent<EnemyController>() == null) {
            enemyInstance.AddComponent<EnemyController>();
        }

        return enemyInstance;
    }

    private GameObject CreateFallbackEnemy(Vector3 position, int enemyIndex) {
        GameObject enemyInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyInstance.name = $"Enemy_{enemyIndex + 1}";
        enemyInstance.transform.position = position;

        Collider primitiveCollider = enemyInstance.GetComponent<Collider>();
        if (primitiveCollider != null) {
            primitiveCollider.enabled = false;
        }

        Renderer renderer = enemyInstance.GetComponent<Renderer>();
        if (renderer != null) {
            renderer.material.color = fallbackEnemyColor;
        }

        return enemyInstance;
    }

    private void ClearSpawnedEnemies() {
        for (int i = 0; i < spawnedEnemies.Count; i++) {
            if (spawnedEnemies[i] != null) {
                Destroy(spawnedEnemies[i]);
            }
        }

        spawnedEnemies.Clear();
    }
}
