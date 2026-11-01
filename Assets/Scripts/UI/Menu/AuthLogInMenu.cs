using TMPro;
using UnityEngine;

public class AuthLogInMenu : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Navigation")]
    [SerializeField] private PanelType successPanel = PanelType.Multiplayer;

    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private WarningWindow warningWindow;
    private AuthMenu authMenu;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        warningWindow = FindFirstObjectByType<WarningWindow>();

        authMenu = FindFirstObjectByType<AuthMenu>();
    }

    public void SetSuccessPanel(PanelType targetPanel) {
        successPanel = targetPanel;
    }

    public void DoLogin() {
        authMenu.SetInput(usernameInput, passwordInput, successPanel);
        authMenu.Login();
    }
    public void SwitchToRegister() {
        FindFirstObjectByType<AuthRegisterMenu>(FindObjectsInactive.Include)?.SetSuccessPanel(successPanel);
        menuManager.SwitchToPanel(PanelType.Auth_Register);
    }
}
