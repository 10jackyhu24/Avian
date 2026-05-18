using UnityEngine;

public class MainMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void SwitchToSinglePlayer() {
        menuManager.SwitchToPanel(PanelType.SinglePlayer);
    }

    public void SwitchToMultiPlayer() {
        menuManager.SwitchToPanel(PanelType.Multiplayer);
    }

    public void SwitchToOptions() {
        menuManager.SwitchToPanel(PanelType.Options);
    }

    public void QuitGame() {
        Debug.Log("Quit Game");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}