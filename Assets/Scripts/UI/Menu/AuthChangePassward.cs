using TMPro;
using UnityEngine;

public class AuthChangePassward : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField passwardCheck;

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

    public void DoRegist() {
        authMenu.SetInput(usernameInput, passwordInput, passwardCheck, successPanel);
        authMenu.Register();
    }
}