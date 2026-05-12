using UnityEngine;

public class MainMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void SinglePlayer() {
        Debug.Log("Switch to Single Player");
        menuManager.SwitchToPanel(1);
    }

    public void Multiplayer() {
        Debug.Log("Switch to Multiplayer");
        menuManager.SwitchToPanel(2);
    }

    public void OptionsPanel() {
        Debug.Log("Switch to Options");
        menuManager.SwitchToPanel(3);
    }

    public void QuitGame() {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}