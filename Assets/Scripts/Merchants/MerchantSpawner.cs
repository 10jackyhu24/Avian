using Unity.Netcode;
using UnityEngine;

public static class MerchantSpawner {
    private const string MerchantPrefabPath = "Merchant";
    private static readonly Vector3 OriginMerchantPosition = new Vector3(3f, 0.25f, 10f);

    public static bool TrySpawnOriginMerchant(NetworkManager networkManager, MonoBehaviour context = null) {
        return TrySpawnMerchantAt(networkManager, OriginMerchantPosition, context);
    }

    public static bool TrySpawnMerchantAt(
        NetworkManager networkManager,
        Vector3 position,
        MonoBehaviour context = null) {
        if (networkManager == null || !networkManager.IsServer) {
            return false;
        }

        GameObject merchantPrefab = Resources.Load<GameObject>(MerchantPrefabPath);
        if (merchantPrefab == null) {
            Debug.LogError("[MerchantSpawner] Merchant prefab was not found in Resources.", context);
            return false;
        }

        GameObject merchantObject = Object.Instantiate(
            merchantPrefab,
            position,
            Quaternion.identity
        );

        if (!merchantObject.TryGetComponent(out NetworkObject networkObject)) {
            Debug.LogError("[MerchantSpawner] Merchant prefab is missing NetworkObject.", context);
            Object.Destroy(merchantObject);
            return false;
        }

        networkObject.Spawn(true);
        return true;
    }
}
