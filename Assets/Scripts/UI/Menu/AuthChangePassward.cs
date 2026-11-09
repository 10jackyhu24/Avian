using TMPro;
using UnityEngine;

public class AuthChangePassward : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField passwardCheck;

    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private WarningWindow warningWindow;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        warningWindow = FindFirstObjectByType<WarningWindow>();
        if (passwordInput != null) passwordInput.contentType = TMP_InputField.ContentType.Password;
        if (passwardCheck != null) passwardCheck.contentType = TMP_InputField.ContentType.Password;
    }

    private void OnDisable() {
        passwordInput?.SetTextWithoutNotify(string.Empty);
        passwardCheck?.SetTextWithoutNotify(string.Empty);
    }

    public void DoChangePassword() {
        if (backendAuthClient == null || warningWindow == null || passwordInput == null || passwardCheck == null) {
            Debug.LogError("Password change requires its input fields, BackendAuthClient and WarningWindow.", this);
            return;
        }
        if (backendAuthClient.IsChangingPassword) return;
        if (!backendAuthClient.HasStoredSessionToken()) {
            warningWindow.Show("Login Required", "Please log in before changing your password.");
            ReturnToLogin();
            return;
        }

        string newPassword = passwordInput.text;
        if (newPassword.Length < 6 || newPassword.Length > 128) {
            warningWindow.Show("Password Change", "Password must be 6 to 128 characters.");
            return;
        }
        if (newPassword != passwardCheck.text) {
            warningWindow.Show("Password Change", "Passwords do not match.");
            return;
        }

        backendAuthClient.ChangePassword(newPassword,
            _ => {
                passwordInput.SetTextWithoutNotify(string.Empty);
                passwardCheck.SetTextWithoutNotify(string.Empty);
                ReturnToLogin();
                warningWindow.Show("Password Changed", "Please log in with your new password.");
            },
            error => {
                if (!backendAuthClient.HasStoredSessionToken()) ReturnToLogin();
                warningWindow.Show("Password Change Failed", error);
            });
    }

    public void Back() {
        menuManager?.Back();
    }

    private void ReturnToLogin() {
        FindFirstObjectByType<AuthMenu>()?.ClearCredentials();
        if (menuManager != null) {
            menuManager.ClearHistory();
            menuManager.SwitchToPanel(PanelType.MainMenu);
            menuManager.SwitchToPanel(PanelType.Auth_LogIn);
        }
    }
}
