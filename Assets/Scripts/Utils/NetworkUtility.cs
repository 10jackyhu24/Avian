using Unity.Netcode;

public static class NetworkUtility {
    public static void DisableSceneManagement(NetworkManager networkManager) {
        if (networkManager == null) {
            return;
        }

        networkManager.NetworkConfig.EnableSceneManagement = false;
    }
}
