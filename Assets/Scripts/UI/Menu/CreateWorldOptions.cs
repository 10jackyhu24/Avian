using UnityEngine;
using TMPro;

public class CreateWorldOptions : MonoBehaviour {
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField seedInputField;

    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void OnGenerateWorldClicked() {
        int seed = ResolveSeed();

        Debug.Log($"[CreateWorld] Seed: {seed}");
        GameState.SetPendingSinglePlayerWorld(seed);

        if (menuManager == null) {
            Debug.LogError("[CreateWorld] MenuManager not found.", this);
            return;
        }

        menuManager.SwitchToPanel(PanelType.CharacterSelection);
    }

    private int ResolveSeed() {
        if (seedInputField == null ||
            string.IsNullOrWhiteSpace(seedInputField.text)) {
            return Random.Range(0, 999999);
        }

        string rawText = seedInputField.text.Trim();
        return int.TryParse(rawText, out int parsedSeed)
            ? parsedSeed
            : rawText.GetHashCode();
    }
}
