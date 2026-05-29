using UnityEngine;
using Unity.Netcode;

public class PlayerSpawnerNetwork : NetworkBehaviour
{
    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("World position where the player will spawn when the world is created.")]
    [SerializeField]
    private Vector3 spawnPosition = new Vector3(0f, 5f, 10f);

    private GameObject spawnedPlayerInstance;

    private void Start()
    {
        // Client 連入時生成玩家
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        // 只有 Server 可以 Spawn
        if (!IsServer)
            return;

        if (playerPrefab == null)
        {
            Debug.LogError(
                "PlayerSpawner is missing a player prefab reference.",
                this
            );

            return;
        }

        // 生成玩家
        spawnedPlayerInstance =
            Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

        // 取得 NetworkObject
        NetworkObject networkObject =
            spawnedPlayerInstance.GetComponent<NetworkObject>();

        // NGO 玩家生成
        networkObject.SpawnAsPlayerObject(clientId);

        // Host 自己的 Camera 綁定
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            CameraController cameraController =
                FindFirstObjectByType<CameraController>();

            if (cameraController != null)
            {
                cameraController.SetTarget(
                    spawnedPlayerInstance.transform
                );
            }
        }
    }
}