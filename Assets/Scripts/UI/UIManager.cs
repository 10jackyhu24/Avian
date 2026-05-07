using UnityEngine;

public class UIManager : MonoBehaviour {
    [Header("UI Elements")]
    [Tooltip("Drag all Panels in sequence")]
    public GameObject[] uiPanels;

    public void Start() {
        if (uiPanels == null || uiPanels.Length == 0) {
            Debug.LogError("UI Panels not assigned in the inspector.");
            return;
        }
        SwitchToPanel(0);
    }

    public void SwitchToPanel(int panelIndex) {
        if (panelIndex < 0 || panelIndex >= uiPanels.Length) {
            Debug.LogError("Panel index out of range: " + panelIndex);
            return;
        }
        for (int i = 0; i < uiPanels.Length; ++i) {
            uiPanels[i].SetActive(i == panelIndex);
        }
    }

    public void MainMenu() {
        Debug.Log("Switch to Main Menu");
        SwitchToPanel(0);
    }

    public void SinglePlayer() {
        Debug.Log("Switch to Single Player");
        SwitchToPanel(1);
    }

    public void Multiplayer() {
        Debug.Log("Switch to Multiplayer");
        SwitchToPanel(2);
    }

    public void OptionsPanel() {
        Debug.Log("Switch to Options");
        SwitchToPanel(3);
    }

    public void QuitGame() {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
