using UnityEngine;
using UnityEngine.InputSystem;

public class BagMenu : MonoBehaviour {
    private GameInput inputActions;
    private MenuManager menuManager;
    [SerializeField] private GameObject bagMenuUI;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        inputActions = new GameInput();
        inputActions.Player.Bag.performed += OnBagPerformed;
        KeyBinding.LoadBindingOverrides(inputActions);
        inputActions.Player.Enable();
    }

    private void OnEnable() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        OptionsMenu.BindingOverridesChanged += HandleBindingOverridesChanged;
    }

    private void HandleBindingOverridesChanged() {
        KeyBinding.LoadBindingOverrides(inputActions);
    }

    public void OnBagPerformed(InputAction.CallbackContext context) {
        if (!context.performed || !GameState.isPlaying || PauseMenu.isOpen) {
            return;
        }

        if (bagMenuUI.activeInHierarchy) {
            CloseBagMenu();
            return;
        }

        ShowBagMenu();
    }

    private void ShowBagMenu() {
        menuManager.SwitchToPanel(PanelType.BagMenu);
    }

    private void CloseBagMenu() {
        menuManager.HideMenu();
        menuManager.ClearHistory();
    }

    private void RefreshMenu(int size) {
        
    }
}