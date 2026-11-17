using TMPro;
using UnityEngine;

public class AuthMenu : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwardInput;
    [SerializeField] private TMP_InputField passwardCheck;
    private TMP_InputField oldPasswordInput;

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

    public void SetInput(TMP_InputField name, TMP_InputField passward, PanelType targetPanel) {
        usernameInput = name;
        passwardInput = passward;
        passwardCheck = null; //避免影響
        successPanel = targetPanel;
    }
    public void SetInput(TMP_InputField name, TMP_InputField passward, TMP_InputField check, PanelType targetPanel) {
        usernameInput = name;
        passwardInput = passward;
        passwardCheck = check;
        successPanel = targetPanel;
    }

    public void SetChangePasswordInput(TMP_InputField oldPassword, TMP_InputField password, TMP_InputField check) {
        usernameInput = null;
        oldPasswordInput = oldPassword;
        passwardInput = password;
        passwardCheck = check;
    }

    public void Register() {
        if (!TryGetCredentials(out string username, out string password, out string check)) {
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

    public void ChangePassward() {
        if (backendAuthClient == null) backendAuthClient = FindFirstObjectByType<BackendAuthClient>(FindObjectsInactive.Include);
        if (warningWindow == null) warningWindow = FindFirstObjectByType<WarningWindow>(FindObjectsInactive.Include);
        if (backendAuthClient == null || warningWindow == null || oldPasswordInput == null || passwardInput == null || passwardCheck == null) {
            Debug.LogError($"Password change setup is incomplete: BackendAuthClient={backendAuthClient != null}, WarningWindow={warningWindow != null}, OldPassword_Input={oldPasswordInput != null}, NewPassword_Input={passwardInput != null}, NewPassword_Check={passwardCheck != null}.", this);
            return;
        }
        if (backendAuthClient.IsChangingPassword) return;
        if (!backendAuthClient.HasStoredSessionToken()) {
            warningWindow.Show("Login Required", "Please log in before changing your password.");
            ReturnToLogin();
            return;
        }

        string oldPassword = oldPasswordInput.text;
        string newPassword = passwardInput.text;
        if (oldPassword.Length < 6 || oldPassword.Length > 128) {
            warningWindow.Show("Password Change", "Enter your current password.");
            return;
        }
        if (newPassword.Length < 6 || newPassword.Length > 128) {
            warningWindow.Show("Password Change", "Password must be 6 to 128 characters.");
            return;
        }
        if (newPassword != passwardCheck.text) {
            warningWindow.Show("Password Change", "Passwords do not match.");
            return;
        }
        if (newPassword == oldPassword) {
            warningWindow.Show("Password Change", "New password must be different from current password.");
            return;
        }

        TMP_InputField oldPasswordField = oldPasswordInput;
        TMP_InputField passwordField = passwardInput;
        TMP_InputField checkField = passwardCheck;
        backendAuthClient.ChangePassword(oldPassword, newPassword,
            _ => {
                oldPasswordField.SetTextWithoutNotify(string.Empty);
                passwordField.SetTextWithoutNotify(string.Empty);
                checkField.SetTextWithoutNotify(string.Empty);
                ReturnToLogin();
                warningWindow.Show("Password Changed", "Please log in with your new password.");
            },
            error => {
                if (!backendAuthClient.HasStoredSessionToken()) ReturnToLogin();
                warningWindow.Show("Password Change Failed", error);
            });
    }

    private void ReturnToLogin() {
        ClearCredentials();
        oldPasswordInput?.SetTextWithoutNotify(string.Empty);
        passwardCheck?.SetTextWithoutNotify(string.Empty);
        if (menuManager != null) {
            menuManager.ClearHistory();
            menuManager.SwitchToPanel(PanelType.MainMenu);
            menuManager.SwitchToPanel(PanelType.Auth_LogIn);
        }
    }

    public void ClearCredentials() {
        usernameInput?.SetTextWithoutNotify(string.Empty);
        passwardInput?.SetTextWithoutNotify(string.Empty);
    }

    private bool TryGetCredentials(out string username, out string password) {
        username = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        password = passwardInput != null ? passwardInput.text : string.Empty;

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

    private bool TryGetCredentials(out string username, out string password, out string check) {
        username = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        password = passwardInput != null ? passwardInput.text : string.Empty;
        check    = passwardCheck != null ? passwardCheck.text : string.Empty;

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

        if(!password.Equals(check)) {
            SetStatus("CheckBox is not equals to Passward.");
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
