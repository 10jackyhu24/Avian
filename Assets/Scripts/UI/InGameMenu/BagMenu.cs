using UnityEngine;
using UnityEngine.InputSystem;

public class BagMenu : MonoBehaviour {
    private GameInput inputActions;
    private MenuManager menuManager;
    private bool isOpen;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        inputActions = new GameInput();
        inputActions.Player.Bag.performed += OnBagPerformed;
        KeyBinding.LoadBindingOverrides(inputActions);
        inputActions.Player.Enable();
    }

    private void OnBagPerformed(InputAction.CallbackContext context) {
        if (!context.performed || !GameState.isPlaying) {
            return;
        }

        if (isOpen) {
            CloseBagMenu();
            return;
        }

        ShowBagMenu();
    }

    private void ShowBagMenu() {
        isOpen = true;
    }

    private void CloseBagMenu() {
        isOpen = false;
        menuManager.HideMenu();
    }

    private void RefreshMenu(int size) {

    }
}