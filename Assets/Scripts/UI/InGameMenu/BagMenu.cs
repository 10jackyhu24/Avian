using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

public class BagMenu : MonoBehaviour {
    private const float NearbyRefreshInterval = 0.25f;

    private GameInput inputActions;
    private MenuManager menuManager;
    [SerializeField] private GameObject bagMenuUI;

    private UI_BagManager bagManager;
    private PlayerController player;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;
    private float nextNearbyRefreshTime;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        bagManager = FindFirstObjectByType<UI_BagManager>();
        TryFindLocalPlayer();
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
            if (!TryFindLocalPlayer()) {
                return;
            }
        }

        if (!hasLastPlayerPosition) {
            lastPlayerPosition = player.transform.position;
            hasLastPlayerPosition = true;
            return;
        }

        bool playerMoved = (player.transform.position - lastPlayerPosition).sqrMagnitude > 0.0001f;
        if (!playerMoved && Time.unscaledTime < nextNearbyRefreshTime) {
            return;
        }

        lastPlayerPosition = player.transform.position;
        nextNearbyRefreshTime = Time.unscaledTime + NearbyRefreshInterval;
        if (bagManager.RefreshNearbyPickupables()) {
            bagManager.RefreshNearbyPickupablesUI();
        }
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

        nextNearbyRefreshTime = Time.unscaledTime + NearbyRefreshInterval;
    }

    private void CloseBagMenu() {
        menuManager.HideMenu();
        menuManager.ClearHistory();
    }

    private bool TryFindLocalPlayer() {
        NetworkManager networkManager = NetworkManager.Singleton;
        NetworkObject localPlayer = networkManager?.LocalClient?.PlayerObject;
        player = localPlayer != null ? localPlayer.GetComponent<PlayerController>() : null;
        return player != null;
    }

    private void RefreshBagMenu(int size) {
        
    }
}
