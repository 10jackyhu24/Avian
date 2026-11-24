using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class CoinPickup : NetworkBehaviour {
    [SerializeField] private int coinValue = 1;
    [SerializeField] private float collectRadius = 1.25f;
    [SerializeField] private LayerMask playerLayerMask = ~0;

    private bool collected;
    private WorldLootVisual lootVisual;
    private float pickupReadyAt;
    private readonly NetworkVariable<int> stackValue = new NetworkVariable<int>(1,
        readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

    public int Amount => IsSpawned ? stackValue.Value : coinValue;

    private void Awake() {
        pickupReadyAt = Time.time + 0.5f;
        lootVisual = WorldLootVisual.Ensure(gameObject, GetComponentInChildren<Renderer>(), coinValue, 1.2f);
    }

    public override void OnNetworkSpawn() {
        if (IsServer) stackValue.Value = Mathf.Max(1, coinValue);
        stackValue.OnValueChanged += HandleValueChanged;
        lootVisual?.SetCount(Amount);
    }

    public override void OnNetworkDespawn() {
        stackValue.OnValueChanged -= HandleValueChanged;
    }

    private void HandleValueChanged(int previous, int current) {
        lootVisual?.SetCount(current);
    }

    private void SetAmount(int amount) {
        coinValue = Mathf.Max(1, amount);
        if (IsSpawned && IsServer) stackValue.Value = coinValue;
        lootVisual?.SetCount(coinValue);
    }

    public static bool SpawnStack(GameObject prefab, Vector3 position, int amount) {
        if (prefab == null || amount <= 0 || NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer) return false;

        if (GroundPlacement.TryFindDropGround(position, out Vector3 ground)) position = ground + Vector3.up * 0.05f;
        // Merge nearby currency without changing the total awarded on pickup.
        foreach (CoinPickup existing in FindObjectsByType<CoinPickup>(FindObjectsSortMode.None)) {
            Vector3 delta = existing.transform.position - position;
            if (Mathf.Abs(delta.y) > 0.75f) continue;
            delta.y = 0f;
            if (!existing.collected && delta.sqrMagnitude <= 2.25f && existing.Amount <= int.MaxValue - amount) {
                existing.SetAmount(existing.Amount + amount);
                return true;
            }
        }
        GameObject coin = Instantiate(prefab, position, Quaternion.identity);
        CoinPickup pickup = coin.GetComponent<CoinPickup>();
        if (pickup == null) {
            Destroy(coin);
            return false;
        }
        pickup.SetAmount(amount);
        NetworkObject coinNetworkObject = coin.GetComponent<NetworkObject>();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
            if (coinNetworkObject == null) {
                Destroy(coin);
                return false;
            }
            coinNetworkObject.Spawn(true);
        }
        return true;
    }

    private void Reset() {
        Collider pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
    }

    private void Update() {
        if (!CanServerCollect()) {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            collectRadius,
            playerLayerMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits) {
            if (TryCollect(hit)) {
                return;
            }
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (CanServerCollect()) {
            TryCollect(other);
        }
    }

    private bool TryCollect(Collider other) {
        if (collected || other == null) {
            return false;
        }

        PlayerCurrency playerCurrency = other.GetComponentInParent<PlayerCurrency>();
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (playerCurrency == null || player == null || !player.IsAlive) {
            return false;
        }

        collected = true;
        playerCurrency.ServerAddCoins(Amount);
        Debug.Log($"[CoinPickup] Client {playerCurrency.OwnerClientId} collected {Amount} coin(s).", this);

        if (IsSpawned) {
            NetworkObject.Despawn(true);
        }
        else {
            Destroy(gameObject);
        }

        return true;
    }

    private bool CanServerCollect() {
        if (collected || Time.time < pickupReadyAt) {
            return false;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
            return IsServer;
        }

        return true;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectRadius);
    }
}
