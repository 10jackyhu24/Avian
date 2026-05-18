using UnityEngine;
using TMPro;

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

        if (seedInputField != null && !string.IsNullOrWhiteSpace(seedInputField.text)) {
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
        terrainController.GenerateWorld(seed);
        
        menuManager.HideMenu();
    }
}