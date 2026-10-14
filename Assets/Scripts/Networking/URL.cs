using System;
using System.IO;
using UnityEngine;

public static class URL {
    [Serializable]
    private sealed class ClientNetworkConfig {
        public string apiBaseUrl;
    }

    public static string BaseUrl = "http://localhost:8000";

    public static string AuthRegisterPath = "/auth/register";
    public static string AuthLoginPath = "/auth/login";
    public static string AuthValidatePath = "/auth/validate";

    public static string MatchJoinPath = "/match/join";
    public static string MatchStatusPath = "/match/status";

    public static string WorldRoomConfigPath = "/room";

    // The matchmaker supplies the game server address for each match.
    public static string WorldServer = string.Empty;

    public static string MatchCharacterSelectionPath = "/match/character-selection";
    public static string MatchCharacterSelectionCancelPath = "/match/character-selection/cancel";
    public static string MatchCharacterSelectionStatusPath = "/match/character-selection/status";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadClientNetworkConfig() {
        string gameDirectory = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(gameDirectory)) {
            return;
        }

        string configPath = Path.Combine(gameDirectory, "network-config.json");
        if (!File.Exists(configPath)) {
            if (!Application.isEditor && !Application.isBatchMode) {
                Debug.LogWarning($"[NetworkConfig] {configPath} is missing; using local API {BaseUrl}.");
            }
            return;
        }

        try {
            ClientNetworkConfig config = JsonUtility.FromJson<ClientNetworkConfig>(File.ReadAllText(configPath));
            string apiBaseUrl = config?.apiBaseUrl?.Trim();
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
                Debug.LogError($"[NetworkConfig] Invalid apiBaseUrl in {configPath}.");
                return;
            }

            BaseUrl = apiBaseUrl.TrimEnd('/');
            Debug.Log($"[NetworkConfig] API URL: {BaseUrl}");
        }
        catch (Exception exception) {
            Debug.LogError($"[NetworkConfig] Could not read {configPath}: {exception.Message}");
        }
    }
}
