using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class CoinPickup : NetworkBehaviour {
    [SerializeField] private int coinValue = 1;
    [SerializeField] private float collectRadius = 1.25f;
    [SerializeField] private LayerMask playerLayerMask = ~0;

    private bool collected;

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
        if (playerCurrency == null) {
            return false;
        }

        collected = true;
        playerCurrency.ServerAddCoins(coinValue);
        Debug.Log($"[CoinPickup] Client {playerCurrency.OwnerClientId} collected {coinValue} coin(s).", this);

        if (IsSpawned) {
            NetworkObject.Despawn(true);
        }
        else {
            Destroy(gameObject);
        }

        return true;
    }

    private bool CanServerCollect() {
        if (collected) {
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
