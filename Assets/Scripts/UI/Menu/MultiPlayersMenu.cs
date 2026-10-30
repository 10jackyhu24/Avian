using UnityEngine;
using UnityEngine.UI;

public class MultiPlayersMenu : MonoBehaviour {
    [SerializeField] private Button deleteAccountButton;

    private MenuManager menuManager;
    private BackendAuthClient backendAuthClient;
    private WarningWindow warningWindow;
    private GameObject confirmationWindow;
    private GameObject progressWindow;
    private bool isDeletingAccount;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        backendAuthClient = FindFirstObjectByType<BackendAuthClient>();
        warningWindow = FindFirstObjectByType<WarningWindow>();
        deleteAccountButton?.onClick.AddListener(RequestAccountDeletion);
    }

    private void OnDestroy() {
        deleteAccountButton?.onClick.RemoveListener(RequestAccountDeletion);
        if (confirmationWindow != null) {
            Destroy(confirmationWindow);
        }
        if (progressWindow != null) {
            Destroy(progressWindow);
        }
    }

    public void SwitchToRoomList() {
        menuManager.SwitchToPanel(PanelType.MultiplayerRoomList);
    }

    public void SwitchToMatchmaking() {
        menuManager.SwitchToPanel(PanelType.MultiplayerMatchmaking);
    }

    public void RequestAccountDeletion() {
        if (isDeletingAccount || confirmationWindow != null) {
            return;
        }
        if (backendAuthClient == null || warningWindow == null) {
            Debug.LogError("Account deletion requires BackendAuthClient and WarningWindow.", this);
            return;
        }
        if (!backendAuthClient.HasStoredSessionToken()) {
            ReturnToLogin();
            warningWindow.Show("Login Required", "Please log in before deleting your account.");
            return;
        }

        string token = backendAuthClient.SessionToken;
        string username = backendAuthClient.CachedUsername;
        confirmationWindow = warningWindow.ShowConfirmation(
            "Delete Account",
            $"Permanently delete account \"{username}\"?\n\nYour account and all login sessions will be removed. This action cannot be undone.",
            () => {
                confirmationWindow = null;
                if (backendAuthClient.SessionToken != token) {
                    warningWindow.Show("Session Changed", "Please try again with your current account.");
                    return;
                }
                BeginAccountDeletion();
            },
            () => {
                confirmationWindow = null;
                if (deleteAccountButton != null && deleteAccountButton.isActiveAndEnabled) {
                    deleteAccountButton.Select();
                }
            },
            "Delete Account"
        );
    }

    private void BeginAccountDeletion() {
        SetDeletingAccount(true);
        progressWindow = warningWindow.ShowProgress("Delete Account", "Deleting your account...");
        backendAuthClient.DeleteCurrentAccount(
            _ => {
                CloseProgressWindow();
                SetDeletingAccount(false);
                if (!backendAuthClient.HasStoredSessionToken()) {
                    ReturnToLogin();
                }
                warningWindow.Show("Account Deleted", "Your account and all login sessions have been deleted.");
            },
            error => {
                CloseProgressWindow();
                SetDeletingAccount(false);
                if (!backendAuthClient.HasStoredSessionToken()) {
                    ReturnToLogin();
                }
                warningWindow.Show("Account Deletion Failed", error);
            }
        );
    }

    private void SetDeletingAccount(bool deleting) {
        isDeletingAccount = deleting;
        if (deleteAccountButton != null) {
            deleteAccountButton.interactable = !deleting;
        }
    }

    private void CloseProgressWindow() {
        if (progressWindow != null) {
            warningWindow.CloseWindow(progressWindow);
            progressWindow = null;
        }
    }

    private void ReturnToLogin() {
        FindFirstObjectByType<AuthMenu>()?.ClearCredentials();
        if (menuManager != null) {
            menuManager.ClearHistory();
            menuManager.SwitchToPanel(PanelType.MainMenu);
            menuManager.SwitchToPanel(PanelType.Auth);
        }
    }
}
