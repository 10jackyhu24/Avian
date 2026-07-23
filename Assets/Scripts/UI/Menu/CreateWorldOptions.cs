using UnityEngine;
using TMPro;
using Unity.Netcode;

public class CreateWorldOptions : MonoBehaviour {
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField seedInputField;

    private MenuManager menuManager;
    private TerrainController terrainController;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        terrainController = FindFirstObjectByType<TerrainController>();
    }

    public void OnGenerateWorldClicked() {
        int seed;

        if (seedInputField != null &&
            !string.IsNullOrWhiteSpace(seedInputField.text)) {
            string rawText = seedInputField.text.Trim();

            if (int.TryParse(rawText, out int parsedSeed)) {
                seed = parsedSeed;
            }
            else {
                seed = rawText.GetHashCode();
            }
        }
        else {
            seed = Random.Range(0, 999999);
        }

        Debug.Log($"[CreateWorld] Seed: {seed}");

        // Generate world first
        terrainController.GenerateWorld(seed);

        // Run NGO Host
        NetworkUtility.DisableSceneManagement(NetworkManager.Singleton);

        if (!NetworkManager.Singleton.IsListening) {
            NetworkManager.Singleton.StartHost();
        }

        menuManager.HideMenu();
        menuManager.ClearHistory();

        GameState.SetWorld("singleplayer", seed);
    }
}
