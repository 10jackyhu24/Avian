using UnityEngine;

public class MainMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void SinglePlayer() {
        menuManager.SwitchToPanel(PanelType.SinglePlayer);
    }

    public void Multiplayer() {
        menuManager.SwitchToPanel(PanelType.Multiplayer);
    }

    public void OptionsPanel() {
        menuManager.SwitchToPanel(PanelType.Options);
    }

    public void QuitGame() {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}