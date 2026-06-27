using TMPro;
using UnityEngine;

public class AuthMenu : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Feedback")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text sessionText;

    [Header("Navigation")]
    [SerializeField] private PanelType successPanel = PanelType.Multiplayer;

    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
    }

    // private void OnEnable() {
    //     RefreshSessionStatus();
    // }

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

    public void ValidateSession() {
        if (backendAuthClient == null) {
            SetStatus("BackendAuthClient not found in scene.");
            return;
        }

        SetStatus("Validating session...");
        backendAuthClient.ValidateCurrentSession(
            response => {
                SetStatus($"Session valid for {response.username}.");
                RefreshSessionStatus();
            },
            HandleAuthError
        );
    }

    public void ContinueToMultiplayer() {
        if (backendAuthClient == null || !backendAuthClient.HasStoredSessionToken()) {
            SetStatus("Please log in first.");
            return;
        }

        backendAuthClient.ValidateCurrentSession(
            _ => {
                menuManager.SwitchToPanel(successPanel);
            },
            errorMessage => {
                backendAuthClient.ClearSession();
                SetStatus(errorMessage);
                menuManager.SwitchToPanel(PanelType.Auth);
            }
        );
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
        if (sessionText == null) {
            return;
        }

        if (backendAuthClient == null || string.IsNullOrWhiteSpace(backendAuthClient.SessionToken)) {
            sessionText.text = "No active session.";
            return;
        }

        sessionText.text = $"Session token saved: {backendAuthClient.SessionToken}";
    }

    private void SetStatus(string message) {
        Debug.Log($"[AuthMenu] {message}", this);
        if (statusText != null) {
            statusText.text = message;
        }
    }
}
