using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class MatchmakingMenu : MonoBehaviour {
    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private MatchmakerClient matchmakerClient;
    private TerrainController terrainController;
    private bool isConnectingToWorldServer;
    private string joiningRoomId;
    private int joiningSeed;
    private string joiningEndpoint;
    private int connectionAttemptId;
    private bool transportConnected;
    private NetworkTransport observedTransport;
    private bool hasEnteredMatchWorld;
    private bool applicationQuitting;

    [SerializeField, Min(1f)] private float connectionTimeoutSeconds = 30f;

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

    private void OnApplicationQuit() {
        applicationQuitting = true;
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
        GameState.SetPendingMatchWorld(response.room_id, response.seed, response.world_endpoint);

        if (menuManager == null) {
            SetStatus(MatchmakingStatusText, "MenuManager not found.");
            return;
        }

        menuManager.SwitchToPanel(PanelType.CharacterSelection);
    }

    public void ConnectToPendingMatchWorld() {
        if (isConnectingToWorldServer) {
            return;
        }
        if (!GameState.hasPendingMatchWorld) {
            warningWindow.Show("Match Missing", "No pending match found.");
            return;
        }

        StartCoroutine(EnterWorldRoutine(
            GameState.pendingMatchRoomId,
            GameState.pendingMatchSeed,
            GameState.pendingMatchEndpoint
        ));
    }

    private IEnumerator EnterWorldRoutine(string roomId, int seed, string worldEndpoint) {
        string endpoint = !string.IsNullOrWhiteSpace(worldEndpoint)
            ? worldEndpoint
            : URL.WorldServer;
        joiningRoomId = roomId;
        joiningSeed = seed;
        joiningEndpoint = endpoint;
        int attemptId = ++connectionAttemptId;

        if (!TryConnectToDedicatedServer(endpoint)) {
            FindFirstObjectByType<CharacterSelectionMenu>()?.ShowConnectionFailure("Could not start the game connection. Check the connection warning.");
            yield break;
        }

        float startedAt = Time.realtimeSinceStartup;
        float deadline = startedAt + Mathf.Max(1f, connectionTimeoutSeconds);
        float nextProgressAt = startedAt;
        while (isConnectingToWorldServer && attemptId == connectionAttemptId) {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsConnectedClient &&
                manager.LocalClient?.PlayerObject != null && manager.LocalClient.PlayerObject.IsSpawned) {
                CompleteWorldConnection(manager.LocalClientId);
                yield break;
            }
            if (Time.realtimeSinceStartup >= deadline) {
                string stage = manager != null && manager.IsConnectedClient
                    ? "The server accepted the client, but its player did not spawn. Check matching client/server builds."
                    : transportConnected ? "Transport connected, but Netcode did not finish joining. Check the client/server errors."
                    : "The game transport did not connect. Check the endpoint, TLS certificate, and Caddy game proxy.";
                FailWorldConnection($"Timed out joining {endpoint} after {connectionTimeoutSeconds:0}s. {stage}");
                yield break;
            }
            if (Time.realtimeSinceStartup >= nextProgressAt) {
                string progress = manager != null && manager.IsConnectedClient ? "Preparing your character"
                    : transportConnected ? "Joining game server" : "Connecting to game server";
                Debug.Log($"[MatchmakingMenu] {progress}: {endpoint}, elapsed={Time.realtimeSinceStartup - startedAt:0}s", this);
                FindFirstObjectByType<CharacterSelectionMenu>()?.ShowConnectionProgress($"{progress}... ({Time.realtimeSinceStartup - startedAt:0}/{connectionTimeoutSeconds:0}s)");
                nextProgressAt = Time.realtimeSinceStartup + 5f;
            }
            yield return null;
        }
    }

    private bool TryConnectToDedicatedServer(string endpoint) {
        if (NetworkManager.Singleton == null) {
            warningWindow.Show("NetworkManager", "NetworkManager not found.");
            return false;
        }

        if (NetworkManager.Singleton.IsListening) {
            warningWindow.Show("NetworkManager", "NetworkManager is already running.");
            return false;
        }

        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is not UnityTransport unityTransport) {
            warningWindow.Show("NetworkManager", "UnityTransport not found on NetworkManager.");
            return false;
        }

        try {
            GameConnectionSettings.ConfigureClient(unityTransport, endpoint);
        } catch (System.ArgumentException error) {
            warningWindow?.Show("Connect Failed", error.Message);
            return false;
        }

        NetworkUtility.DisableSceneManagement(NetworkManager.Singleton);
        transportConnected = false;
        hasEnteredMatchWorld = false;
        RegisterNetworkCallbacks();
        isConnectingToWorldServer = true;

        bool started = NetworkManager.Singleton.StartClient();

        Debug.Log($"[MatchmakingMenu] StartClient={started}, endpoint={endpoint}, websocket={unityTransport.UseWebSockets}, encrypted={unityTransport.UseEncryption}", this);
        if (!started) {
            isConnectingToWorldServer = false;
            UnregisterNetworkCallbacks();
            warningWindow?.Show("Client Failed", "Failed to start client.");
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
        NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
        NetworkManager.Singleton.OnTransportFailure += HandleTransportFailure;
        observedTransport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        observedTransport.OnTransportEvent -= HandleTransportEvent;
        observedTransport.OnTransportEvent += HandleTransportEvent;
    }

    private void UnregisterNetworkCallbacks() {
        if (observedTransport != null) {
            observedTransport.OnTransportEvent -= HandleTransportEvent;
            observedTransport = null;
        }
        if (NetworkManager.Singleton == null) {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
    }

    private void HandleTransportEvent(NetworkEvent eventType, ulong clientId, System.ArraySegment<byte> payload, float receiveTime) {
        if (isConnectingToWorldServer && eventType == NetworkEvent.Connect) {
            transportConnected = true;
            Debug.Log($"[MatchmakingMenu] Game transport connected: {joiningEndpoint}; waiting for Netcode.", this);
        }
    }

    private void HandleClientConnected(ulong clientId) {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId) {
            return;
        }

        Debug.Log($"[MatchmakingMenu] Server accepted client {clientId}; waiting for its player object.", this);
    }

    private void CompleteWorldConnection(ulong clientId) {
        isConnectingToWorldServer = false;
        hasEnteredMatchWorld = true;
        GameState.SetWorld(joiningRoomId, joiningSeed);
        Debug.Log($"[MatchmakingMenu] Local player spawned: client={clientId}, room={joiningRoomId}, seed={joiningSeed}", this);
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

    public void CancelPendingWorldConnection() {
        if (!isConnectingToWorldServer) {
            return;
        }
        isConnectingToWorldServer = false;
        connectionAttemptId++;
        UnregisterNetworkCallbacks();
        NetworkManager.Singleton?.Shutdown();
    }

    private void FailWorldConnection(string message) {
        CancelPendingWorldConnection();
        GameState.SetPendingMatchWorld(joiningRoomId, joiningSeed, joiningEndpoint);
        Debug.LogWarning($"[MatchmakingMenu] {message}", this);
        FindFirstObjectByType<CharacterSelectionMenu>()?.ShowConnectionFailure(message);
        warningWindow?.Show("Game connection failed", message);
    }

    private void HandleTransportFailure() {
        if (isConnectingToWorldServer) {
            FailWorldConnection($"Transport failed joining {joiningEndpoint}. Check the TLS/transport error in Console.");
        }
    }

    private void HandleClientDisconnected(ulong clientId) {
        if (applicationQuitting || NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId) {
            return;
        }

        string reason = NetworkManager.Singleton.DisconnectReason;
        if (isConnectingToWorldServer) {
            FailWorldConnection($"Disconnected joining {joiningEndpoint}. {reason}");
            return;
        }
        if (!hasEnteredMatchWorld || !GameState.isPlaying) {
            // Leaving through the pause menu already resets the world and shows the main menu.
            hasEnteredMatchWorld = false;
            UnregisterNetworkCallbacks();
            return;
        }
        string message = $"Disconnected from game server. {reason}";

        isConnectingToWorldServer = false;
        Debug.LogWarning($"[MatchmakingMenu] {message}", this);
        if (hasEnteredMatchWorld) {
            hasEnteredMatchWorld = false;
            UnregisterNetworkCallbacks();
            GameState.ResetWorld();
            GameState.SetPendingMatchWorld(joiningRoomId, joiningSeed, joiningEndpoint);
            menuManager?.ClearHistory();
            menuManager?.HideMenu(true);
            menuManager?.SwitchToPanel(PanelType.CharacterSelection);
            FindFirstObjectByType<CharacterSelectionMenu>()?.ShowConnectionFailure(message);
        }
        warningWindow?.Show("Game disconnected", message);
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
