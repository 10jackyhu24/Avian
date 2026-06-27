using UnityEngine;

public class MainMenu : MonoBehaviour {
    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
    }

    public void SwitchToSinglePlayer() {
        menuManager.SwitchToPanel(PanelType.SinglePlayer);
    }

    public void SwitchToMultiPlayer() {
        if (backendAuthClient != null && backendAuthClient.HasStoredSessionToken()) {
            backendAuthClient.ValidateCurrentSession(
                _ => {
                    menuManager.SwitchToPanel(PanelType.Multiplayer);
                },
                _ => {
                    backendAuthClient.ClearSession();
                    menuManager.SwitchToPanel(PanelType.Auth);
                }
            );
            return;
        }

        menuManager.SwitchToPanel(PanelType.Auth);
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
