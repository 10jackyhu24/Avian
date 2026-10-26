using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class CharacterSelectionMenu : MonoBehaviour
{
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private string defaultDescription = "Hover over a character to see details.";
    [SerializeField] private float matchReadyPollIntervalSeconds = 1f;

    protected string selectedCharacterId;
    private bool selectionLocked;
    private Coroutine matchReadyCoroutine;

    private void OnEnable() {
        selectionLocked = false;
        selectedCharacterId = string.Empty;
        ShowDefaultDescription();
    }

    private void OnDisable() {
        StopMatchReadyCoroutine();
    }

    private void Update() {
        if (!selectionLocked || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) {
            return;
        }

        UnlockSelection();
    }

    public void PreviewCharacter(string characterName, string description) {
        if (descriptionText == null || selectionLocked) {
            return;
        }

        descriptionText.text = string.IsNullOrWhiteSpace(description)
            ? characterName
            : $"{characterName}\n{description}";
    }

    public void ClearPreview() {
        if (selectionLocked) {
            return;
        }

        ShowDefaultDescription();
    }

    public virtual void SelectCharacter(string characterId, string characterName) {
        if (selectionLocked) {
            return;
        }

        selectionLocked = true;
        selectedCharacterId = characterId;
        GameState.SetSelectedCharacter(characterId);

        if (descriptionText != null) {
            descriptionText.text = $"Selected: {characterName}\nPress Esc to choose again.";
        }

        Debug.Log($"[CharacterSelectionMenu] Selected character: {selectedCharacterId}", this);
        OnCharacterSelected(characterId, characterName);
    }

    protected virtual void OnCharacterSelected(string characterId, string characterName) {
        switch (GameState.characterSelectionMode) {
            case CharacterSelectionMode.SinglePlayer:
                StartSinglePlayerWorld();
                break;
            case CharacterSelectionMode.P2P:
                Debug.Log("[CharacterSelectionMenu] P2P character selection is not wired yet.", this);
                break;
            case CharacterSelectionMode.Match:
                WaitForMatchedPlayersToSelect(characterId, characterName);
                break;
            default:
                Debug.Log("[CharacterSelectionMenu] Character selected without an active selection mode.", this);
                break;
        }
    }

    private void ShowDefaultDescription() {
        if (descriptionText != null) {
            descriptionText.text = defaultDescription;
        }
    }

    private void UnlockSelection() {
        FindFirstObjectByType<MatchmakingMenu>()?.CancelPendingWorldConnection();
        string previousCharacterId = selectedCharacterId;
        selectedCharacterId = string.Empty;
        selectionLocked = false;
        GameState.SetSelectedCharacter(string.Empty);
        StopMatchReadyCoroutine();
        ShowDefaultDescription();

        if (GameState.characterSelectionMode != CharacterSelectionMode.Match ||
            string.IsNullOrWhiteSpace(previousCharacterId)) {
            return;
        }

        MatchmakerClient matchmakerClient = FindFirstObjectByType<MatchmakerClient>();
        BackendAuthClient backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        if (matchmakerClient == null || backendAuthClient == null) {
            return;
        }

        matchmakerClient.CancelCharacterSelection(
            backendAuthClient.GetCachedUserId(),
            _ => Debug.Log("[CharacterSelectionMenu] Match character selection cancelled.", this),
            error => Debug.LogWarning($"[CharacterSelectionMenu] Failed to cancel character selection: {error}", this)
        );
    }

    private void StartSinglePlayerWorld() {
        if (!GameState.hasPendingSinglePlayerWorld) {
            Debug.LogWarning("[CharacterSelectionMenu] No pending singleplayer seed found. Using a random seed.", this);
            GameState.SetPendingSinglePlayerWorld(Random.Range(0, 999999));
        }

        int seed = GameState.pendingSinglePlayerSeed;
        TerrainController terrainController = FindFirstObjectByType<TerrainController>();
        if (terrainController != null) {
            terrainController.GenerateWorld(seed);
        }
        else {
            Debug.LogWarning("[CharacterSelectionMenu] TerrainController not found.", this);
        }

        if (NetworkManager.Singleton != null) {
            NetworkUtility.DisableSceneManagement(NetworkManager.Singleton);

            if (!NetworkManager.Singleton.IsListening) {
                if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is Unity.Netcode.Transports.UTP.UnityTransport transport) {
                    GameConnectionSettings.ConfigureSinglePlayer(transport);
                }
                NetworkManager.Singleton.StartHost();
            }
        }
        else {
            Debug.LogWarning("[CharacterSelectionMenu] NetworkManager not found.", this);
        }

        GameState.SetWorld("singleplayer", seed);

        MenuManager menuManager = FindFirstObjectByType<MenuManager>();
        if (menuManager != null) {
            menuManager.HideMenu();
            menuManager.ClearHistory();
        }
    }

    private void ConnectToMatchWorld() {
        MatchmakingMenu matchmakingMenu = FindFirstObjectByType<MatchmakingMenu>();
        if (matchmakingMenu == null) {
            Debug.LogWarning("[CharacterSelectionMenu] MatchmakingMenu not found.", this);
            return;
        }

        matchmakingMenu.ConnectToPendingMatchWorld();
    }

    private void WaitForMatchedPlayersToSelect(string characterId, string characterName) {
        StopMatchReadyCoroutine();
        matchReadyCoroutine = StartCoroutine(WaitForMatchedPlayersToSelectRoutine(characterId, characterName));
    }

    private IEnumerator WaitForMatchedPlayersToSelectRoutine(string characterId, string characterName) {
        MatchmakerClient matchmakerClient = FindFirstObjectByType<MatchmakerClient>();
        BackendAuthClient backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        if (matchmakerClient == null || backendAuthClient == null) {
            SetDescription("Match services not found.");
            selectionLocked = false;
            yield break;
        }

        SetDescription($"Selected: {characterName}\nWaiting for other player...\nPress Esc to choose again.");

        bool requestDone = false;
        string errorMessage = null;
        MatchmakerClient.CharacterSelectionResponse latestResponse = null;

        matchmakerClient.SubmitCharacterSelection(
            backendAuthClient.GetCachedUserId(),
            characterId,
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

        while (string.IsNullOrWhiteSpace(errorMessage) && latestResponse != null && !latestResponse.all_ready) {
            SetDescription(
                $"Selected: {characterName}\nWaiting for players: {latestResponse.selected_count}/{latestResponse.player_count}\nPress Esc to choose again."
            );

            yield return new WaitForSeconds(matchReadyPollIntervalSeconds);

            requestDone = false;
            matchmakerClient.PollCharacterSelectionStatus(
                backendAuthClient.GetCachedUserId(),
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
        }

        matchReadyCoroutine = null;

        if (!string.IsNullOrWhiteSpace(errorMessage)) {
            SetDescription($"Character selection failed: {errorMessage}");
            selectionLocked = false;
            yield break;
        }

        if (latestResponse == null || !latestResponse.all_ready) {
            yield break;
        }

        GameState.SetPendingMatchWorld(
            latestResponse.room_id,
            latestResponse.seed,
            latestResponse.world_endpoint
        );
        SetDescription("All players ready. Entering game...");
        ConnectToMatchWorld();
    }

    private void StopMatchReadyCoroutine() {
        if (matchReadyCoroutine == null) {
            return;
        }

        StopCoroutine(matchReadyCoroutine);
        matchReadyCoroutine = null;
    }

    private void SetDescription(string message) {
        if (descriptionText != null) {
            descriptionText.text = message;
        }
    }

    public void ShowConnectionProgress(string message) {
        SetDescription($"{message}\nPress Esc to cancel.");
    }

    public void ShowConnectionFailure(string message) {
        StopMatchReadyCoroutine();
        selectionLocked = false;
        selectedCharacterId = string.Empty;
        GameState.SetSelectedCharacter(string.Empty);
        SetDescription($"{message}\nSelect a character to retry.");
    }
}
