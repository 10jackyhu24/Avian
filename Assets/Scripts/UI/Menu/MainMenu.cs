using UnityEngine;

public class MainMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void SinglePlayer() {
        menuManager.SwitchToPanel(1);
    }

    public void Multiplayer() {
        menuManager.SwitchToPanel(2);
    }

    public void OptionsPanel() {
        menuManager.SwitchToPanel(3);
    }

    public void QuitGame() {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}