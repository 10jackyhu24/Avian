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
                    SwitchToLogin(PanelType.Multiplayer);
                }
            );
            return;
        }

        SwitchToLogin(PanelType.Multiplayer);
    }

    public void SwitchToOptions() {
        menuManager.SwitchToPanel(PanelType.Options);
    }

    public void SwitchToProfile() {
        if (backendAuthClient != null && backendAuthClient.HasStoredSessionToken()) {
            backendAuthClient.ValidateCurrentSession(
                _ => {
                    menuManager.SwitchToPanel(PanelType.Profile);
                },
                _ => {
                    backendAuthClient.ClearSession();
                    SwitchToLogin(PanelType.Profile);
                }
            );
            return;
        }

        SwitchToLogin(PanelType.Profile);
    }

    private void SwitchToLogin(PanelType targetPanel) {
        FindFirstObjectByType<AuthLogInMenu>(FindObjectsInactive.Include)?.SetSuccessPanel(targetPanel);
        menuManager.SwitchToPanel(PanelType.Auth_LogIn);
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
