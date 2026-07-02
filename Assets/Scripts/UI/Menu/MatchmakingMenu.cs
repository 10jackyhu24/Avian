using UnityEngine;
using TMPro;
using System.Collections;

public class MatchmakingMenu : MonoBehaviour {
    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private MatchmakerClient matchmakerClient;

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float pollIntervalSeconds = 2f;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        matchmakerClient = FindFirstObjectByType<MatchmakerClient>();
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
        SetStatus($"Matched successfully: {response.room_id}");
        menuManager.HideMenu();
    }

    private void SetStatus(string message) {
        Debug.Log($"[MatchmakingMenu] {message}", this);
        if (statusText != null) {
            statusText.text = message;
        }
    }
}
