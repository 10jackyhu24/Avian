using TMPro;
using UnityEngine;

public class AuthMenu : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Navigation")]
    [SerializeField] private PanelType successPanel = PanelType.Multiplayer;

    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private WarningWindow warningWindow;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        warningWindow = FindFirstObjectByType<WarningWindow>();
    }

    public void Register() {
        if (!TryGetCredentials(out string username, out string password)) {
            return;
        }

        if (backendAuthClient == null) {
            SetStatus("BackendAuthClient not found in scene.");
            return;
        }

        SetStatus("Registering...");
        backendAuthClient.Register(username, password, HandleAuthSuccess, HandleAuthError);
    }

    public void Login() {
        if (!TryGetCredentials(out string username, out string password)) {
            return;
        }

        if (backendAuthClient == null) {
            SetStatus("BackendAuthClient not found in scene.");
            return;
        }

        SetStatus("Logging in...");
        backendAuthClient.Login(username, password, HandleAuthSuccess, HandleAuthError);
    }

    public void Logout() {
        if (backendAuthClient == null) {
            SetStatus("BackendAuthClient not found in scene.");
            return;
        }

        backendAuthClient.ClearSession();
        SetStatus("Session cleared.");
        RefreshSessionStatus();
    }

    public void ClearCredentials() {
        usernameInput?.SetTextWithoutNotify(string.Empty);
        passwordInput?.SetTextWithoutNotify(string.Empty);
    }

    private bool TryGetCredentials(out string username, out string password) {
        username = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        password = passwordInput != null ? passwordInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) {
            SetStatus("Enter a username and password.");
            return false;
        }

        if (username.Length < 3) {
            SetStatus("Username must be at least 3 characters.");
            return false;
        }

        if (password.Length < 6) {
            SetStatus("Password must be at least 6 characters.");
            return false;
        }

        return true;
    }

    private void HandleAuthSuccess(BackendAuthClient.AuthResponse response) {
        SetStatus($"Authenticated as {response.username}.");
        RefreshSessionStatus();
        if (menuManager != null) {
            menuManager.SwitchToPanel(successPanel);
        }
    }

    private void HandleAuthError(string errorMessage) {
        SetStatus(errorMessage);
        RefreshSessionStatus();
    }

    private void RefreshSessionStatus() {
        if (backendAuthClient == null || string.IsNullOrWhiteSpace(backendAuthClient.SessionToken)) {
            warningWindow.Show("Session Status", "No active session.");
            return;
        }
        warningWindow.Show("Session Status", $"Active session for user ID: {backendAuthClient.CachedUserId}, Username: {backendAuthClient.CachedUsername}");
    }

    private void SetStatus(string message) {
        Debug.Log($"[AuthMenu] {message}", this);
        warningWindow.Show("Authentication Status", message);
    }
}
