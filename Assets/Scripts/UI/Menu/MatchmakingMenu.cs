using UnityEngine;
using TMPro;
using System.Collections;

public class MatchmakingMenu : MonoBehaviour {
    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private MatchmakerClient matchmakerClient;

    [Header("UI")]
    [SerializeField] private TMP_Text inQueueText;
    [SerializeField] private TMP_Text MatchmakingStatusText;
    [SerializeField] private float pollIntervalSeconds = 2f;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        matchmakerClient = FindFirstObjectByType<MatchmakerClient>();
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
        SetStatus(MatchmakingStatusText, $"Matched successfully: {response.room_id}");
        menuManager.HideMenu();
    }

    private void SetStatus(TMP_Text textComponent, string message) {
        Debug.Log($"[MatchmakingMenu] {message}", this);
        if (textComponent != null) {
            textComponent.text = message;
        }
    }
}
