using System;
using Unity.Netcode;
using UnityEngine;

public class EnemyHealthDrop : NetworkBehaviour {
    [Header("Health")]
    [SerializeField] private int maxHealth = 30;

    [Header("Health Display")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Coin Drop")]
    [SerializeField] private GameObject coinPickupPrefab;
    [SerializeField] private int minCoinsDropped = 1;
    [SerializeField] private int maxCoinsDropped = 3;
    [SerializeField] private float dropScatterRadius = 1.25f;
    [SerializeField] private float dropHeightOffset = 0.75f;

    private readonly NetworkVariable<float> networkHealth = new NetworkVariable<float>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );
    private float localHealth;
    private bool isDead;

    private float CurrentHealth => IsSpawned ? networkHealth.Value : localHealth;
    public bool IsAlive => !isDead && CurrentHealth > 0f;
    public event Action<EnemyHealthDrop> Died;

    private void Awake() {
        localHealth = maxHealth;
    }

    public override void OnNetworkSpawn() {
        if (IsServer) {
            networkHealth.Value = maxHealth;
        }

        localHealth = networkHealth.Value;
    }

    private void LateUpdate() {
        if (!IsAlive) {
            WorldHealthBarGUI.Remove(this);
            return;
        }

        WorldHealthBarGUI.Draw(
            this,
            transform.position + healthBarOffset,
            CurrentHealth,
            maxHealth,
            new Color(0.9f, 0.15f, 0.12f),
            width: 140f,
            height: 22f
        );
    }

    private void OnDisable() {
        WorldHealthBarGUI.Remove(this);
    }

    public void TakeDamage(float damage, ulong attackerClientId) {
        if (!CanServerTakeDamage() || isDead || damage <= 0) {
            return;
        }
        float newHealth = Mathf.Max(0f, CurrentHealth - damage);
        SetCurrentHealth(newHealth);
        Debug.Log($"[EnemyHealthDrop] {name} took {damage} damage from client {attackerClientId}. HP={newHealth}/{maxHealth}", this);

        if (newHealth <= 0f) {
            Die(attackerClientId);
        }
    }

    private void SetCurrentHealth(float value) {
        localHealth = value;
        if (IsSpawned && IsServer) {
            networkHealth.Value = value;
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
