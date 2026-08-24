using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class BagMenu : MonoBehaviour {
    private GameInput inputActions;
    private MenuManager menuManager;
    [SerializeField] private GameObject bagMenuUI;

    private UI_BagManager bagManager;
    private PlayerController player;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        bagManager = FindFirstObjectByType<UI_BagManager>();
        player = FindFirstObjectByType<PlayerController>();
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

    private void Update() {
        if (bagMenuUI == null || !bagMenuUI.activeInHierarchy || bagManager == null) {
            hasLastPlayerPosition = false;
            return;
        }

        if (player == null) {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) {
                return;
            }
        }

        if (!hasLastPlayerPosition) {
            lastPlayerPosition = player.transform.position;
            hasLastPlayerPosition = true;
            return;
        }

        if ((player.transform.position - lastPlayerPosition).sqrMagnitude <= 0.0001f) {
            return;
        }

        lastPlayerPosition = player.transform.position;
        bagManager.RefreshNearbyPickupables();
        bagManager.RefreshNearbyPickupablesUI();
    }

    private void ShowBagMenu() {
        menuManager.SwitchToPanel(PanelType.BagMenu);
        if (bagManager != null) {
            bagManager.RefreshNearbyPickupables();
            bagManager.RefreshNearbyPickupablesUI();
        }

        if (player != null) {
            lastPlayerPosition = player.transform.position;
            hasLastPlayerPosition = true;
        }
    }

    private void CloseBagMenu() {
        menuManager.HideMenu();
        menuManager.ClearHistory();
    }

    private void RefreshBagMenu(int size) {
        
    }
}
