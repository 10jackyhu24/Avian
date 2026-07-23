using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.VisualScripting;

public class MatchmakingMenu : MonoBehaviour {
    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private MatchmakerClient matchmakerClient;
    private TerrainController terrainController;
    private bool isConnectingToWorldServer;

    [Header("UI")]
    [SerializeField] private TMP_Text inQueueText;
    [SerializeField] private TMP_Text MatchmakingStatusText;
    private WarningWindow warningWindow;

    [SerializeField] private float pollIntervalSeconds = 2f;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        matchmakerClient = FindFirstObjectByType<MatchmakerClient>();
        terrainController = FindFirstObjectByType<TerrainController>();
        warningWindow = FindFirstObjectByType<WarningWindow>();
    }

    private void OnDestroy() {
        UnregisterNetworkCallbacks();
    }

    public void Matchmaking() {
        if (backendAuthClient == null || string.IsNullOrWhiteSpace(backendAuthClient.SessionToken)) {
            // SetStatus(MatchmakingStatusText, "Please log in first.");
            return;
        }

        if (matchmakerClient == null) {
            // SetStatus(MatchmakingStatusText, "MatchmakerClient not found in scene.");
            return;
        }

        StartCoroutine(JoinAndWait());
    }

    private IEnumerator JoinAndWait() {
        SetStatus(MatchmakingStatusText, "Joining queue...");

        bool requestDone = false;
        string errorMessage = null;
        MatchmakerClient.MatchResponse latestResponse = null;

        matchmakerClient.JoinQueue(
            backendAuthClient.GetCachedUserId(),
            backendAuthClient.GetCachedUsername(),
            response => {
                latestResponse = response;
                requestDone = true;
            },
            response => {
                latestResponse = response;
                requestDone = true;
            },
            error => {
                errorMessage = error;
                requestDone = true;
            }
        );

        yield return new WaitUntil(() => requestDone);

        if (!string.IsNullOrWhiteSpace(errorMessage)) {
            SetStatus(MatchmakingStatusText, errorMessage);
            yield break;
        }

        if (latestResponse != null && latestResponse.status == "matched") {
            EnterWorld(latestResponse);
            yield break;
        }

        SetStatus(MatchmakingStatusText, "Matching...");

        while (true) {
            yield return new WaitForSeconds(pollIntervalSeconds);

            bool pollDone = false;
            errorMessage = null;

            matchmakerClient.PollStatus(
                backendAuthClient.GetCachedUserId(),
                response => {
                    latestResponse = response;
                    pollDone = true;
                },
                response => {
                    latestResponse = response;
                    pollDone = true;
                },
                error => {
                    errorMessage = error;
                    pollDone = true;
                }
            );

            yield return new WaitUntil(() => pollDone);

            if (!string.IsNullOrWhiteSpace(errorMessage)) {
                SetStatus(MatchmakingStatusText, errorMessage);
                yield break;
            }

            if (latestResponse != null && latestResponse.status == "matched") {
                EnterWorld(latestResponse);
                yield break;
            }

            SetStatus(inQueueText, $"Matching Player: {latestResponse?.queue_position ?? 1}");
        }
    }

    private void EnterWorld(MatchmakerClient.MatchResponse response) {
        if (response == null) {
            SetStatus(MatchmakingStatusText, "Match response missing.");
            return;
        }

        Debug.Log($"[Matchmaking] Matched successfully: room={response.room_id}, seed={response.seed}, endpoint={response.world_endpoint}", this);
        StartCoroutine(EnterWorldRoutine(response));
    }

    private IEnumerator EnterWorldRoutine(MatchmakerClient.MatchResponse response) {
        GameState.SetWorld(response.room_id, response.seed);

        string endpoint = !string.IsNullOrWhiteSpace(response.world_endpoint)
            ? response.world_endpoint
            : URL.WorldServer;

        if (!TryConnectToDedicatedServer(endpoint)) {
            yield break;
        }

        yield return null;
    }

    private bool TryConnectToDedicatedServer(string endpoint) {
        if (NetworkManager.Singleton == null) {
            warningWindow.Show("NetworkManager", "NetworkManager not found.");
            return false;
        }

        if (NetworkManager.Singleton.IsListening) {
            warningWindow.Show("NetworkManager", "NetworkManager is already running.");
            return true;
        }

        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is not UnityTransport unityTransport) {
            warningWindow.Show("NetworkManager", "UnityTransport not found on NetworkManager.");
            return false;
        }

        if (!TryParseEndpoint(endpoint, out string host, out ushort port)) {
            warningWindow.Show("Connect Failed", $"Invalid world server endpoint: {endpoint}");
            return false;
        }

        NetworkUtility.DisableSceneManagement(NetworkManager.Singleton);
        unityTransport.SetConnectionData(host, port);
        RegisterNetworkCallbacks();
        isConnectingToWorldServer = true;

        bool started = NetworkManager.Singleton.StartClient();

        Debug.Log($"[MatchmakingMenu] StartClient={started}, endpoint={host}:{port}", this);
        if (!started) {
            isConnectingToWorldServer = false;
            warningWindow.Show("Client Faild", "Failed to start client.");
        }

        return started;
    }

    private void RegisterNetworkCallbacks() {
        if (NetworkManager.Singleton == null) {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnregisterNetworkCallbacks() {
        if (NetworkManager.Singleton == null) {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId) {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId) {
            return;
        }

        isConnectingToWorldServer = false;
        if (terrainController == null) {
            terrainController = FindFirstObjectByType<TerrainController>();
        }

        if (terrainController != null && GameState.hasWorldSeed) {
            terrainController.GenerateWorld(GameState.currentSeed);
        }

        warningWindow.Show("Client Connected",$"Connected to game server as client {clientId}.");

        menuManager.HideMenu();
        menuManager.ClearHistory();
    }

    private void HandleClientDisconnected(ulong clientId) {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId) {
            return;
        }

        string message = isConnectingToWorldServer
            ? "Disconnected while joining game server. Check server build, scene list, and k8s logs."
            : "Disconnected from game server.";

        isConnectingToWorldServer = false;
        Debug.LogWarning($"[MatchmakingMenu] {message}", this);
        warningWindow.Show("Warning", message);
    }

    private static bool TryParseEndpoint(string endpoint, out string host, out ushort port) {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(endpoint)) {
            return false;
        }

        string trimmed = endpoint.Trim();
        if (System.Uri.TryCreate(trimmed, System.UriKind.Absolute, out System.Uri uri) &&
            !string.IsNullOrWhiteSpace(uri.Host) &&
            uri.Port > 0 &&
            uri.Port <= ushort.MaxValue) {
            host = uri.Host;
            port = (ushort)uri.Port;
            return true;
        }

        int colonIndex = trimmed.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex >= trimmed.Length - 1) {
            return false;
        }

        host = trimmed.Substring(0, colonIndex);
        return ushort.TryParse(trimmed.Substring(colonIndex + 1), out port);
    }

    private void SetStatus(TMP_Text textComponent, string message) {
        Debug.Log($"[MatchmakingMenu] {message}", this);
        if (textComponent != null) {
            textComponent.text = message;
        }
    }
}
