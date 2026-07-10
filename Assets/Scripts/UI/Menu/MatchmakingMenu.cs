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

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float pollIntervalSeconds = 2f;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        matchmakerClient = FindFirstObjectByType<MatchmakerClient>();
        terrainController = FindFirstObjectByType<TerrainController>();
    }

    public void Matchmaking() {
        if (backendAuthClient == null || string.IsNullOrWhiteSpace(backendAuthClient.SessionToken)) {
            SetStatus("Please log in first.");
            return;
        }

        if (matchmakerClient == null) {
            SetStatus("MatchmakerClient not found in scene.");
            return;
        }

        StartCoroutine(JoinAndWait());
    }

    private IEnumerator JoinAndWait() {
        SetStatus("Joining queue...");

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
            SetStatus(errorMessage);
            yield break;
        }

        if (latestResponse != null && latestResponse.status == "matched") {
            EnterWorld(latestResponse);
            yield break;
        }

        SetStatus("Queued. Waiting for another player...");

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
                SetStatus(errorMessage);
                yield break;
            }

            if (latestResponse != null && latestResponse.status == "matched") {
                EnterWorld(latestResponse);
                yield break;
            }

            SetStatus($"Queue position: {latestResponse?.queue_position ?? 1}, waiting...");
        }
    }

    private void EnterWorld(MatchmakerClient.MatchResponse response) {
        if (response == null) {
            SetStatus("Match response missing.");
            return;
        }

        Debug.Log($"[Matchmaking] Matched successfully: room={response.room_id}, seed={response.seed}, endpoint={response.world_endpoint}", this);
        StartCoroutine(EnterWorldRoutine(response));
    }

    private IEnumerator EnterWorldRoutine(MatchmakerClient.MatchResponse response) {
        GameState.currentRoomId = response.room_id;
        GameState.currentSeed = response.seed;
        GameState.isPlaying = true;

        if (terrainController != null) {
            terrainController.GenerateWorld(response.seed);
        }

        string endpoint = string.IsNullOrWhiteSpace(URL.WorldServer)
            ? response.world_endpoint
            : URL.WorldServer;

        if (!TryConnectToDedicatedServer(endpoint)) {
            yield break;
        }

        SetStatus($"Connecting to {endpoint}...");
        yield return null;
        menuManager.HideMenu();
    }

    private bool TryConnectToDedicatedServer(string endpoint) {
        if (NetworkManager.Singleton == null) {
            SetStatus("NetworkManager not found.");
            return false;
        }

        if (NetworkManager.Singleton.IsListening) {
            SetStatus("NetworkManager is already running.");
            return true;
        }

        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is not UnityTransport unityTransport) {
            SetStatus("UnityTransport not found on NetworkManager.");
            return false;
        }

        if (!TryParseEndpoint(endpoint, out string host, out ushort port)) {
            SetStatus($"Invalid world server endpoint: {endpoint}");
            return false;
        }

        unityTransport.SetConnectionData(host, port);
        bool started = NetworkManager.Singleton.StartClient();

        Debug.Log($"[MatchmakingMenu] StartClient={started}, endpoint={host}:{port}", this);
        if (!started) {
            SetStatus("Failed to start client.");
        }

        return started;
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

    private void SetStatus(string message) {
        Debug.Log($"[MatchmakingMenu] {message}", this);
        if (statusText != null) {
            statusText.text = message;
        }
    }
}
