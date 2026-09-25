using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Pickupable))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkDroppedItem : NetworkBehaviour {
    [SerializeField] private SpriteRenderer itemRenderer;
    [SerializeField] private Pickupable pickupable;

    private Rigidbody itemRigidbody;
    private bool hasSettled;

    private readonly NetworkVariable<InventoryItemState> itemState =
        new NetworkVariable<InventoryItemState>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

    public InventoryItemState ItemState => itemState.Value;

    private void Awake() {
        ResolveReferences();
    }

    public override void OnNetworkSpawn() {
        itemState.OnValueChanged += HandleItemStateChanged;
        ApplyItemState(itemState.Value);
        hasSettled = false;
    }

    public override void OnNetworkDespawn() {
        itemState.OnValueChanged -= HandleItemStateChanged;
    }

    private void FixedUpdate() {
        if (!IsServer || hasSettled || itemRigidbody == null ||
            itemRigidbody.isKinematic || !itemRigidbody.IsSleeping() ||
            !itemState.Value.IsValid) {
            return;
        }

        hasSettled = true;
        DroppedItemRegistry.TryUpdatePosition(
            itemState.Value.InstanceId,
            this,
            transform.position
        );
        itemRigidbody.isKinematic = true;

        Debug.Log(
            $"[NetworkDroppedItem] Settled {itemState.Value.ItemId} " +
            $"(instance {itemState.Value.InstanceId}) at {transform.position}.",
            this
        );
    }

    public void SetItemStateOnServer(InventoryItemState value) {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !value.IsValid) {
            return;
        }

        itemState.Value = value;
        ApplyItemState(value);
    }

    private void HandleItemStateChanged(InventoryItemState previousValue, InventoryItemState newValue) {
        ApplyItemState(newValue);
    }

    private void ApplyItemState(InventoryItemState value) {
        ResolveReferences();

        bool hasValidItem = pickupable != null && pickupable.SetNetworkState(value);
        if (itemRenderer == null) {
            return;
        }

        itemRenderer.sprite = hasValidItem ? pickupable.ItemData.itemObject.icon : null;
        itemRenderer.enabled = itemRenderer.sprite != null;
    }

    private void ResolveReferences() {
        if (pickupable == null) {
            pickupable = GetComponent<Pickupable>();
        }

        if (itemRenderer == null) {
            itemRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (itemRigidbody == null) {
            itemRigidbody = GetComponent<Rigidbody>();
        }
    }

    public static bool TrySpawnOnServer(InventoryItemState state, Vector3 playerPosition) {
        Vector3 spawnPosition = FindGroundedSpawnPosition(playerPosition);
        if (!TryCreateOnServer(state, spawnPosition, out NetworkDroppedItem droppedItem)) {
            return false;
        }

        if (!DroppedItemRegistry.TryRegister(state, spawnPosition, droppedItem)) {
            Debug.LogError($"[NetworkDroppedItem] Could not register item instance {state.InstanceId}.");
            droppedItem.NetworkObject.Despawn(true);
            return false;
        }

        Debug.Log($"[NetworkDroppedItem] Spawned {state.ItemId} x{state.Count} at {spawnPosition}.");
        return true;
    }

    public static bool TryRespawnOnServer(DroppedItemRecord record) {
        if (record == null || record.SpawnedItem != null ||
            !TryCreateOnServer(record.ItemState, record.Position, out NetworkDroppedItem droppedItem)) {
            return false;
        }

        if (!DroppedItemRegistry.TrySetSpawnedItem(
                record.ItemState.InstanceId,
                expectedCurrentItem: null,
                newSpawnedItem: droppedItem)) {
            droppedItem.NetworkObject.Despawn(true);
            return false;
        }

        return true;
    }

    private static bool TryCreateOnServer(
        InventoryItemState state,
        Vector3 position,
        out NetworkDroppedItem droppedItem) {
        droppedItem = null;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || !state.IsValid) {
            return false;
        }

        GameObject prefab = Resources.Load<GameObject>("NetworkDroppedItem");
        if (prefab == null) {
            Debug.LogError("[NetworkDroppedItem] Prefab was not found in Resources.");
            return false;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        if (!instance.TryGetComponent(out NetworkObject networkObject) ||
            !instance.TryGetComponent(out droppedItem)) {
            Debug.LogError("[NetworkDroppedItem] Prefab is missing required network components.", instance);
            Destroy(instance);
            droppedItem = null;
            return false;
        }

        networkObject.Spawn(true);
        droppedItem.SetItemStateOnServer(state);
        return true;
    }

    private static Vector3 FindGroundedSpawnPosition(Vector3 playerPosition) {
        Vector3 spawnPosition = playerPosition + new Vector3(3f, 0f, 0f);
        Vector3 rayOrigin = spawnPosition + Vector3.up * 20f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) {
            spawnPosition.y = hit.point.y + 0.55f;
        } else {
            // Fallback for the current flat terrain if no collider is found.
            spawnPosition.y = 0.8f;
        }

        return spawnPosition;
    }
}
