using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PauseMenu : MonoBehaviour {
    private GameInput inputActions;
    private MenuManager menuManager;
    private bool isOpen;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        inputActions = new GameInput();
        inputActions.Player.Cancel.performed += OnCancelPerformed;
        KeyBinding.LoadBindingOverrides(inputActions);
        inputActions.Player.Enable();
    }

    public void Continue() {
        ResumeGame();
    }

    public void SwitchToOptions() {
        if (menuManager != null) {
            menuManager.SwitchToPanel(PanelType.Options);
        }
    }

    public void Leave() {
        LeaveCurrentGame();
    }

    private void OnCancelPerformed(InputAction.CallbackContext context) {
        if (!context.performed || !GameState.isPlaying) {
            return;
        }

        HandleCancel();
    }

    public void HandleCancel() {
        if (!GameState.isPlaying) {
            return;
        }

        if (!isOpen) {
            OpenPauseMenu();
            return;
        }

        if (menuManager.TryBack()) {
            return;
        }

        ResumeGame();
    }

    private void OpenPauseMenu() {
        if (menuManager == null) {
            Debug.LogError("[PauseMenu] MenuManager not found.", this);
            return;
        }

        isOpen = true;
        menuManager.HideMenu();
        menuManager.ClearHistory();
        menuManager.SwitchToPanel(PanelType.PauseMenu);
        Time.timeScale = 0f;
    }

    private void ResumeGame() {
        if (!isOpen) {
            return;
        }

        isOpen = false;
        Time.timeScale = 1f;

        if (menuManager != null) {
            menuManager.HideMenu();
            menuManager.ClearHistory();
        }
    }

    private void LeaveCurrentGame() {
        isOpen = false;
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
            NetworkManager.Singleton.Shutdown();
        }

        GameState.ResetWorld();

        if (menuManager != null) {
            menuManager.HideMenu();
            menuManager.ClearHistory();
            menuManager.SwitchToPanel(PanelType.MainMenu);
        }
    }

}
