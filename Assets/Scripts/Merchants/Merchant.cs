using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class Merchant : NetworkBehaviour {
    private const string PlaceholderName = "MerchantPlaceholder";

    [SerializeField] private MerchantDefinition definition;

    public MerchantDefinition Definition => definition;
    public string MerchantId => definition != null ? definition.merchantId : string.Empty;
    public float InteractionRadius => definition != null ? definition.interactionRadius : 0f;

    public void RequestPurchase(string itemId) {
        if (!IsSpawned || !IsClient || string.IsNullOrWhiteSpace(itemId)) {
            return;
        }

        RequestPurchaseRpc(new FixedString64Bytes(itemId));
    }

    public override void OnNetworkSpawn() {
        CreatePlaceholderVisual();

        if (definition == null) {
            Debug.LogError("[Merchant] Merchant definition is missing.", this);
            return;
        }

        Debug.Log(
            $"[Merchant] Spawned {definition.displayName} ({definition.merchantId}) " +
            $"at {transform.position}.",
            this
        );
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPurchaseRpc(
        FixedString64Bytes requestedItemId,
        RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string itemId = requestedItemId.ToString();

        if (!TryGetSellOffer(itemId, out MerchantOffer offer)) {
            RejectPurchase(clientId, $"{itemId} is not sold here");
            return;
        }

        if (!offer.unlimitedStock) {
            RejectPurchase(clientId, "limited stock is not implemented yet");
            return;
        }

        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null) {
            RejectPurchase(clientId, "player object was not found");
            return;
        }

        float distanceSquared = (client.PlayerObject.transform.position - transform.position).sqrMagnitude;
        if (distanceSquared > InteractionRadius * InteractionRadius) {
            RejectPurchase(clientId, "player is too far away");
            return;
        }

        if (!client.PlayerObject.TryGetComponent(out PlayerInventory inventory) ||
            !client.PlayerObject.TryGetComponent(out PlayerCurrency currency)) {
            RejectPurchase(clientId, "player inventory or currency was not found");
            return;
        }

        if (!inventory.ServerCanAddItem(itemId)) {
            RejectPurchase(clientId, "inventory does not have enough capacity");
            return;
        }

        if (offer.buyPrice > 0 && !currency.ServerTrySpendCoins(offer.buyPrice)) {
            RejectPurchase(clientId, "not enough coins");
            return;
        }

        if (!inventory.ServerTryAddItem(itemId)) {
            currency.ServerAddCoins(offer.buyPrice);
            RejectPurchase(clientId, "item could not be added");
            return;
        }

        Debug.Log(
            $"[Merchant] Client {clientId} bought {itemId} for {offer.buyPrice} coin(s).",
            this
        );
    }

    private bool TryGetSellOffer(string itemId, out MerchantOffer matchingOffer) {
        matchingOffer = null;
        if (definition == null || definition.offers == null) {
            return false;
        }

        foreach (MerchantOffer offer in definition.offers) {
            if (offer != null &&
                offer.merchantSellsThisItem &&
                offer.buyPrice >= 0 &&
                string.Equals(offer.itemId, itemId, StringComparison.Ordinal)) {
                matchingOffer = offer;
                return true;
            }
        }

        return false;
    }

    private void RejectPurchase(ulong clientId, string reason) {
        Debug.LogWarning($"[Merchant] Rejected purchase from client {clientId}: {reason}.", this);
    }

    private void CreatePlaceholderVisual() {
        if (transform.Find(PlaceholderName) != null) {
            return;
        }

        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        placeholder.name = PlaceholderName;
        placeholder.transform.SetParent(transform, false);
        placeholder.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        placeholder.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);

        if (placeholder.TryGetComponent(out Collider placeholderCollider)) {
            Destroy(placeholderCollider);
        }

        if (placeholder.TryGetComponent(out Renderer placeholderRenderer)) {
            placeholderRenderer.material.color = new Color(0.75f, 0.45f, 0.15f);
        }
    }
}
