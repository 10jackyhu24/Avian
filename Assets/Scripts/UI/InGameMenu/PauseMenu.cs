using UnityEngine;

public class PauseMenu : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }

    public void Continue() {
        if (menuManager != null) {
            menuManager.ResumeGame();
        }
    }

    public void SwitchToOptions() {
        if (menuManager != null) {
            menuManager.SwitchToPanel(PanelType.Options);
        }
    }

    public void Leave() {
        if (menuManager != null) {
            menuManager.LeaveCurrentGame();
        }
    }
}
