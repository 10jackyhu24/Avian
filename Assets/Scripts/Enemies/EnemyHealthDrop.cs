using System;
using Unity.Netcode;
using UnityEngine;

public class EnemyHealthDrop : MonoBehaviour {
    [Header("Health")]
    [SerializeField] private int maxHealth = 30;

    [Header("Coin Drop")]
    [SerializeField] private GameObject coinPickupPrefab;
    [SerializeField] private int minCoinsDropped = 1;
    [SerializeField] private int maxCoinsDropped = 3;
    [SerializeField] private float dropScatterRadius = 1.25f;
    [SerializeField] private float dropHeightOffset = 0.75f;

    private int currentHealth;
    private bool isDead;

    public bool IsAlive => !isDead && currentHealth > 0;
    public event Action<EnemyHealthDrop> Died;

    private void Awake() {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage, ulong attackerClientId) {
        if (!CanServerTakeDamage() || isDead || damage <= 0) {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log($"[EnemyHealthDrop] {name} took {damage} damage from client {attackerClientId}. HP={currentHealth}/{maxHealth}", this);

        if (currentHealth <= 0) {
            Die(attackerClientId);
        }
    }

    private void Die(ulong attackerClientId) {
        if (isDead) {
            return;
        }

        isDead = true;
        Died?.Invoke(this);
        DropCoins();
        Debug.Log($"[EnemyHealthDrop] {name} defeated by client {attackerClientId}.", this);

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned) {
            networkObject.Despawn(true);
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
            Debug.LogWarning("[EnemyHealthDrop] Enemy is not network-spawned. The server destroyed it locally, but clients may not see the enemy disappear.", this);
        }

        Destroy(gameObject);
    }

    private void DropCoins() {
        if (coinPickupPrefab == null) {
            Debug.LogWarning("[EnemyHealthDrop] Coin Pickup Prefab is not assigned.", this);
            return;
        }

        int coinCount = UnityEngine.Random.Range(
            Mathf.Min(minCoinsDropped, maxCoinsDropped),
            Mathf.Max(minCoinsDropped, maxCoinsDropped) + 1
        );

        for (int i = 0; i < coinCount; i++) {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * dropScatterRadius;
            Vector3 spawnPosition = transform.position + new Vector3(offset.x, dropHeightOffset, offset.y);
            GameObject coin = Instantiate(coinPickupPrefab, spawnPosition, Quaternion.identity);

            NetworkObject coinNetworkObject = coin.GetComponent<NetworkObject>();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
                if (coinNetworkObject != null) {
                    coinNetworkObject.Spawn(true);
                }
                else {
                    Debug.LogWarning("[EnemyHealthDrop] Coin prefab is missing NetworkObject, so multiplayer clients will not see the spawned coin.", coin);
                }
            }
        }
    }

    private bool CanServerTakeDamage() {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
            return NetworkManager.Singleton.IsServer;
        }

        return true;
    }
}
